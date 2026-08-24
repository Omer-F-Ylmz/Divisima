using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Divisima.Bussiness.Abstract;
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
    // E4a - ADMIN STOK + GORSEL EKRANLARININ ARKASINDAKI UCLAR
    //
    // Launch on kosulu: uclar vardi, ekran yoktu. Ekranlar eklenirken uclarin sozlesmesi
    // pinlenir - ozellikle YETKI (rezerve bilgisi ve stok yazma admin isidir) ve YUKLEME
    // SAVUNMALARI (bugun var olan magic-byte + nosniff sessizce kaybolmasin).
    [Trait("Category", "Sql")]
    public class AdminStockAndImageTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaAdminStockTest";
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

        private sealed class AdminFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                // SPRINT 8 MADDE 4 - BU HIZALAMA KALDIRILDI (ve pin haline geldi).
                //
                // Burada eskiden `builder.UseContentRoot(Directory.GetCurrentDirectory())` vardi.
                // Gerekcesi: LocalImageStorage dosyayi CALISMA DIZINI/wwwroot altina yaziyor,
                // UseStaticFiles ise ContentRoot/wwwroot'tan servis ediyordu; testte bu ikisi AYRI
                // dizin oldugu icin (CWD = test bin, ContentRoot = Divisima.API) dosya yaziliyor
                // ama 404 donuyordu. Yani test, URETIMDEKI GERCEK BIR AYRISMAYI kendi ayariyla
                // GIZLIYORDU - ve o ayrisma E2b'de canli ortamda gerceklesti.
                //
                // LocalImageStorage artik `IWebHostEnvironment.WebRootPath`e yaziyor. Hizalama
                // ayari KALDIRILDI: bu sinifin yesil kalmasi, yazma ile sunumun FARKLI calisma
                // dizininde bile ortustugunun KANITIDIR. Ayar geri konursa pin anlamini yitirir.
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private AdminFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                // wwwroot HOST BASLAMADAN ONCE var olmali: UseStaticFiles, WebRootPath yoksa
                // NullFileProvider'a duser ve dizin sonradan olussa bile 404 dondurur.
                // SPRINT 8 MADDE 4: artik CWD degil, gercek content root'un altindaki wwwroot
                // (yani sunumun bakacagi ve LocalImageStorage'in yazacagi TEK dizin) hazirlanir.
                Directory.CreateDirectory(Path.Combine(
                    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Divisima.API")),
                    "wwwroot"));

                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new AdminFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak admin stok/gorsel testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        // ADMIN ISTEMCI: TestAuthHelper yeniden kullanilir (gercek register/verify/login zinciri),
        // sonra o musterinin user_type'i Admin'e cekilip TEKRAR giris yapilir - token yine
        // UYGULAMANIN urettigi gercek token, yalniz tipi Admin. Elde uydurulmus JWT yok.
        private async Task<HttpClient> CreateAdminClientAsync()
        {
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

            var body = await login.Content.ReadFromJsonAsync<LoginEnvelope>();
            var token = body?.data?.token;
            token.Should().NotBeNullOrWhiteSpace("admin token alinmali");

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private sealed class LoginEnvelope { public LoginData? data { get; set; } }
        private sealed class LoginData { public string? token { get; set; } }

        // Urun + tek bedenli stok satiri. Rezerve gerekiyorsa GERCEK rezervasyon servisi kullanilir
        // (satiri elle yazmak rezervasyon modelini yanlis kurar - hareket/rezervasyon kaydi olusmaz).
        private async Task<int> SeedProductAsync(int stock, int reserve = 0)
        {
            int productId;
            await using (var ctx = NewContext())
            {
                var cat = new Category
                {
                    name = "Stok Kategori",
                    slug = $"stok-{Guid.NewGuid():N}",
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "Stok Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 100m,
                    description = "stok testi urunu",
                    color_hex = "#334455",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Products.Add(p);
                await ctx.SaveChangesAsync();

                ctx.ProductStocks.Add(new ProductStock
                {
                    product_id = p.id,
                    size = "M",
                    stock_quantity = stock,
                    reserved_quantity = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
                productId = p.id;
            }

            if (reserve > 0)
            {
                // D-SEMA-FIX: `orderId: 0` bir SENTINEL'di - uretimde rezervasyon HER ZAMAN
                // OrderManager'in az once yazdigi gercek bir siparisin id'siyle acilir.
                // FK_stock_reservations_order_id eklenince kurgu kirildi ve URETIME uyduruldu.
                int siparisId;
                await using (var kur = NewContext())
                    siparisId = await TestVeriKurgusu.GercekSiparisAsync(kur);

                var r = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                    .ReserveStock(productId, "M", reserve, orderId: siparisId));
                r.Item2.Success.Should().BeTrue($"rezervasyon kurulmali: {r.Item2.Message}");
            }
            return productId;
        }

        private static async Task<(int stock, int reserved)> ReadStockAsync(int productId)
        {
            await using var ctx = NewContext();
            var s = await ctx.Set<ProductStock>().AsNoTracking().SingleAsync(x => x.product_id == productId);
            return (s.stock_quantity, s.reserved_quantity);
        }

        // ── 1) YENI ADMIN UCU: uc alan da dogru ──────────────────────────────────────────
        [Fact]
        public async Task StokDetayi_Admin_200_VeUcAlanDaDOGRU()
        {
            if (Skipped()) return;
            var productId = await SeedProductAsync(stock: 10, reserve: 3);
            var admin = await CreateAdminClientAsync();

            var resp = await admin.GetAsync($"/api/Stock/{productId}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                $"admin stok detayini okuyabilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await resp.Content.ReadAsStringAsync())}");

            var payload = await resp.Content.ReadFromJsonAsync<StockDetailEnvelope>();
            var rows = payload?.data;
            rows.Should().NotBeNull().And.HaveCount(1, "tek bedenli urun");

            var row = rows![0];
            row.size.Should().Be("M");
            row.stock_quantity.Should().Be(10, "fiziksel stok");
            row.reserved_quantity.Should().Be(3, "acik rezervasyon");
            // ASIL DEGER: satilabilir, fiziksel DEGIL. Operatorun ekranda gordugu fark bu.
            row.available.Should().Be(7, "satilabilir = fiziksel - rezerve");
        }

        // CIFT-ANLAM KIRICI: 403 "uc bozuk" oldugu icin degil YETKI oldugu icin geliyor;
        // ayni uc admin'e 200 donuyor (ustteki test). Anonim ise 401 - ikisi ayirt edilir.
        [Fact]
        public async Task StokDetayi_MusteriTokeni_403_Anonim_401()
        {
            if (Skipped()) return;
            var productId = await SeedProductAsync(stock: 5);
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            (await musteri.Client.GetAsync($"/api/Stock/{productId}")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden,
                    "rezerve bilgisi admin isi - musteri token'i REDDEDILMELI");

            (await _factory!.CreateClient().GetAsync($"/api/Stock/{productId}")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "kimliksiz istek 401 - 403 DEGIL");
        }

        // ── 2) DUZELTME: stok degisir + hareket kaydi olusur ─────────────────────────────
        [Fact]
        public async Task AdminDuzeltme_StokDegisir_VeHareketKaydi_OLUSUR()
        {
            if (Skipped()) return;
            var productId = await SeedProductAsync(stock: 10, reserve: 2);
            var admin = await CreateAdminClientAsync();

            int hareketOnce;
            await using (var ctx = NewContext())
                hareketOnce = await ctx.Set<StockMovement>().CountAsync(m => m.product_id == productId);

            // Panel FARK aliyor, uc MUTLAK deger istiyor: 10 + 15 = 25.
            var resp = await admin.PostAsJsonAsync("/api/Stock/adjust", new
            {
                product_id = productId,
                size = "M",
                new_quantity = 25,
                note = "Yeni sevkiyat"
            });
            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                $"admin duzeltmesi kabul edilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await resp.Content.ReadAsStringAsync())}");

            var (stok, rezerve) = await ReadStockAsync(productId);
            stok.Should().Be(25, "fiziksel stok yeni mutlak degere gitmeli");
            rezerve.Should().Be(2, "duzeltme rezervasyona DOKUNMAMALI");

            await using (var ctx = NewContext())
            {
                var hareketler = await ctx.Set<StockMovement>().AsNoTracking()
                    .Where(m => m.product_id == productId).ToListAsync();
                hareketler.Count.Should().Be(hareketOnce + 1, "duzeltme TEK hareket kaydi yazmali");

                var son = hareketler.OrderByDescending(m => m.id).First();
                son.movement_type.Should().Be((byte)StockMovementType.Adjustment, "hareket tipi Adjustment");
                son.quantity.Should().Be(15, "hareket miktari FARK olmali (25-10), mutlak deger degil");
                son.size.Should().Be("M");
                son.note.Should().Contain("Yeni sevkiyat", "operatorun sebebi denetim izinde durmali");
            }

            // Duzeltme sonrasi admin ucu tutarli okumali (ekranin tazelemesi dogru veriyi gosterir).
            var detay = await admin.GetAsync($"/api/Stock/{productId}");
            var rows = (await detay.Content.ReadFromJsonAsync<StockDetailEnvelope>())?.data;
            rows![0].available.Should().Be(23, "25 - 2 rezerve");
        }

        [Fact]
        public async Task MusteriTokeni_StokDuzeltmede_403_VeStok_DEGISMEZ()
        {
            if (Skipped()) return;
            var productId = await SeedProductAsync(stock: 10);
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var resp = await musteri.Client.PostAsJsonAsync("/api/Stock/adjust", new
            {
                product_id = productId,
                size = "M",
                new_quantity = 999,
                note = "yetkisiz deneme"
            });
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "stok yazma admin isi");

            // 403 KOZMETIK DEGIL: islem gercekten olmadi.
            (await ReadStockAsync(productId)).stock.Should().Be(10, "yetkisiz istek stogu DEGISTIRMEMELI");
            await using var ctx = NewContext();
            (await ctx.Set<StockMovement>().CountAsync(m => m.product_id == productId))
                .Should().Be(0, "yetkisiz istek hareket kaydi da yazmamali");
        }

        // ── 3) YUKLEME SAVUNMALARI (bugun VAR - pinlenmezse sessizce kaybolabilir) ───────
        // (a) magic-byte: sahte content-type ile gonderilen metin dosyasi REDDEDILIR.
        // (b) nosniff: yuklenen gorselin servis edildigi yanit X-Content-Type-Options tasir.
        [Fact]
        public async Task UploadedImage_NosniffVeMagicByte_PINLENIR()
        {
            if (Skipped()) return;
            var productId = await SeedProductAsync(stock: 1);
            var admin = await CreateAdminClientAsync();

            // (a) SAHTE: icerik duz metin, content-type "image/png" diye YALAN soyluyor.
            var sahte = new MultipartFormDataContent();
            var metin = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"));
            metin.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            sahte.Add(metin, "file", "zararli.png");
            sahte.Add(new StringContent(productId.ToString()), "productId");
            sahte.Add(new StringContent("false"), "isPrimary");

            var sahteResp = await admin.PostAsync("/api/product-image/upload", sahte);
            sahteResp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "magic-byte dogrulamasi sahte content-type'i yakalamali");
            await using (var ctx = NewContext())
                (await ctx.Set<ProductImage>().CountAsync(i => i.product_id == productId))
                    .Should().Be(0, "reddedilen yukleme kayit OLUSTURMAMALI");

            // (b) GERCEK PNG imzali icerik kabul edilir (vakum kirici: (a) yolu her seyi reddetseydi
            // yesil kalirdi). 8 baytlik PNG imzasi + dolgu; manager icerigi decode ETMEZ.
            var png = new byte[64];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
            var gercek = new MultipartFormDataContent();
            var img = new ByteArrayContent(png);
            img.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            gercek.Add(img, "file", "urun.png");
            gercek.Add(new StringContent(productId.ToString()), "productId");
            gercek.Add(new StringContent("true"), "isPrimary");

            var okResp = await admin.PostAsync("/api/product-image/upload", gercek);
            okResp.StatusCode.Should().Be(HttpStatusCode.OK,
                $"gecerli imzali gorsel kabul edilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await okResp.Content.ReadAsStringAsync())}");

            string url;
            await using (var ctx = NewContext())
            {
                var kayit = await ctx.Set<ProductImage>().AsNoTracking()
                    .SingleAsync(i => i.product_id == productId);
                kayit.is_primary.Should().BeTrue("ilk gorsel birincil olmali");
                url = kayit.image_url;
            }

            // ISTEMCI DOSYA ADI KULLANILMIYOR: depolanan ad Guid + dogrulanmis uzanti.
            // "urun.png" adi URL'de GORUNMEMELI (path traversal / uzanti kacirma savunmasi).
            url.Should().NotContain("urun.png", "istemci dosya adi depolamada KULLANILMAMALI");
            url.Should().EndWith(".png", "uzanti dogrulanmis content-type'tan turetilmeli");

            // (b) nosniff - yanit basligi. SecurityHeadersMiddleware global; statik dosya
            // sunumundan ONCE kayitli oldugu icin gorsel yanitini da kapsamali.
            var path = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? new Uri(url).AbsolutePath
                : url;
            var dosya = await admin.GetAsync(path);
            dosya.StatusCode.Should().Be(HttpStatusCode.OK,
                $"yuklenen gorsel statik olarak servis edilmeli: {path}");
            dosya.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue(
                "MIME-sniffing engeli basligi gorsel yanitinda BULUNMALI");
            nosniff!.Should().Contain("nosniff");

            // ── DALGA D / D1: YAZMA YOLU TEST HOST'UNUN WebRoot'UNDAN TURER ────────────────
            // Sprint 8 madde 4'un invarianti (yazma ve sunum ORTUSUR) yukaridaki 200 ile zaten
            // kanitlandi. Burada ayrica dosyanin FIZIKSEL olarak NEREYE dustugu olculur:
            // test host'unun WebRoot'una - depoya ya da calisma dizinine DEGIL.
            var dosyaAdi = Path.GetFileName(path);
            File.Exists(Path.Combine(TestWebRoot.YuklemeDizini, dosyaAdi)).Should().BeTrue(
                "yukleme test host'unun WebRoot'una yazilmali - yazma ve sunum ayni kokten turer");

            // CIFT-ANLAM KIRICI (1): DEPO agacina SIZMAMALI. Olculen sizinti buydu - her kosum
            // Divisima.API/wwwroot/uploads/products altina 64 baytlik sahte PNG birakiyordu.
            var depoKoku = new DirectoryInfo(AppContext.BaseDirectory);
            while (depoKoku != null && !File.Exists(Path.Combine(depoKoku.FullName, "docker-compose.yml")))
                depoKoku = depoKoku.Parent;
            depoKoku.Should().NotBeNull("depo koku bulunmali - sessiz skip YOK");
            File.Exists(Path.Combine(depoKoku!.FullName, "Divisima.API", "wwwroot", "uploads", "products", dosyaAdi))
                .Should().BeFalse("test yuklemesi DEPO agacina yazilmamali (D1 sizintisi)");

            // CIFT-ANLAM KIRICI (2): CALISMA DIZININE de dusmemeli. Sprint 8 madde 4 oncesinde
            // yazma tam da oraya gidiyordu; `UseContentRoot(CWD)` hizalamasi bunu GIZLIYORDU ve
            // o ayar GERI GELMEMELI.
            File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products", dosyaAdi))
                .Should().BeFalse("yukleme CALISMA DIZININE dusmemeli");
        }

        private sealed class StockDetailEnvelope { public List<StockDetailRow>? data { get; set; } }
        private sealed class StockDetailRow
        {
            public string size { get; set; } = "";
            public int stock_quantity { get; set; }
            public int reserved_quantity { get; set; }
            public int available { get; set; }
        }
    }
}
