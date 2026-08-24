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
    // SPRINT 8 MADDE 10 - BILDIRIM ABONELIKLERI: LISTELE / KALDIR / JETONLA CIK
    //
    // OLCULEN BOSLUK (E3'te bulundu): backend'de YALNIZ "subscribe" vardi. Tum controller'lar
    // tarandi - abonelikten CIKMA ve "hangi aboneliklerim var" uclari YOKTU. Sonuc: kullanici
    // kurdugu stok/fiyat bildirimini ne gorebiliyor ne kapatabiliyordu. Ticari elektronik ileti
    // icin izin GERI ALINABILIR olmali; bu bir kolaylik meselesi degil.
    //
    // TASARIM KARARI: cikma yolu IKI TURLU.
    //   (a) Giris yapmis kullanici: "aboneliklerim" listesi + kendi satirini silme. Sahiplik
    //       JWT'deki E-POSTA ile dogrulanir (istemci girdisiyle DEGIL) - IDOR engeli.
    //   (b) Anonim abone: e-postadaki baglantida gelen JETON ile. Abonelik uye olmadan
    //       kurulabildigi icin cikma da kimlik dogrulamasi ISTEYEMEZ; jeton tahmin edilemez
    //       oldugu icin sahiplik kanitidir. "E-posta + urun" ile cikma SECILMEDI: herkes
    //       herkesi cikarabilir ve uc "bu e-posta abone mi?" sorusuna yanit veren bir sizinti
    //       kanali olurdu.
    [Trait("Category", "Sql")]
    public class NotificationSubscriptionTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaNotificationSubTest";
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

        private sealed class NotifFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private NotifFactory? _factory;
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
                _factory = new NotifFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak bildirim abonelik testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // Kategori GERCEKTEN olusturulur; urunun description/color_hex alanlari zorunlu.
        private static async Task<int> UrunEkleAsync()
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            await using var ctx = NewContext();
            var kat = new Category
            {
                name = "Bildirim Kategori " + damga,
                slug = "bildirim-kat-" + damga.ToLowerInvariant(),
                display_order = 1,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();

            var u = new Product
            {
                name = "Bildirim Urunu " + damga,
                brand = "Divisima",
                category_id = kat.id,
                price = 250.00m,
                description = "Bildirim pini icin urun.",
                color_hex = "#334455",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(u);
            await ctx.SaveChangesAsync();
            return u.id;
        }

        private static async Task<(int Id, string Token)> StokAboneligiAsync(int productId, string email)
        {
            await using var ctx = NewContext();
            var row = await ctx.Set<StockNotificationRequest>().AsNoTracking()
                .SingleAsync(n => n.product_id == productId && n.email == email);
            return (row.id, row.unsubscribe_token);
        }

        // ── 1) LISTE: YALNIZ KENDI E-POSTASININ ABONELIKLERI GORUNUR ─────────────────
        //
        // CIFT-ANLAM KIRICI: yalniz "kendi satirini goruyor" demiyoruz - BASKASININ satirinin
        // listede BULUNMADIGI da olculuyor. Aksi halde "hepsini donduren" bir uc de gecerdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Aboneliklerim_YALNIZ_KENDI_EPOSTASININ_Aboneliklerini_Doner()
        {
            if (Skipped()) return;

            var urunId = await UrunEkleAsync();
            var ben = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var baskasi = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var anon = _factory!.CreateClient();
            (await anon.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "M", email = ben.Email })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await anon.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "L", email = baskasi.Email })).StatusCode.Should().Be(HttpStatusCode.OK);

            var resp = await ben.Client.GetAsync("/api/StockNotification/my");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var satirlar = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

            satirlar.Should().HaveCount(1, "POZITIF OLAY: kendi aboneligi listede olmali - ve YALNIZ o");
            satirlar[0].GetProperty("type").GetString().Should().Be("stock");
            satirlar[0].GetProperty("size").GetString().Should().Be("M", "baskasinin 'L' satiri DEGIL");
            satirlar[0].GetProperty("product_name").GetString().Should().NotBeNullOrWhiteSpace(
                "urun adi cozulmus olmali - liste ham id gostermemeli");
        }

        // ── 2) IDOR: BASKASININ ABONELIGI SILINEMEZ ──────────────────────────────────
        //
        // 404 KOZMETIK DEGIL: satirin GERCEKTEN durdugu da dogrulanir. Ayrica "403" degil "404"
        // donuyor - "var ama senin degil" demek baskasinin aboneliginin VARLIGINI sizdirirdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task BaskasininAboneligi_SILINEMEZ_404_ve_SATIR_KALIR()
        {
            if (Skipped()) return;

            var urunId = await UrunEkleAsync();
            var sahibi = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var saldirgan = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var anon = _factory!.CreateClient();
            await anon.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "M", email = sahibi.Email });
            var (id, _) = await StokAboneligiAsync(urunId, sahibi.Email);

            var r = await saldirgan.Client.DeleteAsync($"/api/StockNotification/{id}");
            r.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "sahiplik e-postayla dogrulanmali; 'var ama senin degil' demek varligi sizdirirdi");

            await using var ctx = NewContext();
            (await ctx.Set<StockNotificationRequest>().AsNoTracking().AnyAsync(n => n.id == id))
                .Should().BeTrue("reddedilen silme satiri KALDIRMAMALI - 404 kozmetik degil");
        }

        // ── 3) KENDI ABONELIGINI SILEBILIR (VAKUM KIRICI) ────────────────────────────
        [Fact]
        [Trait("Category", "Sql")]
        public async Task KendiAboneligini_SILEBILIR_SATIR_GIDER()
        {
            if (Skipped()) return;

            var urunId = await UrunEkleAsync();
            var ben = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var anon = _factory!.CreateClient();
            await anon.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "M", email = ben.Email });
            var (id, _) = await StokAboneligiAsync(urunId, ben.Email);

            (await ben.Client.DeleteAsync($"/api/StockNotification/{id}")).StatusCode
                .Should().Be(HttpStatusCode.OK, "kendi aboneligini kaldirabilmeli");

            await using var ctx = NewContext();
            (await ctx.Set<StockNotificationRequest>().AsNoTracking().AnyAsync(n => n.id == id))
                .Should().BeFalse("satir GERCEKTEN silinmeli - 200 kozmetik degil");
        }

        // ── 4) JETONLA CIKMA: ANONIM CALISIR, YANLIS JETON CALISMAZ ──────────────────
        //
        // Abonelik uye olmadan kurulabiliyor; cikma yolu da kimlik dogrulamasi ISTEMEMELI.
        // CIFT-ANLAM KIRICI: once yanlis jetonun REDDEDILDIGI, sonra dogru jetonun CALISTIGI
        // olculuyor - biri olmadan "her jeton siler" ya da "hicbir jeton silmez" ayirt edilemezdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task JetonlaCikma_ANONIM_Calisir_YanlisJeton_REDDEDILIR()
        {
            if (Skipped()) return;

            var urunId = await UrunEkleAsync();
            var eposta = $"anonim-{Guid.NewGuid():N}@example.com";

            var anon = _factory!.CreateClient();
            (await anon.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "M", email = eposta })).StatusCode.Should().Be(HttpStatusCode.OK);

            var (id, token) = await StokAboneligiAsync(urunId, eposta);
            token.Should().NotBeNullOrWhiteSpace("abonelik olusurken jeton URETILMELI - POZITIF olay kosulu");
            token.Should().HaveLength(64, "32 bayt hex; URL'de kacis sorunu cikaran karakter icermez");

            (await anon.GetAsync("/api/StockNotification/unsubscribe?token=YANLIS")).StatusCode
                .Should().Be(HttpStatusCode.NotFound, "gecersiz jeton hicbir seyi silmemeli");
            await using (var ara = NewContext())
                (await ara.Set<StockNotificationRequest>().AsNoTracking().AnyAsync(n => n.id == id))
                    .Should().BeTrue("yanlis jeton satiri KALDIRMAMALI");

            (await anon.GetAsync($"/api/StockNotification/unsubscribe?token={Uri.EscapeDataString(token)}")).StatusCode
                .Should().Be(HttpStatusCode.OK, "dogru jeton - KIMLIK DOGRULAMASI OLMADAN calismali");

            await using var son = NewContext();
            (await son.Set<StockNotificationRequest>().AsNoTracking().AnyAsync(n => n.id == id))
                .Should().BeFalse("jetonla cikma satiri GERCEKTEN silmeli");
        }

        // ── 5) FIYAT UYARISI TARAFI DA AYNI SOZLESMEYI TASIR ─────────────────────────
        //
        // Iki abonelik turu AYRI tablolar ve AYRI uclar; birinde calisan sozlesmenin digerinde
        // de calistigi olculmezse yarim bir ozellik yayinlanmis olur.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task FiyatUyarisi_Listelenir_ve_JetonlaCikilir()
        {
            if (Skipped()) return;

            var urunId = await UrunEkleAsync();
            var ben = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var anon = _factory!.CreateClient();
            (await anon.PostAsJsonAsync("/api/price-drop/subscribe",
                new { product_id = urunId, email = ben.Email })).StatusCode.Should().Be(HttpStatusCode.OK);

            var resp = await ben.Client.GetAsync("/api/price-drop/my");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            {
                var satirlar = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
                satirlar.Should().HaveCount(1);
                satirlar[0].GetProperty("type").GetString().Should().Be("price_drop");
                satirlar[0].GetProperty("subscribed_price").GetDecimal().Should().Be(250.00m,
                    "abone olurkenki fiyat tasinmali - kullanici neyi takip ettigini gormeli");
            }

            string token;
            int id;
            await using (var ctx = NewContext())
            {
                var row = await ctx.Set<PriceDropSubscription>().AsNoTracking()
                    .SingleAsync(p => p.product_id == urunId && p.email == ben.Email);
                token = row.unsubscribe_token;
                id = row.id;
            }
            token.Should().NotBeNullOrWhiteSpace();

            (await anon.GetAsync($"/api/price-drop/unsubscribe?token={Uri.EscapeDataString(token)}")).StatusCode
                .Should().Be(HttpStatusCode.OK);

            await using var son = NewContext();
            (await son.Set<PriceDropSubscription>().AsNoTracking().AnyAsync(p => p.id == id))
                .Should().BeFalse();
        }
    }
}
