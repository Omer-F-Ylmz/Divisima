using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.Core.Utilities.Enums;
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
    // ══ DALGA-3-FIX: P1 (CORS preflight onbellegi) + P3 (admin listesi sayfalama) ═════════════
    //
    // BU SINIFTA SURE PINI YOKTUR - kullanicinin kurali: sure pini KIRILGAN, YAPI pini konur.
    // Olculen sey "ne kadar hizli" degil, "yapinin dogru olup olmadigi":
    //   P1 -> preflight yaniti Access-Control-Max-Age TASIR (tarayici artik her cagrida
    //         yeniden preflight yapmak ZORUNDA DEGIL)
    //   P3 -> liste ucu SAYFALI bir zarf doner ve TOPLAM SAYIYI bildirir (kirpilma sessiz kalamaz)
    [Trait("Category", "Sql")]
    public class PreflightAndAdminPagingTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaPreflightPagingTest";
        private const string TestOrigin = "https://storefront.test";
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

        private sealed class PagingFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                // CORS politikasi izin verilen origin listesini yapilandirmadan okuyor; preflight'in
                // GERCEKTEN degerlendirilmesi icin test origin'i buraya konur. Aksi halde yanit
                // "origin izinli degil" dali olur ve Max-Age hic yazilmazdi - test yanlis sebepten
                // kirmizi olurdu.
                builder.UseSetting("AllowedOrigins:0", TestOrigin);
                builder.UseSetting("RateLimit:AuthPermitLimit", "1000");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private PagingFactory? _factory;
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
                _factory = new PagingFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak preflight/sayfalama testleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
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

        // ── P1) PREFLIGHT YANITI Access-Control-Max-Age TASIR ────────────────────────────────
        //
        // OLCULEN ZARAR (Chrome, gercek gezinti): baslik YOKKEN tarayici kendi KISA varsayilanina
        // duser - 24 saniyede 12 kimlikli istek icin 4 OPTIONS. Bir hesap gezintisinde 34 istegin
        // 15'i preflight'ti (trafigin %44'u). Baslik eklendikten sonra AYNI akista 1 OPTIONS.
        [Fact]
        public async Task Preflight_Yaniti_ACCESS_CONTROL_MAX_AGE_Tasir()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();

            var istek = new HttpRequestMessage(HttpMethod.Options, "/api/order/my-orders");
            istek.Headers.Add("Origin", TestOrigin);
            istek.Headers.Add("Access-Control-Request-Method", "GET");
            istek.Headers.Add("Access-Control-Request-Headers", "authorization");

            var yanit = await client.SendAsync(istek);

            // VAKUM KIRICI: once preflight'in GERCEKTEN degerlendirildigi dogrulanir. Origin
            // reddedilseydi Max-Age zaten yazilmazdi ve asagidaki assert "yanlis sebepten" kirmizi
            // olurdu; burada CORS'un izin verdigini de goruyoruz.
            yanit.Headers.TryGetValues("Access-Control-Allow-Origin", out var izinliOrigin)
                .Should().BeTrue("preflight CORS tarafindan DEGERLENDIRILMIS olmali");
            izinliOrigin!.Should().Contain(TestOrigin);

            yanit.Headers.TryGetValues("Access-Control-Max-Age", out var maxAge)
                .Should().BeTrue("preflight yaniti Access-Control-Max-Age TASIMALI - yoksa tarayici " +
                                 "her birkac saniyede bir yeniden preflight yapar (olculdu: 12 istek -> 4 OPTIONS)");

            var saniye = int.Parse(maxAge!.First());
            saniye.Should().BeGreaterThan(0);
            // Deger BURADA sabitlenmiyor (gelecekte ayarlanabilir); sabitlenen sey BASLIGIN VARLIGI
            // ve makul bir pencere olmasi. 10 dakika secildi - gerekcesi Program.cs'te.
            saniye.Should().BeInRange(60, 86400,
                "cok kisa bir deger preflight'i pratikte devre disi birakir; cok uzugu ise CORS " +
                "politikasi degistiginde eski izni gereginden fazla yasatir");
        }

        // ── P3) ADMIN LISTESI SAYFALI BIR ZARF DONER ─────────────────────────────────────────
        private async Task<(HttpClient client, int urunSayisi)> AdminIstemciAsync(int urunSayisi)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            await using (var ctx = NewContext())
            {
                var kategori = new Category
                {
                    name = "Sayfalama " + damga,
                    slug = "sayfalama-" + damga,
                    display_order = 1,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(kategori);
                await ctx.SaveChangesAsync();

                for (int i = 0; i < urunSayisi; i++)
                {
                    ctx.Set<Product>().Add(new Product
                    {
                        name = $"Sayfalama Urun {damga}-{i}",
                        brand = "Divisima",
                        category_id = kategori.id,
                        price = 100m + i,
                        description = "Sayfalama pini icin urun.",
                        color_hex = "#556677",
                        product_type = 0,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                }
                await ctx.SaveChangesAsync();
            }

            // ADMIN ISTEMCI: TestAuthHelper YENIDEN KULLANILIR (gercek register/verify/login
            // zinciri), sonra user_type Admin'e cekilip TEKRAR giris yapilir - token yine
            // UYGULAMANIN urettigi gercek token. Elde uydurulmus JWT yok.
            // (AdminStockAndImageTests'teki kalibin aynisi.)
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == user.CustomerId);
                c.user_type = (byte)UserTypeEnum.Admin;
                await ctx.SaveChangesAsync();
            }

            var anon = _factory!.CreateClient();
            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            login.IsSuccessStatusCode.Should().BeTrue(
                $"admin girisi calismali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await login.Content.ReadAsStringAsync())}");
            using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var token = loginDoc.RootElement.GetProperty("data").GetProperty("token").GetString();
            token.Should().NotBeNullOrWhiteSpace("admin token alinmali");

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return (client, urunSayisi);
        }

        private static async Task<JsonElement> DataAsync(HttpClient c, string yol)
        {
            var r = await c.GetAsync(yol);
            r.StatusCode.Should().Be(HttpStatusCode.OK, $"{yol} 200 donmeli");
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("data").Clone();
        }

        [Fact]
        public async Task AdminListesi_SAYFALI_ZARF_Doner_TOPLAM_SAYIYI_Bildirir()
        {
            if (Skipped()) return;
            var (client, urunSayisi) = await AdminIstemciAsync(7);

            var data = await DataAsync(client, "/api/product/getlist");

            // Zarf sozlesmesi - storefront yolundaki desenin AYNISI.
            data.ValueKind.Should().Be(JsonValueKind.Object,
                "liste ucu artik CIPLAK DIZI degil, sayfalama meta'si tasiyan bir zarf donmeli");
            data.GetProperty("items").GetArrayLength().Should().Be(urunSayisi);
            data.GetProperty("total_count").GetInt32().Should().Be(urunSayisi,
                "TOPLAM SAYI bildirilmeli - yoksa kirpilma SESSIZ olur ve operator '62 urunum vardi, " +
                "100 gorunuyor' durumunu fark edemez");
            data.GetProperty("page").GetInt32().Should().Be(1);
            data.GetProperty("size").GetInt32().Should().Be(100, "parametresiz cagri varsayilan sayfa boyutunu kullanir");
            data.GetProperty("total_pages").GetInt32().Should().Be(1);
        }

        // SAYFA BOYUTU GERCEKTEN ISLIYOR (once ISLEMIYORDU - olculdu: ?page=1&size=1 gonderildi,
        // donen kalem sayisi DEGISMEDI cunku uc parametre KABUL ETMIYORDU).
        [Fact]
        public async Task AdminListesi_SAYFA_PARAMETRELERI_ISLER_ve_TOPLAM_DEGISMEZ()
        {
            if (Skipped()) return;
            var (client, urunSayisi) = await AdminIstemciAsync(7);

            var s1 = await DataAsync(client, "/api/product/getlist?page=1&size=3");
            s1.GetProperty("items").GetArrayLength().Should().Be(3, "istenen sayfa boyutu UYGULANMALI");
            s1.GetProperty("total_count").GetInt32().Should().Be(urunSayisi, "toplam SAYFADAN bagimsizdir");
            s1.GetProperty("total_pages").GetInt32().Should().Be(3, "7 urun / 3 = 3 sayfa");

            var s3 = await DataAsync(client, "/api/product/getlist?page=3&size=3");
            s3.GetProperty("items").GetArrayLength().Should().Be(1, "son sayfada 1 urun kalir");

            // CIFT-ANLAM KIRICI: sayfalar AYNI urunleri dondurmemeli (yoksa "sayfalama var" iddiasi
            // bombos kalirdi - her sayfa ilk N urunu donduren bir uygulama da ustteki assertleri gecerdi).
            var ilkSayfaIdler = s1.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToHashSet();
            var sonSayfaIdler = s3.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToHashSet();
            ilkSayfaIdler.Overlaps(sonSayfaIdler).Should().BeFalse("farkli sayfalar FARKLI urunler dondurmeli");
        }

        // SINIR CLAMP'I - storefront yolundaki korumanin AYNISI: page<=0 -> Skip negatif (patlar),
        // size=0 -> sifira bolme (total_pages), size cok buyuk -> tum tablo (DoS yuzeyi).
        [Theory]
        [InlineData("?page=0&size=0", 1, 100)]
        [InlineData("?page=-5&size=-5", 1, 100)]
        [InlineData("?size=9999", 1, 200)]
        public async Task AdminListesi_SINIR_DEGERLERI_CLAMP_Edilir(string sorgu, int beklenenPage, int beklenenSize)
        {
            if (Skipped()) return;
            var (client, _) = await AdminIstemciAsync(3);

            var data = await DataAsync(client, "/api/product/getlist" + sorgu);
            data.GetProperty("page").GetInt32().Should().Be(beklenenPage);
            data.GetProperty("size").GetInt32().Should().Be(beklenenSize,
                "sinir disi sayfa boyutu clamp'lenmeli - sinirsiz size tum tabloyu tek yanitta " +
                "dondurup DoS yuzeyi acardi (P3'un duzeltmeye calistigi durumun ta kendisi)");
        }

        // YAPI PINI (kullanicinin istedigi bicim): liste ucu KALEM BASINA ek sorgu ATMAZ.
        // Sure olcmez - urun sayisi degistiginde yanitin YAPISI ayni kalir ve zenginlestirme
        // alanlari (kategori adi / bedenler / toplam stok) YINE DOLU gelir. Kalem basina sorgu
        // atan bir uygulama da "dolu" dondurebilir, ama bu pin Dalga 2'de olculen toplu
        // zenginlestirmenin sozlesmesini sabitler: 3 urunle de 30 urunle de AYNI alanlar dolu.
        [Fact]
        public async Task AdminListesi_ZENGINLESTIRME_URUN_SAYISINDAN_BAGIMSIZ_Calisir()
        {
            if (Skipped()) return;
            var (client, _) = await AdminIstemciAsync(30);

            foreach (var size in new[] { 1, 30 })
            {
                var data = await DataAsync(client, $"/api/product/getlist?page=1&size={size}");
                data.GetProperty("items").GetArrayLength().Should().Be(size);
                foreach (var urun in data.GetProperty("items").EnumerateArray())
                {
                    urun.GetProperty("category_name").GetString()
                        .Should().NotBeNullOrWhiteSpace($"size={size} sayfasinda kategori adi DOLU gelmeli");
                }
            }
        }

        // ══ GF-3 / K7 (AV-1: E-6) - DAVRANIS PINI ══════════════════════════════════════════
        //
        // BU SINIFA EKLENDI, YENI SQL SINIFI ACILMADI (10d794d dersi: kendi veritabanini kuran
        // her yeni sinif `model` kilidinde bir katilimci daha olur). Burasi zaten admin
        // istemcisi uretiyor ve TAM bu ucu cagiriyor - K7'nin hedefi `/api/product/getlist`.
        //
        // OLCULEN ONCEKI HAL: `ETagMiddleware` onek listesinde `/api/product` var ve kimlikli
        // ucu AYIRT ETMIYORDU; middleware `SecurityHeadersMiddleware`den DIS halkada oldugu
        // icin onun `no-store` basligini `private, max-age=60` ile EZIYORDU. Yani ADMIN URUN
        // LISTESI paylasilan bir ara onbellege ya da diske dusebilirdi.
        //
        // CIFT ANLAM KIRICI: yalnizca "ETag yok" demek yetmez - ETag hic uretilmemis de
        // olabilirdi. Bu yuzden `Cache-Control`un GERCEKTEN `no-store` oldugu da olculuyor.
        [Fact]
        public async Task GF3_K7_ADMIN_URUN_LISTESI_ONBELLEKLENMEZ_ETag_YOK_ve_no_store_KALIR()
        {
            var (client, _) = await AdminIstemciAsync(3);

            var yanit = await client.GetAsync("/api/product/getlist");

            yanit.StatusCode.Should().Be(HttpStatusCode.OK, "admin listesi acilabilmeli - vakum kirici");
            yanit.Headers.ETag.Should().BeNull(
                "kimlikli uc ETag ALMAMALI: 304 pazarligi yaniti paylasilan bir onbellege tasiyabilir");

            var cacheControl = yanit.Headers.CacheControl?.ToString() ?? "";
            cacheControl.Should().Contain("no-store",
                "SecurityHeaders'in no-store'u ETag dali tarafindan EZILMEMELI");
            cacheControl.Should().NotContain("max-age=60",
                "ETag dalinin gevsetmesi bu uca ULASMAMALI");
        }
    }
}
