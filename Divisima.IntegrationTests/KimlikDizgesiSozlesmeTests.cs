using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    // ══ KALITE SUPURMESI DALGA 1 - KIMLIK DIZGESI SOZLESMESI ═══════════════════════════════
    //
    // KOK ILKE: kimlik/makine dizgelerinde (e-posta, kupon kodu, URL yolu, MIME tipi, saglayici
    // durum kodu) casing ve karsilastirma KULTURDEN BAGIMSIZ olmalidir. Kultur YALNIZ
    // insan-gorunur bicimlendirmede kullanilir (fatura tutari, tarih - Sprint 8 madde 13).
    //
    // NEDEN: uygulama tr-TR'ye pinli ve Turkcede 'i' ile 'I' AYNI HARF DEGIL (cift'ler I<->ı
    // ve İ<->i). Veritabani collation'i da Turkish_CI_AS. Olculdu: 'irem' = 'IREM' -> FARKLI.
    // Kimlik dizgesinde kulturlu casing kullanildiginda ayni degerin iki yazimi FARKLI ANAHTAR
    // uretiyordu.
    //
    // ORTAM SARTI: bu sinifin olctugu seylerin ANLAMLI olmasi icin veritabani Turkish_CI_AS
    // olmalidir - Latin1'de 'irem' = 'IREM' ESIT doner ve buradaki assert'lerin bir kismi
    // duzeltme OLMASA DA yesil kalirdi. Bu sart CollationMetaPinTests'te ayrica pinli.
    [Trait("Category", "Sql")]
    public class KimlikDizgesiSozlesmeTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaKimlikDizgesiTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = DbName }.ConnectionString;
            }
        }

        private sealed class KimlikFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                // Kayit/giris "auth" kovasinda (10/dk). Bu sinif tek testte birden cok kayit+giris
                // yapiyor ve test sunucusunda RemoteIpAddress null oldugu icin hepsi AYNI kovaya
                // duser. Limitin KENDISI AuthRateLimitPinTests'te uretim varsayilaniyla pinli.
                builder.UseSetting("RateLimit:AuthPermitLimit", "1000");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private KimlikFactory? _factory;
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
                _factory = new KimlikFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak kimlik dizgesi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task<HttpResponseMessage> KayitAsync(string email) =>
            await _factory!.CreateClient().PostAsJsonAsync("/api/auth/register", new
            {
                name = "Kimlik Testi",
                email,
                phone = "5550000000",
                password = TestAuthHelper.TestPassword,
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });

        // Kayit + GERCEK verify-email ucundan dogrulama (token DB'den okunur - e-posta teslimi
        // dis bagimlilik; TestAuthHelper ile AYNI desen).
        private async Task KayitVeDogrulaAsync(string email)
        {
            (await KayitAsync(email)).StatusCode.Should().Be(HttpStatusCode.Created, "on kosul: kayit basarili olmali");

            string token;
            await using (var ctx = NewContext())
            {
                var kanonik = email.Trim().ToLowerInvariant();
                var c = await ctx.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(x => x.email == kanonik);
                c.Should().NotBeNull("kayit KANONIK (invariant kucuk) e-posta ile saklanmali");
                token = c!.email_verification_token!;
            }
            var v = await _factory!.CreateClient()
                .GetAsync($"/api/auth/verify-email?token={Uri.EscapeDataString(token)}");
            v.IsSuccessStatusCode.Should().BeTrue("on kosul: dogrulama basarili olmali");
        }

        private async Task<HttpStatusCode> GirisAsync(string email) =>
            (await _factory!.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { email, password = TestAuthHelper.TestPassword })).StatusCode;

        // ── B1-a) AYNI ADRESIN FARKLI YAZIMI IKINCI BIR HESAP ACMAZ ───────────────────────
        //
        // CANLI ZARAR (Dalga 1'de olculdu): 'Iris.Kalite@example.com' ve 'iris.kalite@example.com'
        // ARDI ARDINA 201 dondu ve customers'ta IKI SATIR olustu (id 14 'ırıs...', id 15 'iris...').
        // Tekillik kontrolu kulturlu kucultme yaptigi icin iki yazim FARKLI ANAHTAR uretiyordu.
        //
        // ══ BILINCLI DEGISTIRILDI - GUVENLIK-FIX (G2) ═══════════════════════════════════════
        // Testin ADI ve DURUM KODU ASSERT'I degisti; OLCTUGU INVARIANT AYNEN DURUYOR.
        // Eskiden `ikinci.StatusCode.Should().NotBe(Created)` yaziyordu. O assert, B1'in gercek
        // invariantini ("tek adres -> TEK hesap") DEGIL, o gunku YAN ETKISINI (400 donmesi)
        // sabitliyordu. G2 ile kayit ucu artik var olan adres icin de AYNI 201'i donuyor
        // (enumeration engeli), yani eski assert dogru davranisi KIRMIS olurdu.
        // Invarianti koruyan asil assert asagida ve DEGISMEDI: satir sayisi 1, deger kanonik.
        // Enumeration esitliginin KENDISI ayrica pinli: SecurityHardeningTests ->
        // `Kayit_VAR_OLAN_ve_YENI_ADRES_AYNI_YANITI_Doner`.
        [Fact]
        public async Task AyniAdresinFarkliCasingi_IKINCI_HESAP_ACMAZ()
        {
            if (Skipped()) return;
            var yerel = "iris-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var kucuk = yerel + "@example.com";
            var buyuk = yerel.ToUpperInvariant() + "@EXAMPLE.COM";

            (await KayitAsync(buyuk)).StatusCode.Should().Be(HttpStatusCode.Created, "ilk kayit gecmeli");
            var ikinci = await KayitAsync(kucuk);

            // G2: yanit ARTIK ayirt edilemez (201). Onemli olan yanit degil, DB'deki sonuc.
            ikinci.StatusCode.Should().Be(HttpStatusCode.Created,
                "G2 sonrasi kayit ucu var olan adresi de AYNI yanitla karsilar - 'zaten kayitli' " +
                "diyen bir yanit enumeration'dir");

            await using var ctx = NewContext();
            var satirlar = await ctx.Set<Customer>().AsNoTracking()
                .Where(c => c.email.Contains(yerel)).ToListAsync();
            satirlar.Should().HaveCount(1,
                $"tek adres -> TEK hesap. Bulunanlar: {string.Join(" | ", satirlar.Select(s => s.email))}");
            satirlar[0].email.Should().Be(kucuk,
                "saklanan deger KANONIK (invariant kucuk harf) olmali - Turkce kucultme 'I'yi 'ı' yapardi");
        }

        // ── B1-b) KAYITLI KULLANICI HER YAZIMLA GIRIS YAPABILIR ───────────────────────────
        //
        // Ikinci ve daha agir sonuc: kullanici ancak KAYITTA yazdigi harf duzeniyle
        // girebiliyordu. Mobil klavyeler ilk harfi buyuttugu icin bu cok sik bir durum.
        [Fact]
        public async Task KayitliKullanici_HER_CASING_ile_GIRIS_YAPABILIR()
        {
            if (Skipped()) return;
            var yerel = "iris-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var kucuk = yerel + "@example.com";

            await KayitVeDogrulaAsync(kucuk);

            (await GirisAsync(kucuk)).Should().Be(HttpStatusCode.OK, "kayittaki yazim calismali (vakum kirici)");
            (await GirisAsync(yerel.ToUpperInvariant() + "@EXAMPLE.COM")).Should().Be(HttpStatusCode.OK,
                "TAMAMEN BUYUK yazim da ayni hesaba girmeli");
            (await GirisAsync(char.ToUpperInvariant(yerel[0]) + yerel.Substring(1) + "@example.com"))
                .Should().Be(HttpStatusCode.OK,
                    "ILK HARFI BUYUK yazim da girmeli - mobil klavyelerin varsayilan davranisi budur");
        }

        // ── B2) KUPON KODU HANGI YAZIMLA GIRILIRSE GIRILSIN ESLESIR ──────────────────────
        //
        // OLCULEN UCLU AYRISMA: admin paneli JS toUpperCase ile 'INDIRIM10' gonderiyordu,
        // backend tr-TR ToUpper ile 'İNDİRİM10' ariyordu, storefront kodu HAM gonderiyordu.
        // Sonuc: "i" iceren kupon YALNIZ buyuk harfle calisiyordu.
        [Fact]
        public async Task KuponKodu_HANGI_YAZIMLA_GIRILIRSE_GIRILSIN_ESLESIR()
        {
            if (Skipped()) return;
            var ek = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            var kanonik = "INDIRIM" + ek;

            await using (var ctx = NewContext())
            {
                ctx.Set<Coupon>().Add(new Coupon
                {
                    code = kanonik,
                    discount_type = 0,
                    value = 10m,
                    min_amount = 0m,
                    is_active = true,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            var yazimlar = new[]
            {
                kanonik,                                   // kanonik
                kanonik.ToLowerInvariant(),                // 'indirim...' - eskiden CALISMIYORDU
                "İNDİRİM" + ek                             // tr-TR ToUpper ciktisi - eskiden SAKLANAN degerle carpisiyordu
            };

            using var scope = _factory!.Services.CreateScope();
            var dal = scope.ServiceProvider.GetRequiredService<Divisima.DataAccess.Abstract.ICouponDal>();
            foreach (var yazim in yazimlar)
            {
                var bulunan = await dal.GetByCodeAsync(yazim);
                bulunan.Should().NotBeNull($"'{yazim}' yazimi ayni kupona cozulmeli");
                bulunan!.code.Should().Be(kanonik);
            }

            // CIFT-ANLAM KIRICI: "her sey eslesiyor" olmamali - var olmayan kod bulunmamali.
            (await dal.GetByCodeAsync("YOKBOYLEBIRKOD" + ek)).Should().BeNull(
                "arama gevsemedi, yalnizca KANONIKLESTI");
        }

        // ── B4) CSV ICE AKTARIMDA BOZUK product_type SESSIZCE YUTULMAZ ───────────────────
        [Fact]
        public async Task CsvIceAktarim_BOZUK_ProductType_HATA_LISTESINE_Duser()
        {
            if (Skipped()) return;
            int kategoriId;
            await using (var ctx = NewContext())
            {
                var kat = new Category
                {
                    name = "CSV Kategori",
                    slug = "csv-" + Guid.NewGuid().ToString("N"),
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(kat);
                await ctx.SaveChangesAsync();
                kategoriId = kat.id;
            }

            // Kolonlar: name,brand,category_id,price,sale_price,description,color_hex,product_type,size,qty
            var ad = "CSV Urun " + Guid.NewGuid().ToString("N").Substring(0, 6);
            var csv =
                "name,brand,category_id,price,sale_price,description,color_hex,product_type,size,qty\n" +
                $"{ad},Marka,{kategoriId},100,,aciklama,#123456,ABC,M,5\n";

            using var scope = _factory!.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<Divisima.Bussiness.Abstract.IProductService>();
            var sonuc = await svc.ImportFromCsv(csv);

            // NOT: uc, hata AYRINTILARINI degil yalnizca SAYIYI donduruyor ("... 1 hatali satir").
            // Olculen sey satirin REDDEDILDIGI; ayrintinin donmemesi ayri bir bulgu olarak
            // deftere yazildi (kozmetik).
            var govde = JsonSerializer.Serialize(sonuc.Item2);
            govde.Should().Contain("1 hatali satir",
                $"bozuk product_type satiri HATALI sayilmali - sessizce 0'a dusup ICERI ALINMAMALI. Yanit: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(govde)}");
            govde.Should().Contain("0 urun eklendi", $"hicbir urun eklenmemis olmali. Yanit: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(govde)}");

            await using var son = NewContext();
            (await son.Products.CountAsync(p => p.name == ad)).Should().Be(0,
                "hatali satir ICERI ALINMAMALI");
        }

        // CIFT-ANLAM KIRICI: yukaridaki test "her CSV reddediliyor" ile de yesil kalirdi.
        // GECERLI bir satir ICERI ALINMALI ve product_type dogru okunmalidir.
        [Fact]
        public async Task CsvIceAktarim_GECERLI_ProductType_ICERI_ALINIR()
        {
            if (Skipped()) return;
            int kategoriId;
            await using (var ctx = NewContext())
            {
                var kat = new Category
                {
                    name = "CSV Kategori 2",
                    slug = "csv2-" + Guid.NewGuid().ToString("N"),
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(kat);
                await ctx.SaveChangesAsync();
                kategoriId = kat.id;
            }

            var ad = "CSV Gecerli " + Guid.NewGuid().ToString("N").Substring(0, 6);
            var csv =
                "name,brand,category_id,price,sale_price,description,color_hex,product_type,size,qty\n" +
                $"{ad},Marka,{kategoriId},100,,aciklama,#123456,1,M,5\n";

            using var scope = _factory!.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<Divisima.Bussiness.Abstract.IProductService>();
            await svc.ImportFromCsv(csv);

            await using var son = NewContext();
            var urun = await son.Products.AsNoTracking().FirstOrDefaultAsync(p => p.name == ad);
            urun.Should().NotBeNull("gecerli satir ICERI ALINMALI");
            urun!.product_type.Should().Be(1, "product_type CSV'deki degeri tasimali");
        }
    }
}
