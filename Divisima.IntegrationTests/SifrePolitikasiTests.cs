using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Divisima.Core.Security;
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
    // ══ A2-FIX (SUPHELI #21) - SIFRE POLITIKASI TEK MERKEZDEN, HER UCTA AYNI ═══════════════
    //
    // OLCULEN ONCE-DURUM: sifre belirlenen DORT yolda DORT ayri davranis vardi ve EN GEVSEK
    // olan, EN KOLAY ulasilan yoldu:
    //   POST /api/auth/register            8 + buyuk + kucuk + rakam
    //   POST /api/seller/auth/register     AYNI KURALIN BIREBIR KOPYASI (dorduncu kopya)
    //   POST /api/account/change-password  YALNIZCA >= 6, karmasiklik YOK
    //   POST /api/auth/reset-password      HICBIR KONTROL YOK
    // Yani "Sifremi unuttum" ile gelen biri, KAYITTA reddedilecek bir sifreyi belirleyebiliyordu.
    // Bir politika ancak EN ZAYIF girisi kadar gucludur.
    //
    // BILINCLI KIRILAN PIN: LaunchFixMailZinciriTests'teki
    // "SUPHELI_SifreSifirlamada_SUNUCU_TARAFI_SIFRE_POLITIKASI_YOK_PINLENIR" bu davranisi
    // KABUL EDILMIS gibi sabitliyordu. Kural duzelince YALAN SOYLER hale gelirdi; kaldirildi ve
    // yerini asagidaki UC-UCTA-AYNI pinleri aldi.
    [Trait("Category", "Sql")]
    public class SifrePolitikasiTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaSifrePolitikasiTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        // Politikanin UC farkli kuralini tek tek ihlal eden adaylar. Degerler DUSUK ENTROPILI
        // secildi (secret-scan dersi: anahtar kelime + entropi >= 3.5 tetikler).
        private const string KisaSifre = "Aa1";          // uzunluk ihlali
        private const string BuyuksuzSifre = "aaaaaa11"; // buyuk harf yok
        private const string RakamsizSifre = "Aaaaaaaa"; // rakam yok
        private const string GecerliSifre = "Aaaaaa11";  // politikayi KARSILAR

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

        private sealed class PolitikaFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                // ══ GF-1b / K2 - RIG AYARI, DISLAMA DEGIL ═════════════════════════════════
                // K2 `change-password` ucunu `auth` hiz siniri kovasina soktu (10/dk, IP
                // basina) ve bu sinif TEK bir kovadan onlarca auth istegi atiyor - olculdu:
                // `GECERLI_SIFRE_UC_UCTA_DA_KABUL_EDILIR` 429 aldi. Limit YUKSELTILIR ki
                // olculen sey SIFRE POLITIKASI olsun, rigin kovasi olmasin.
                // Bu, depoda ZATEN kullanilan bir kalip (yedi sinif ayni ayari tasiyor).
                builder.UseSetting("RateLimit:AuthPermitLimit", "1000");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private PolitikaFactory? _factory;
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
                _factory = new PolitikaFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak sifre politikasi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // ── MERKEZ: KURALIN KENDISI ──────────────────────────────────────────────────────
        [Theory]
        [InlineData("", "boş")]
        [InlineData(KisaSifre, "en az 8")]
        [InlineData(BuyuksuzSifre, "büyük harf")]
        [InlineData("AAAAAA11", "küçük harf")]
        [InlineData(RakamsizSifre, "rakam")]
        public void MERKEZ_IHLAL_EDILEN_ILK_KURALIN_OZEL_MESAJINI_Doner(string sifre, string beklenenParca)
        {
            var hata = SifrePolitikasi.Dogrula(sifre);
            hata.Should().NotBeNull("politika bu sifreyi REDDETMELI");
            // Genel bir "sifre gecersiz" mesaji YETMEZ: kullanici hangi kurali cignedigini
            // bilmezse deneme yanilmaya duser.
            hata.Should().Contain(beklenenParca);
        }

        [Fact]
        public void MERKEZ_GECERLI_SIFREYI_KABUL_Eder()
        {
            // VAKUM KIRICI: "her seyi reddet" de yukaridaki Theory'yi gecerdi.
            SifrePolitikasi.Dogrula(GecerliSifre).Should().BeNull();
            SifrePolitikasi.Gecerli(GecerliSifre).Should().BeTrue();
        }

        [Fact]
        public void MERKEZ_TURKCE_BUYUK_KUCUK_HARFI_DE_SAYAR()
        {
            // Eski kayit kuralindaki "[A-Z]" / "[a-z]" regex'leri "Ş"/"ş" gormezdi ve Turkce
            // harfli bir sifre kullanan musteriyi GEREKSIZCE zorlardi. Kural GEVSEMEDI,
            // KAPSAMI GENISLEDI - uzunluk ve rakam sartlari aynen duruyor.
            SifrePolitikasi.Gecerli("Şşşşşş11").Should().BeTrue("Ş buyuk, ş kucuk harftir");
            SifrePolitikasi.Gecerli("şşşşşş11").Should().BeFalse("buyuk harf YOK");
        }

        // ── UC UCTA DA AYNI POLITIKA ─────────────────────────────────────────────────────
        [Fact]
        public async Task ZAYIF_SIFRE_UC_UCTA_DA_REDDEDILIR()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            // 1) KAYIT
            var kayit = await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(
                $"zayif-{Guid.NewGuid():N}@example.com", RakamsizSifre));
            kayit.StatusCode.Should().Be(HttpStatusCode.BadRequest, "kayit zayif sifreyi reddetmeli");

            // 2) SIFRE DEGISTIRME  (once gecerli bir hesap + oturum)
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var degistir = await musteri.Client.PostAsJsonAsync("/api/account/change-password", new
            {
                current_password = TestAuthHelper.TestPassword,
                new_password = RakamsizSifre
            });
            degistir.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "change-password ARTIK karmasiklik da istemeli - eski kural yalnizca >= 6 idi");

            // 3) SIFRE SIFIRLAMA - ASIL BULGU: bu uc sifreye HIC BAKMIYORDU
            var jeton = await SifirlamaJetonuAlAsync(anon, musteri.Email);
            var sifirla = await anon.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = RakamsizSifre });
            sifirla.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "reset-password ARTIK politikayi uygulamali - ONCEDEN 'abc' bile kabul ediliyordu");
        }

        [Fact]
        public async Task GECERLI_SIFRE_UC_UCTA_DA_KABUL_EDILIR()
        {
            if (Skipped()) return;
            // CIFT-ANLAM KIRICI: politikayi "her seyi reddet" diye uygulamak da yukaridaki
            // testi gecerdi. Bu test, uclarin GERCEKTEN calismaya devam ettigini sabitler.
            var anon = _factory!.CreateClient();

            var eposta = $"gecerli-{Guid.NewGuid():N}@example.com";
            var kayit = await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(eposta, GecerliSifre));
            kayit.StatusCode.Should().Be(HttpStatusCode.Created, "gecerli sifreyle kayit CALISMALI");

            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var degistir = await musteri.Client.PostAsJsonAsync("/api/account/change-password", new
            {
                current_password = TestAuthHelper.TestPassword,
                new_password = GecerliSifre
            });
            degistir.StatusCode.Should().Be(HttpStatusCode.OK, "gecerli sifreyle degistirme CALISMALI");

            // Sifre degisince TUM oturumlar kapaniyor; sifirlama icin YENI bir hesap kullanilir.
            var ikinci = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var jeton = await SifirlamaJetonuAlAsync(anon, ikinci.Email);
            var sifirla = await anon.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = GecerliSifre });
            sifirla.StatusCode.Should().Be(HttpStatusCode.OK, "gecerli sifreyle sifirlama CALISMALI");

            // VE GERCEKTEN DEGISTI: yeni sifreyle giris yapilabilmeli.
            var giris = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = ikinci.Email, password = GecerliSifre });
            giris.StatusCode.Should().Be(HttpStatusCode.OK,
                "sifirlama KOZMETIK olmamali - yeni sifre gercekten gecerli olmali");
        }

        [Fact]
        public async Task ZAYIF_SIFRE_SIFIRLAMA_JETONUNU_HARCAMAZ()
        {
            if (Skipped()) return;
            // Jeton TEK KULLANIMLIK. Politika kontrolu jeton dogrulamasindan ONCE kosuyor;
            // aksi halde kullanici zayif bir sifre denedigi icin jetonunu KAYBEDER ve
            // yeniden "sifremi unuttum" yapmak zorunda kalirdi.
            var anon = _factory!.CreateClient();
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var jeton = await SifirlamaJetonuAlAsync(anon, musteri.Email);

            (await anon.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = KisaSifre })).StatusCode
                .Should().Be(HttpStatusCode.BadRequest);

            // AYNI jeton hala GECERLI olmali.
            (await anon.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = GecerliSifre })).StatusCode
                .Should().Be(HttpStatusCode.OK, "reddedilen deneme jetonu TUKETMEMELI");
        }

        // ══ GF-1b / K10 (GF1-B10) - SIFIRLAMA JETONU ES ZAMANLI DA TEK KULLANIMLIK ════════
        //
        // Ustteki pin jetonun SIRAYLA tek kullanimlik oldugunu sabitliyor. Bu pin ayni
        // sozlesmenin ES ZAMANLI halini olcer: iki istek AYNI jetonu AYNI ANDA sunarsa.
        //
        // OLCULEN ONCE-DURUM (pinsizdi): oku-kontrol-et-yaz arasinda kosul YOKTU
        // (`GetAsync(token)` -> expiry kontrolu -> tam-varlik `UpdateAsync`), yani iki istek
        // de jetonu "gecerli" gorup gecebiliyordu. ZARAR: jetonu ele geciren saldirgan,
        // kurbanin sifirlama istegiyle YARISA girip SON YAZAN olabilir - kurban "sifremi
        // degistirdim" der, hesap saldirganin sifresindedir. Ayrica "TEK KULLANIMLIK"
        // sozlesmesi, en cok onemsedigi anda (yaris) gecersiz oluyordu.
        [Fact]
        public async Task K10B_AYNI_SIFIRLAMA_JETONU_ESZAMANLI_TEK_KEZ_KULLANILIR()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var jeton = await SifirlamaJetonuAlAsync(anon, musteri.Email);

            // AYRI istemciler: tek HttpClient uzerinden es zamanli gonderim, olcumu
            // istemci tarafinda serilestirme riskine acardi.
            var a = _factory!.CreateClient();
            var b = _factory!.CreateClient();
            const string sifreA = "YarisAlfa1!x";
            const string sifreB = "YarisBeta1!x";

            var sonuclar = await Task.WhenAll(
                a.PostAsJsonAsync("/api/auth/reset-password", new { token = jeton, new_password = sifreA }),
                b.PostAsJsonAsync("/api/auth/reset-password", new { token = jeton, new_password = sifreB }));

            sonuclar.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1,
                "AYNI jeton es zamanli sunuldugunda YALNIZ BIRI basarili olmali - "
                + "jeton TEK KULLANIMLIK");

            // ALAN BAZLI (vakum kirici): kazananin sifresi GERCEKTEN gecerli olmali ve
            // kaybedenin sifresi hesaba YAZILMAMIS olmali. Yalniz durum koduna bakmak,
            // "ikisi de yazdi ama biri 400 dondu" halini kaciririrdi.
            var girisA = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = musteri.Email, password = sifreA });
            var girisB = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = musteri.Email, password = sifreB });
            new[] { girisA.StatusCode, girisB.StatusCode }.Count(k => k == HttpStatusCode.OK)
                .Should().Be(1, "hesapta TEK sifre gecerli olmali - kaybeden yazma UYGULANMAMALI");

            // Jeton TUKENMIS olmali (ucuncu bir deneme de gecmemeli).
            (await anon.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = "UcuncuDeneme1!x" })).StatusCode
                .Should().Be(HttpStatusCode.BadRequest, "jeton yarisin ardindan TUKENMIS olmali");
        }

        // ══ GF-1b / F2+F3 (R-1b9) - SIFIRLAMA IZ BIRAKIR ve ESKI JETONLARI OLDURUR ═══════
        //
        // OLCULEN ONCE-DURUM (ikisi de PINSIZDI):
        //  (F2) Basarili sifirlama HICBIR denetim izi birakmiyordu. K10'un CAS'i
        //       `ExecuteUpdateAsync` kullanir, o da `AuditInterceptor`in dayandigi
        //       SaveChanges'i ATLAR -> `audit_logs` 0. Ustelik `ResetPassword` diye bir
        //       `security_events` kaydi da HIC yazilmiyordu -> olay TAMAMEN IZSIZ.
        //  (F3) Sifirlama `revoked_before` esigini YAZMIYORDU (change-password ve
        //       logout-all yaziyordu). Yani "sifremi unuttum" ile hesabini geri alan
        //       kullanicinin saldirgani, ELINDEKI ACCESS TOKEN ile 15 dakikaya kadar
        //       ISLEM YAPMAYA DEVAM edebiliyordu.
        [Fact]
        public async Task R1b9_SIFIRLAMA_IZ_BIRAKIR_ve_ESKI_ACCESS_ile_REFRESHI_OLDURUR()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            // POZITIF OLAY KOSULU (vakum yasagi): eski jeton sifirlamadan ONCE CALISIYOR.
            (await musteri.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.OK, "on kosul: access token sifirlamadan ONCE gecerli olmali");

            int auditOnce, olayOnce;
            await using (var ilk = NewContext())
            {
                auditOnce = await ilk.Set<AuditLog>().AsNoTracking()
                    .CountAsync(a => a.entity_id == musteri.CustomerId.ToString());
                olayOnce = await ilk.Set<SecurityEvent>().AsNoTracking()
                    .CountAsync(e => e.customer_id == musteri.CustomerId && e.event_type == "ResetPassword");
            }

            // ══ AYNI-SANIYE JETON PENCERESI (BILINEN - K1B'de de birebir yasandi) ═══════
            // `iat` claim'i SANIYE cozunurlukludur ve iptal kosulu KASITLI olarak
            // `iat < esik` (strictly less). Jeton ile esik AYNI saniyeye duserse jeton
            // iptal edilmez - bu bir kusur DEGIL, esigin kendi anini kapsamamasi icin
            // bilincli secim. Testin butun adimlari milisaniyeler icinde kostugu icin
            // bekleme ZORUNLU; olculdu: beklemesiz kosumda eski jeton 200 donuyor.
            await Task.Delay(1100);

            var jeton = await SifirlamaJetonuAlAsync(anon, musteri.Email);
            const string yeniSifre = "SifirlamaSonrasi1!x";
            (await anon.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = yeniSifre })).StatusCode
                .Should().Be(HttpStatusCode.OK, "sifirlama basarili olmali");

            // ── F3: ESKI ACCESS TOKEN ARTIK REDDEDILIR ────────────────────────────────
            (await musteri.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized,
                    "sifirlamadan SONRA eski access token REDDEDILMELI - once 200 doneriyordu");

            // ── F2: IZ GERCEKTEN YAZILDI ("401 dondu" tek basina yetmez) ─────────────
            await using var son = NewContext();
            (await son.Set<AuditLog>().AsNoTracking()
                .CountAsync(a => a.entity_id == musteri.CustomerId.ToString()))
                .Should().BeGreaterThan(auditOnce,
                    "basarili sifirlama audit_logs satiri BIRAKMALI - CAS interceptor'i atlar");
            (await son.Set<SecurityEvent>().AsNoTracking()
                .CountAsync(e => e.customer_id == musteri.CustomerId && e.event_type == "ResetPassword"))
                .Should().BeGreaterThan(olayOnce,
                    "basarili sifirlama ResetPassword guvenlik olayi YAZMALI - once HIC yazilmiyordu");

            // ── CIFT-ANLAM KIRICI: yeni sifre GERCEKTEN gecerli (401 "hesap oldu" degil) ──
            (await anon.PostAsJsonAsync("/api/auth/login",
                new { email = musteri.Email, password = yeniSifre })).StatusCode
                .Should().Be(HttpStatusCode.OK, "yeni sifreyle giris CALISMALI - hesap yasiyor");
        }


        // ── P-H3) MANTIK-FIX-3 / K3 - SIFRE DEGISTIRME GERCEKTEN DEGISTIRIR ──────────
        //
        // OLCULEN ONCE-DURUM: sunucu ucu ZATEN VARDI ve DOGRUYDU; kirik olan ISTEMCIYDI
        // (#pfPassSave hicbir yerde bagli degildi, index.html'in govdesi API'ye gitmeden
        // "Sifren guncellendi" diyordu). Bu pin sunucu SOZLESMESINI sabitler: yanlis
        // mevcut sifre REDDEDILIR ve degisim KOZMETIK DEGILDIR.
        //
        // CIFT-ANLAM KIRICI: "her degisimi reddet" gibi bir uygulama ilk asserti gecer
        // ama ikinci bacakta (eski 401 / yeni 200) KIRILIR.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task YANLIS_MEVCUT_SIFRE_REDDEDILIR_ve_DEGISIM_ESKI_SIFREYI_GECERSIZ_Kilar()
        {
            if (Skipped()) return;

            var anon = _factory!.CreateClient();
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            // (1) YANLIS mevcut sifre -> 400 ve mesaj SEBEBI SOYLER.
            var yanlis = await musteri.Client.PostAsJsonAsync("/api/account/change-password", new
            {
                current_password = "BuKesinlikleYanlis1",
                new_password = GecerliSifre
            });
            yanlis.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "yanlis mevcut sifreyle degisim REDDEDILMELI");
            var yanlisGovde = await yanlis.Content.ReadAsStringAsync();
            yanlisGovde.Should().Contain("Mevcut",
                "mesaj SEBEBI soylemeli - istemci hata eslemesi bu metne dayaniyor (MK-7 capasi)");

            // VAKUM KIRICI: reddedilen istek sifreyi DEGISTIRMEMIS olmali.
            (await anon.PostAsJsonAsync("/api/auth/login",
                new { email = musteri.Email, password = TestAuthHelper.TestPassword })).StatusCode
                .Should().Be(HttpStatusCode.OK, "reddedilen degisim mevcut sifreyi BOZMAMALI");

            // (2) DOGRU mevcut sifre -> 200.
            (await musteri.Client.PostAsJsonAsync("/api/account/change-password", new
            {
                current_password = TestAuthHelper.TestPassword,
                new_password = GecerliSifre
            })).StatusCode.Should().Be(HttpStatusCode.OK, "dogru mevcut sifreyle degisim CALISMALI");

            // (3) DEGISIM KOZMETIK DEGIL: eski sifre 401, yeni sifre 200.
            (await anon.PostAsJsonAsync("/api/auth/login",
                new { email = musteri.Email, password = TestAuthHelper.TestPassword })).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "ESKI sifre artik gecersiz olmali");
            (await anon.PostAsJsonAsync("/api/auth/login",
                new { email = musteri.Email, password = GecerliSifre })).StatusCode
                .Should().Be(HttpStatusCode.OK, "YENI sifreyle giris yapilabilmeli");
        }
        [Fact]
        public void HICBIR_UC_KENDI_SIFRE_KURALINI_TANIMLAMAZ()
        {
            // SINIF DUZEYI TARAMA: politikanin BESINCI bir kopyasi eklenirse bu pin kirilir.
            // Kaynak okunur - yansima ile bakmak kural KOPYASINI goremezdi.
            var kok = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (kok != null && !System.IO.Directory.Exists(
                System.IO.Path.Combine(kok.FullName, "Divisima.Bussiness")))
                kok = kok.Parent;
            kok.Should().NotBeNull("depo koku bulunmali - sessiz skip YOK");

            var dosyalar = System.IO.Directory.GetFiles(
                System.IO.Path.Combine(kok!.FullName, "Divisima.Bussiness"), "*.cs",
                System.IO.SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}")
                         && !p.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}"));

            foreach (var yol in dosyalar)
            {
                var metin = System.IO.File.ReadAllText(yol);
                // Politikanin eski kopyalarinin imzasi: sifre alanina MinimumLength(8) ya da
                // "[A-Z]" regex'i. Ikisi de artik YALNIZ merkezde olmali (merkez Core'da).
                metin.Should().NotContain("MinimumLength(8)",
                    $"sifre kurali kopyasi kalmamali: {System.IO.Path.GetFileName(yol)}");
                metin.Should().NotContain(".Matches(\"[A-Z]\")",
                    $"sifre kurali kopyasi kalmamali: {System.IO.Path.GetFileName(yol)}");
            }

            // VAKUM KIRICI: tarama GERCEKTEN dosya okuyor olmali.
            dosyalar.Count().Should().BeGreaterThan(50, "Bussiness katmani taranmis olmali");
        }

        // ── Yardimcilar ─────────────────────────────────────────────────────────────────
        private static object KayitGovdesi(string eposta, string sifre) => new
        {
            name = "Politika Musteri",
            email = eposta,
            phone = "5550000000",
            password = sifre,
            accepted_terms = true,
            accepted_privacy = true,
            accepted_marketing = false
        };

        private static async Task<string> SifirlamaJetonuAlAsync(HttpClient anon, string eposta)
        {
            (await anon.PostAsJsonAsync("/api/auth/forgot-password", new { email = eposta }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            // ══ GF-1b / K3 UYARLAMASI ═════════════════════════════════════════════════════
            // Kolon artik DUZ jeton degil SHA-256 OZET tutuyor, yani DB'den okunan deger
            // jeton olarak KULLANILAMAZ (duz jeton YALNIZ maile gider - bu sinif mail
            // yakalamiyor). Test BILINEN bir duz jeton belirleyip ozetini yaziyor.
            // GERCEK URETIM YOLU YINE KOSUYOR: ustteki `forgot-password` cagrisi 200
            // donduruyor ve satirin GERCEKTEN doldugu asagida DOGRULANIYOR.
            // NIYET DEGISMEDI: elde GECERLI bir sifirlama jetonu olmasi.
            await using var ctx = NewContext();
            var m = await ctx.Set<Customer>()
                .FirstAsync(c => c.email == eposta.ToLowerInvariant());
            m.password_reset_token.Should().NotBeNullOrWhiteSpace("sifirlama jetonu uretilmis olmali");
            m.password_reset_token!.Length.Should().Be(Divisima.Core.Security.Tokens.JetonOzeti.OzetUzunlugu,
                "DB'de OZET durmali (64 hex) - duz jeton DURMAMALI");

            var duzJeton = "gf1b-reset-" + Guid.NewGuid().ToString("N");
            m.password_reset_token = Divisima.Core.Security.Tokens.JetonOzeti.Hesapla(duzJeton);
            await ctx.SaveChangesAsync();
            return duzJeton;
        }
    }
}
