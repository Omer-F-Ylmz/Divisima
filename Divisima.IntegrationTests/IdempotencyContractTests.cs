using System.Net;
using System.Net.Http.Json;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // === DALGA D / D4 - IDEMPOTENCY SOZLESMESI (CANLI TURDAN PINLENDI) ===================
    //
    // D4 bugune kadar YALNIZ STATIK okunmustu. Bu dalgada gercek API'ye gercek hesaplarla
    // canli tur yapildi ve olculenler buraya pinlendi. Tur, statik okumamdaki BIR HATAYI da
    // duzeltti: "anahtar kapsami key|path|user" sanmistim - MIDDLEWARE'da user bileseni YOK.
    //
    // IKI AYRI MEKANIZMA VAR ve DAVRANISLARI FARKLI:
    //   1) IdempotencyMiddleware  - TUM POST/PUT'larda, Program.cs:523'te yani
    //      UseAuthentication'DAN ONCE. Anahtar: "idem:{METHOD}:{PATH}:{key}" - KULLANICI YOK.
    //      Tekrar -> 409, REPLAY YOK.
    //   2) IdempotencyAttribute   - yalniz isaretli action'larda (order/place, guest-checkout,
    //      loyalty/redeem, giftcard/redeem), auth SONRASI. Anahtar kapsaminda KULLANICI VAR.
    //      Tekrar -> ILK YANITIN KOPYASI + "Idempotency-Replayed: true".
    //
    // MIDDLEWARE ONCE KOSTUGU ICIN (2)'nin replay'i URETIMDE ULASILAMAZ - canli olculdu.
    //
    // BU SINIFTAKI "SUPHELI_" PINLERI BUGUNKU DAVRANISI SABITLER, DOGRU OLDUGUNU IDDIA ETMEZ.
    // Ev kurali: supheli uretim davranisi DUZELTILMEZ, PINLENIR; duzeltme karari kullanicinin.
    [Trait("Category", "Sql")]
    public class IdempotencyContractTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaIdempotencyTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = TestDbAdi.Cozumle(DbName) }.ConnectionString;
            }
        }

        private sealed class IdemFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                // Bu sinif tek testte BIRDEN COK hesap aciyor; auth kovasi olcumu bozmasin.
                builder.UseSetting("RateLimit:AuthPermitLimit", "1000");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private IdemFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new IdemFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak idempotency testleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static object Adres() => new
        {
            title = "Ev",
            full_name = "D4 Test",
            phone = "5550000000",
            city = "Istanbul",
            district = "Kadikoy",
            full_address = "Test mah. 1",
            zip_code = "34000",
            is_default = true
        };

        private static async Task<HttpResponseMessage> AdresYazAsync(HttpClient c, string? anahtar, object govde)
        {
            var istek = new HttpRequestMessage(HttpMethod.Post, "/api/address/upsert")
            {
                Content = JsonContent.Create(govde)
            };
            if (anahtar != null) istek.Headers.Add("Idempotency-Key", anahtar);
            return await c.SendAsync(istek);
        }

        private async Task<int> AdresSayisiAsync(string email)
        {
            await using var ctx = NewContext();
            var musteri = await ctx.Set<Customer>().AsNoTracking().SingleAsync(x => x.email == email);
            return await ctx.Set<Address>().AsNoTracking().CountAsync(a => a.customer_id == musteri.id);
        }

        // ── 1) ASIL VAAT: AYNI ANAHTAR IKINCI KEZ ISLENMEZ ──────────────────────────────
        [Fact]
        public async Task AyniAnahtar_IKINCI_ISTEK_409_ve_IKINCI_KAYIT_OLUSMAZ()
        {
            if (Skipped()) return;
            var u = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var anahtar = "d4-" + Guid.NewGuid().ToString("N");

            var ilk = await AdresYazAsync(u.Client, anahtar, Adres());
            ilk.StatusCode.Should().Be(HttpStatusCode.Created, "ilk istek ISLENMELI");

            var ikinci = await AdresYazAsync(u.Client, anahtar, Adres());
            ikinci.StatusCode.Should().Be(HttpStatusCode.Conflict, "ayni anahtar ikinci kez ISLENMEMELI");

            // CIFT-ANLAM KIRICI: 409 KOZMETIK DEGIL - ikinci satir GERCEKTEN olusmamis olmali.
            (await AdresSayisiAsync(u.Email)).Should().Be(1, "cift islem ENGELLENMIS olmali");

            // VAKUM KIRICI: mekanizma her seyi engellemiyor - FARKLI anahtar islenir.
            var farkli = await AdresYazAsync(u.Client, "d4-" + Guid.NewGuid().ToString("N"), Adres());
            farkli.StatusCode.Should().Be(HttpStatusCode.Created, "FARKLI anahtar islenmeli");
            (await AdresSayisiAsync(u.Email)).Should().Be(2);
        }

        // ── 2) VAKUM KIRICI: ANAHTARSIZ ISTEKLER ETKILENMEZ ────────────────────────────
        [Fact]
        public async Task ANAHTARSIZ_Istekler_ETKILENMEZ()
        {
            if (Skipped()) return;
            var u = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            (await AdresYazAsync(u.Client, null, Adres())).StatusCode.Should().Be(HttpStatusCode.Created);
            (await AdresYazAsync(u.Client, null, Adres())).StatusCode.Should().Be(HttpStatusCode.Created,
                "Idempotency-Key GONDERILMEDIGINDE middleware devrede OLMAMALI");

            (await AdresSayisiAsync(u.Email)).Should().Be(2, "iki istek de islenmis olmali");
        }

        // ── 3) CAPRAZ KULLANICI CAKISMASI KAPANDI ──────────────────────────────────────
        //
        // BILINCLI KIRILAN PIN: SUPHELI_CAPRAZ_KULLANICI_AYNI_ANAHTAR_IKINCININ_ISTEGINI_
        // DUSURUR_PINLENIR. O pin CANLI OLCULEN zarari sabitliyordu (A anahtar K ile 201,
        // B AYNI K ile 409 ve B'nin kaydi HIC olusmuyordu). Kullanici karariyla duzeltildi:
        // middleware `UseAuthorization`DAN SONRAYA tasindi ve anahtara KULLANICI bileseni
        // eklendi. Eski pin artik YANLIS bir sozlesmeyi savunurdu.
        [Fact]
        public async Task CAPRAZ_KULLANICI_ETKILENMEZ_HER_KULLANICI_KENDI_KAPSAMINDA()
        {
            if (Skipped()) return;
            var a = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var b = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var ortak = "d4-ortak-" + Guid.NewGuid().ToString("N");

            (await AdresYazAsync(a.Client, ortak, Adres())).StatusCode.Should().Be(HttpStatusCode.Created);

            // ASIL OLCUM: B AYNI anahtarla gonderiyor ve ARTIK ETKILENMIYOR.
            var bYanit = await AdresYazAsync(b.Client, ortak, Adres());
            bYanit.StatusCode.Should().Be(HttpStatusCode.Created,
                "anahtar KULLANICIYLA kapsanmali - B'nin istegi A yuzunden DUSMEMELI");
            (await AdresSayisiAsync(b.Email)).Should().Be(1, "B'nin kaydi GERCEKTEN olusmali");

            // CIFT-ANLAM KIRICI: kullanici kapsami korumayi KALDIRMADI - AYNI kullanici
            // AYNI anahtari tekrar kullanirsa hala 409 alir.
            (await AdresYazAsync(b.Client, ortak, Adres())).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "AYNI kullanici + AYNI anahtar hala ENGELLENMELI");
            (await AdresSayisiAsync(b.Email)).Should().Be(1, "ikinci kayit OLUSMAMALI");

            // A'nin kaydi da tek kalmali (B'nin istegi A'nin kapsamina karismadi).
            (await AdresSayisiAsync(a.Email)).Should().Be(1);
        }

        // ── 4) BASARISIZ ISTEK ANAHTARI ARTIK YAKMIYOR ─────────────────────────────────
        //
        // BILINCLI KIRILAN PIN: SUPHELI_BASARISIZ_ISTEK_ANAHTARI_YAKAR_DUZELTILMIS_TEKRAR_
        // DENEME_409_PINLENIR. CANLI OLCULEN zarar: bozuk govde -> 400; ardindan AYNI anahtar
        // + GECERLI govde -> 409, yani istemci hatasini duzeltse bile istegi HIC islenmiyordu
        // (turda 401 ve 405 ile de birebir ayni sonuc alinmisti).
        // Kullanici karariyla duzeltildi: anahtar YALNIZCA 2xx yanitta tutulur.
        [Fact]
        public async Task BASARISIZ_ISTEK_ANAHTARI_YAKMAZ_DUZELTILMIS_TEKRAR_DENEME_ISLENIR()
        {
            if (Skipped()) return;
            var u = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var anahtar = "d4-" + Guid.NewGuid().ToString("N");

            var bozuk = await AdresYazAsync(u.Client, anahtar, new { title = "", full_name = "", city = "", district = "", full_address = "" });
            bozuk.StatusCode.Should().Be(HttpStatusCode.BadRequest, "on kosul: ilk istek GERCEKTEN basarisiz olmali");

            // ASIL OLCUM: AYNI anahtar, DUZELTILMIS govde -> ISLENMELI.
            var duzeltilmis = await AdresYazAsync(u.Client, anahtar, Adres());
            duzeltilmis.StatusCode.Should().Be(HttpStatusCode.Created,
                "basarisiz istek anahtari SERBEST BIRAKMALI - duzeltilmis tekrar deneme islenmeli");
            (await AdresSayisiAsync(u.Email)).Should().Be(1, "adres GERCEKTEN yazilmis olmali");

            // CIFT-ANLAM KIRICI: serbest birakma korumayi KALDIRMADI - BASARILI istekten
            // sonra AYNI anahtar hala 409 alir. Aksi halde "hep birak" gibi bir uygulama da
            // yukaridaki assert'i gecerdi ve cift islem korumasi TUMDEN kaybolurdu.
            (await AdresYazAsync(u.Client, anahtar, Adres())).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "BASARILI istekten sonra ayni anahtar ENGELLENMELI");
            (await AdresSayisiAsync(u.Email)).Should().Be(1, "ikinci kayit OLUSMAMALI");
        }

        // ── 5) FILTRENIN REPLAY DALI ARTIK GERCEKTEN CALISIYOR ─────────────────────────
        //
        // BILINCLI KIRILAN PIN: SUPHELI_FILTRE_REPLAYI_ULASILAMAZ_MIDDLEWARE_ONCE_409_DONER.
        // CANLI OLCULEN durum: isaretli bir ucta 2. istek middleware'den 409 aliyor ve
        // "Idempotency-Replayed" basligi HIC gelmiyordu; yani ag tekrari yapan musteri ILK
        // istegin sonucunu (or. siparis numarasi) OGRENEMIYORDU.
        //
        // TASARIM KARARI (olcume dayali, kullaniciya raporlandi): FILTRE KALIR, MIDDLEWARE
        // DARALIR. Filtre yalnizca DORT PARA UCUNDA ve orada REPLAY dogru davranistir;
        // middleware geri kalan tum mutasyonlarda genis emniyet agi olarak kalir. Middleware
        // artik endpoint metadata'sinda IdempotencyAttribute gorurse KENARA CEKILIYOR -
        // boylece iki mekanizma da ULASILABILIR, OLU KOD YOK.
        [Fact]
        public async Task FILTRE_REPLAYI_CALISIR_IKINCI_ISTEK_ILK_YANITI_DONER()
        {
            if (Skipped()) return;
            var u = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            // ON KOSUL: basarili (2xx) bir yanit uretebilmek icin puan gerekiyor - filtre
            // ARTIK yalnizca 2xx'i cache'liyor (D4: 4xx "kesin sonuc" DEGILDIR).
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<Customer>().SingleAsync(x => x.email == u.Email);
                m.loyalty_points = 500;
                await ctx.SaveChangesAsync();
            }

            var anahtar = "d4-" + Guid.NewGuid().ToString("N");

            var ilk = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem/100");
            ilk.Headers.Add("Idempotency-Key", anahtar);
            var ilkYanit = await u.Client.SendAsync(ilk);
            ((int)ilkYanit.StatusCode).Should().BeInRange(200, 299,
                $"on kosul: ilk istek BASARILI olmali: {await ilkYanit.Content.ReadAsStringAsync()}");
            ilkYanit.Headers.Contains("Idempotency-Replayed").Should().BeFalse("ilk istek replay DEGIL");

            // ASIL OLCUM: ikinci istek 409 DEGIL, ILK YANITIN KOPYASI olmali.
            var ikinci = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem/100");
            ikinci.Headers.Add("Idempotency-Key", anahtar);
            var ikinciYanit = await u.Client.SendAsync(ikinci);

            ikinciYanit.StatusCode.Should().NotBe(HttpStatusCode.Conflict,
                "middleware isaretli uctan CEKILMELI - filtrenin replay dali calismali");
            ikinciYanit.Headers.Contains("Idempotency-Replayed").Should().BeTrue(
                "replay basligi GELMELI - istemci ILK istegin sonucunu ogrenebilmeli");
            ((int)ikinciYanit.StatusCode).Should().Be((int)ilkYanit.StatusCode,
                "replay ILK yanitin durum kodunu tasimali");

            // CIFT-ANLAM KIRICI: replay KOZMETIK DEGIL - islem IKINCI KEZ UYGULANMAMIS olmali.
            // 500 puanin yalniz 100'u harcanmis olmali (cift harcama olsaydi 300 kalirdi).
            await using (var son = NewContext())
            {
                var m = await son.Set<Customer>().AsNoTracking().SingleAsync(x => x.email == u.Email);
                m.loyalty_points.Should().Be(400, "puan YALNIZ BIR KEZ harcanmis olmali");
            }
        }

        // ── 6) GUVENLIK-FIX-4 / #22(a): FILTREDE DE CAPRAZ KULLANICI AYRISIR ────────────
        //
        // OLCULEN ONCE-DURUM (canli, /api/order/place, iki GERCEK hesap):
        //   A + anahtar K -> 201 siparis 180
        //   B + AYNI K    -> 201 "Idempotency-Replayed: true", GOVDEDE 180
        //   B'nin siparis sayisi -> 0     (B'nin istegi SESSIZCE dustu)
        // Kok sebep: filtre kapsami `User.Identity.Name` okuyordu ve o DAIMA null - yani
        // HER kimlikli cagiran "anon" kapsamina dusuyordu. D4 bunu MIDDLEWARE icin
        // duzeltmisti; FILTRE atlanmisti. Artik ikisi de `IdempotencyKimligi.Coz`ten okur.
        [Fact]
        public async Task FILTREDE_CAPRAZ_KULLANICI_AYRISIR_A_NIN_ANAHTARI_B_YI_ETKILEMEZ()
        {
            if (Skipped()) return;
            var a = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var b = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            await using (var ctx = NewContext())
            {
                foreach (var eposta in new[] { a.Email, b.Email })
                {
                    var m = await ctx.Set<Customer>().SingleAsync(x => x.email == eposta);
                    m.loyalty_points = 500;
                }

                await ctx.SaveChangesAsync();
            }

            var anahtar = "gf4-" + Guid.NewGuid().ToString("N");

            var aYanit = await PuanHarcaAsync(a.Client, anahtar);
            ((int)aYanit.StatusCode).Should().BeInRange(200, 299,
                $"on kosul: A'nin istegi islenmeli: {await aYanit.Content.ReadAsStringAsync()}");

            var bYanit = await PuanHarcaAsync(b.Client, anahtar);
            ((int)bYanit.StatusCode).Should().BeInRange(200, 299,
                "B'nin istegi KENDI kapsaminda islenmeli - A'nin anahtari B'yi DUSURMEMELI");
            bYanit.Headers.Contains("Idempotency-Replayed").Should().BeFalse(
                "B, A'nin yanitini REPLAY olarak ALMAMALI");

            // ASIL KANIT: B'nin islemi GERCEKTEN uygulanmis olmali (sessizce dusmemis).
            await using (var son = NewContext())
            {
                var mB = await son.Set<Customer>().AsNoTracking().SingleAsync(x => x.email == b.Email);
                mB.loyalty_points.Should().Be(400, "B'nin puani GERCEKTEN harcanmis olmali");
            }

            // CIFT-ANLAM KIRICI: kullanici kapsami korumayi KALDIRMADI - AYNI kullanici
            // AYNI anahtarla tekrar denerse HALA replay alir ve islem TEKRARLANMAZ.
            var aTekrar = await PuanHarcaAsync(a.Client, anahtar);
            aTekrar.Headers.Contains("Idempotency-Replayed").Should().BeTrue(
                "ayni kullanici + ayni anahtar HALA replay olmali");
            await using (var son = NewContext())
            {
                var mA = await son.Set<Customer>().AsNoTracking().SingleAsync(x => x.email == a.Email);
                mA.loyalty_points.Should().Be(400, "A'nin puani YALNIZ BIR KEZ harcanmis olmali");
            }
        }

        private static async Task<HttpResponseMessage> PuanHarcaAsync(HttpClient c, string anahtar)
        {
            var istek = new HttpRequestMessage(HttpMethod.Post, "/api/loyalty/redeem/100");
            istek.Headers.Add("Idempotency-Key", anahtar);
            return await c.SendAsync(istek);
        }
    }
}
