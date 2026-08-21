using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Dtos.Payment;
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
    // E2 - ODEME CALLBACK'I TARAYICIYI HAM JSON'DA BIRAKMAZ
    //
    // /api/payment/callback'i Iyzico MUSTERININ TARAYICISI uzerinden POST eder. Onceden
    // ham JSON donuyordu: musteri odeme sonunda {"success":true} yazan bos bir sayfada
    // kaliyordu. Artik storefront'un sonuc sayfasina 302 ile donuyor.
    //
    // SINIR: HandleCallback'in KENDISI degismedi. Bu sinif yalniz HTTP yanit bicimini
    // pinler; imza/S2S/atomik gecis pinleri PaymentCallbackSecurityTests'te duruyor ve
    // onlar servisi DOGRUDAN cagirdigi icin bu degisiklikten etkilenmedi.
    [Trait("Category", "Sql")]
    public class PaymentCallbackRedirectTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCallbackRedirectTest";
        private const string TestSecretKey = "divisima-test-iyzico-secret-key";
        private const string Storefront = "http://localhost:5173";
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

        // Storefront adresi AYARLANABILIR: bir fabrika ayarli, biri ayarsiz kosuluyor -
        // "yapilandirma yoksa eski davranis" dali da gercekten surulur.
        private sealed class CallbackFactory : WebApplicationFactory<Program>
        {
            private readonly string? _storefront;
            public CallbackFactory(string? storefront) => _storefront = storefront;

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Iyzico:SecretKey", TestSecretKey);
                builder.UseSetting("Iyzico:UseRealSdk", "false");
                builder.UseSetting("Storefront:BaseUrl", _storefront ?? "");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private CallbackFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new CallbackFactory(Storefront);
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak callback yonlendirme testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static string Sign(string token)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestSecretKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        private async Task<T> WithScopeAsync<T>(WebApplicationFactory<Program> f, Func<IServiceProvider, Task<T>> fn)
        {
            using var scope = f.Services.CreateScope();
            return await fn(scope.ServiceProvider);
        }

        // Gercek akis: musteri + urun + stok -> siparis -> odeme baslat -> token.
        private async Task<(int orderId, string token)> NewPendingPaymentAsync(WebApplicationFactory<Program> f, int stock = 10)
        {
            int customerId, productId;
            await using (var ctx = NewContext())
            {
                var c = new Customer
                {
                    name = "Callback Testi",
                    email = $"cb-{Guid.NewGuid():N}@divisima.test",
                    phone = "5550000000",
                    password_hash = new byte[] { 1 },
                    password_salt = new byte[] { 2 },
                    is_active = true,
                    email_verified = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Customer>().Add(c);
                var cat = new Category
                {
                    name = "CB Kategori",
                    slug = $"cb-{Guid.NewGuid():N}",
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "CB Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 300m,
                    description = "callback testi",
                    color_hex = "#123456",
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
                customerId = c.id; productId = p.id;
            }

            var place = await WithScopeAsync(f, sp => sp.GetRequiredService<IOrderService>().PlaceOrder(
                new OrderCreateRequestDto
                {
                    customer_id = customerId,
                    coupon_code = "",
                    use_store_credit = 0m,
                    payment_method = 0,
                    items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 1 } }
                }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            int orderId;
            await using (var ctx = NewContext())
                orderId = (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.customer_id == customerId)).id;

            var init = await WithScopeAsync(f, sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId }, customerId));
            init.Item2.Success.Should().BeTrue($"odeme baslatilmali: {init.Item2.Message}");

            await using (var ctx = NewContext())
            {
                var pay = await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId);
                return (orderId, pay.token!);
            }
        }

        // Yonlendirmeyi IZLEMEYEN istemci: 302'yi goruruz (izlerse 5173'e gercek istek atardi).
        private static HttpClient NoRedirect(WebApplicationFactory<Program> f) =>
            f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static FormUrlEncodedContent Form(string token, string signature) =>
            new(new Dictionary<string, string> { ["token"] = token, ["signature"] = signature });

        // ── 1) BASARILI ODEME -> 302 + order & status=success ────────────────────────────
        [Fact]
        public async Task BasariliCallback_302_ile_SonucSayfasina_Yonlendirir()
        {
            if (Skipped()) return;
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            var resp = await NoRedirect(_factory!).PostAsync("/api/payment/callback", Form(token, Sign(token)));

            resp.StatusCode.Should().Be(HttpStatusCode.Found, "tarayici ham JSON'da BIRAKILMAZ");
            var loc = resp.Headers.Location!.ToString();
            loc.Should().StartWith(Storefront, "storefront adresine donmeli");
            loc.Should().Contain("#/odeme/sonuc", "sonuc sayfasina");
            loc.Should().Contain("order=" + orderId, "siparis id'si parametrede olmali");
            loc.Should().Contain("status=success");

            // YONLENDIRME KOZMETIK DEGIL: odeme gercekten islenmis olmali.
            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId))
                .payment_status.Should().Be((byte)PaymentStatusEnum.Success, "callback isini yapmali");
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId))
                .status.Should().Be((byte)OrderStatusEnum.Confirmed);
        }

        // ── 2) BASARISIZ ODEME -> yine 302, status=failed ────────────────────────────────
        // CIFT-ANLAM KIRICI: 302 "her sey yolunda" demek DEGIL; status parametresi ayirir.
        [Fact]
        public async Task BasarisizCallback_302_status_failed_Doner()
        {
            if (Skipped()) return;
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            // Gecersiz imza -> HandleCallback 400 doner; yonlendirme yine olmali.
            var resp = await NoRedirect(_factory!).PostAsync("/api/payment/callback", Form(token, "00"));

            resp.StatusCode.Should().Be(HttpStatusCode.Found);
            var loc = resp.Headers.Location!.ToString();
            loc.Should().Contain("status=failed", "basarisiz odeme sonuc sayfasinda BASARISIZ gorunmeli");
            loc.Should().Contain("order=" + orderId, "siparis id'si basarisiz dalda da tasinmali");

            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId))
                .payment_status.Should().Be((byte)PaymentStatusEnum.Pending,
                    "gecersiz imza odemeyi ISLEMEMELI - yonlendirme islemi tetiklemez");
        }

        // ── 3) YAPILANDIRMA YOKSA ESKI DAVRANIS (JSON) ──────────────────────────────────
        // Storefront:BaseUrl bos birakilan bir ortamda callback sessizce bozulmamali.
        [Fact]
        public async Task StorefrontAyariYOKSA_EskiDavranis_JSON_Doner()
        {
            if (Skipped()) return;
            await using var plain = new CallbackFactory(null);
            var (orderId, token) = await NewPendingPaymentAsync(plain);

            var resp = await NoRedirect(plain).PostAsync("/api/payment/callback", Form(token, Sign(token)));

            resp.StatusCode.Should().Be(HttpStatusCode.OK, "ayar yoksa JSON donmeli - 302 DEGIL");
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("success", $"Result zarfi donmeli: {body}");

            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId))
                .payment_status.Should().Be((byte)PaymentStatusEnum.Success, "bu dalda da odeme islenmis olmali");
        }

        // ── 4) WEBHOOK DEGISMEDI: sunucu-sunucu yol JSON donmeye DEVAM EDIYOR ───────────
        // Yonlendirme yalniz tarayicinin gordugu callback icin; webhook'u bir tarayici okumaz.
        [Fact]
        public async Task Webhook_JSON_Donmeye_DEVAM_EDER_Yonlendirilmez()
        {
            if (Skipped()) return;
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            var resp = await NoRedirect(_factory!).PostAsJsonAsync("/api/payment/webhook",
                new { token = token, signature = Sign(token) });

            resp.StatusCode.Should().NotBe(HttpStatusCode.Found, "webhook YONLENDIRILMEZ");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resp.Content.ReadAsStringAsync()).Should().Contain("success");

            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId))
                .payment_status.Should().Be((byte)PaymentStatusEnum.Success);
        }

        // ── 5) E2b: GERCEK IYZICO BICIMI - CF callback YALNIZ "token" gonderir ──────────
        //
        // OLCUM (tahmin degil): sandbox turunda tarayici Network > callback > Payload >
        // Form Data icinde TEK alan vardi: "token". "signature" alani YOKTU. Eski kod imzayi
        // kosulsuz zorunlu tuttugu icin GERCEK Iyzico ile her gecerli odeme reddediliyordu -
        // callback 4 ms'de 400 donuyor, retrieve HIC calismiyor, odeme Pending kaliyor, para
        // Iyzico'da kalmis oluyordu. Bu pin o bicimi BIREBIR uretir: govdede sadece token.
        //
        // Otorite imza degil: sunucu-sunucu retrieve + token zaman asimi + tutar/fraud +
        // "yalniz Pending islenebilir". Bu yuzden asagida 302'nin KOZMETIK OLMADIGI da
        // dogrulanir - odeme gercekten Success, siparis gercekten Confirmed.
        [Fact]
        public async Task CFCallback_YALNIZ_TOKEN_ile_ISLENIR_GercekIyzicoBicimi()
        {
            if (Skipped()) return;
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            // signature ALANI HIC YOK - Form(token, imza) degil, tek alanli govde.
            var govde = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
            var resp = await NoRedirect(_factory!).PostAsync("/api/payment/callback", govde);

            resp.StatusCode.Should().Be(HttpStatusCode.Found);
            var loc = resp.Headers.Location!.ToString();
            loc.Should().Contain("status=success", "imzasiz CF callback GECERLIDIR - Iyzico imza gondermiyor");
            loc.Should().Contain("order=" + orderId);

            // VAKUM KIRICI: 302 kozmetik degil, is gercekten yapildi.
            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId))
                .payment_status.Should().Be((byte)PaymentStatusEnum.Success);
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId))
                .status.Should().Be((byte)OrderStatusEnum.Confirmed);
        }

        // ── 6) WEBHOOK YONLENDIRILMEZ (JSON DONER) ─────────────────────────────────────
        //
        // E2'nin sinirini pinler: 302 yalniz TARAYICI yolu icin: webhook'u bir tarayici
        // okumuyor, oraya yonlendirme zarar verirdi.
        //
        // ══ BILINCLI KIRILAN PIN - KAYIT (SPRINT 8 MADDE 9) ════════════════════════════
        // Bu testin adi eskiden Webhook_ImzaSIZ_REDDEDILIR_CF_Gevsemesi_SIZMAZ idi ve
        // "imzasiz webhook 400 alir" iddiasini sabitliyordu. O iddia E2b'de DOGRU bir sey
        // soyluyordu (tarayici yolundaki gevseme sunucu-sunucu yola sizmasin) ama dayandigi
        // VARSAYIM - "webhook'ta imza GELIR" - 22 Agustos 2026'da GERCEK Iyzico bildirimiyle
        // CURUTULDU: govdede "signature" alani yok, baslikta X-Iyz-Signature VAR ama BOS.
        // Yani pin, gercek bildirimi reddeden bir davranisi savunur hale gelmisti (canli
        // zarar: siparis #33 - para alindi, siparis Pending kaldi).
        // Imza asserti KALDIRILDI; yerine WebhookContractTests geldi ve gevsemenin SINIRINI
        // pinliyor (imza GELIRSE hala dogrulanir). Bu testte YALNIZ E2'nin kendi iddiasi
        // (yonlendirme YOK) kaldi - kapsam daraldi, adi bunu soyluyor.
        [Fact]
        public async Task Webhook_YONLENDIRILMEZ_JSON_Doner()
        {
            if (Skipped()) return;
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            var resp = await NoRedirect(_factory!).PostAsJsonAsync("/api/payment/webhook",
                new { token = token });   // signature YOK - gercek bildirimin bicimi

            resp.StatusCode.Should().NotBe(HttpStatusCode.Found, "webhook YONLENDIRILMEZ");
            resp.Headers.Location.Should().BeNull("webhook yanitinda Location basligi olmamali");
            resp.Content.Headers.ContentType!.MediaType.Should().Be("application/json",
                "bant-disi bildirim JSON doner - tarayici bicimine cevrilmez");

            // VAKUM KIRICI: "yonlendirme yok" iddiasi, uc hic calismasa da yesil kalirdi.
            // Imzasiz gercek bildirim retrieve otoritesiyle GERCEKTEN islenmis olmali.
            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId))
                .payment_status.Should().Be((byte)PaymentStatusEnum.Success,
                    "imzasiz gercek bildirim retrieve otoritesiyle islenir (Sprint 8 madde 9)");
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId))
                .status.Should().Be((byte)OrderStatusEnum.Confirmed);
        }
    }
}
