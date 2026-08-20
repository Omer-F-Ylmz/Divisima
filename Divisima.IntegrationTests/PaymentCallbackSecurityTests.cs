using System.Net;
using System.Security.Cryptography;
using System.Text;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Iyzico;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 5 - ODEME CALLBACK GUVENLIK ZINCIRI
    //
    // IyzicoPaymentManager.HandleCallback bir savunma zinciri: imza -> kayit -> idempotency ->
    // token zaman asimi -> dagitik kilit -> kilit sonrasi TEKRAR okuma -> sunucu-sunucu sonuc
    // sorgusu -> tutar/para birimi/fraud dogrulamasi. Halkalarin her biri ayri ayri surulur.
    //
    // MOCK MODU: Iyzico:UseRealSdk=false. Imza dogrulamasi GERCEK algoritma ile calisir
    // (HMAC-SHA256(secretKey, token), timing-safe) - bu yuzden mock'la olcmek anlamli.
    // Sonuc sorgusunu kontrol edebilmek icin IIyzicoClient SARMALANIR: imza ve init GERCEK
    // istemciye devredilir, yalniz RetrievePaymentResultAsync test tarafindan belirlenir.
    // Boylece "tutar uyusmazligi" ve "fraud reddi" dallari deterministik surulebilir.
    [Trait("Category", "Sql")]
    public class PaymentCallbackSecurityTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaPaymentSecTest";
        private const string TestSecretKey = "divisima-test-iyzico-secret-key";
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

        // Sonucu test tarafindan belirlenebilen istemci. Imza + init GERCEK istemciye devredilir -
        // dogrulama zinciri bozulmasin diye.
        private sealed class ControllableIyzicoClient : IIyzicoClient
        {
            private readonly IyzicoClient _real;
            public ControllableIyzicoClient(IConfiguration config)
                => _real = new IyzicoClient(config, NullLogger<IyzicoClient>.Instance);

            // Test bunu doldurmazsa gercek mock sonucu kullanilir (init'te saklanan tutar, fraud=1).
            public static Func<string, IyzicoPaymentResult>? RetrieveOverride { get; set; }
            // Kac cagri sunucu-sunucu sorguya ULASTI - kilit + idempotency halkasinin
            // gercekten kac cagriyi elediginin OLCUMU (tahmin degil).
            public static int RetrieveCallCount;
            public static Func<string, decimal, IyzicoRefundResult>? RefundOverride { get; set; }
            public static int RefundCallCount { get; set; }
            public static decimal RefundedTotal { get; set; }

            public Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request)
                => _real.InitializeCheckoutFormAsync(request);

            // GERCEK algoritma - sahte callback testi bunu surer.
            public bool VerifyCallbackSignature(string token, string signature)
                => _real.VerifyCallbackSignature(token, signature);

            public async Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token)
            {
                System.Threading.Interlocked.Increment(ref RetrieveCallCount);
                return RetrieveOverride != null ? RetrieveOverride(token) : await _real.RetrievePaymentResultAsync(token);
            }

            public Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount)
            {
                RefundCallCount++;
                RefundedTotal += amount;
                return Task.FromResult(RefundOverride != null
                    ? RefundOverride(paymentTransactionId, amount)
                    : new IyzicoRefundResult { Success = true, RefundId = Guid.NewGuid().ToString("N") });
            }
        }

        private sealed class PaymentFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Iyzico:SecretKey", TestSecretKey);
                builder.UseSetting("Iyzico:UseRealSdk", "false");   // mock mod - gercek SDK cagrilmaz
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    // Autofac modulu IIyzicoClient'i kaydediyor; son kayit kazanir.
                    services.AddScoped<IIyzicoClient>(sp =>
                        new ControllableIyzicoClient(sp.GetRequiredService<IConfiguration>()));
                });
            }
        }

        private PaymentFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            ControllableIyzicoClient.RetrieveOverride = null;
            ControllableIyzicoClient.RefundOverride = null;
            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new PaymentFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak odeme guvenlik testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            ControllableIyzicoClient.RetrieveOverride = null;
            ControllableIyzicoClient.RefundOverride = null;
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        // GERCEK imza: HMAC-SHA256(secretKey, token) -> hex kucuk harf (IyzicoClient ile ayni kural).
        private static string Sign(string token)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestSecretKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        private static async Task<(int customerId, int productId)> SeedAsync(int stock = 10)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Odeme Testi", email = $"pay-{Guid.NewGuid():N}@divisima.test", phone = "5550000000",
                password_hash = new byte[] { 1 }, password_salt = new byte[] { 2 },
                is_active = true, email_verified = true, created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            var cat = new Category
            {
                name = "Odeme Kategori", slug = $"pay-{Guid.NewGuid():N}",
                vat_rate = 0.10m, is_active = true, created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Odeme Urun", brand = "T", category_id = cat.id, price = 500m,
                description = "odeme testi urunu", color_hex = "#121212",
                product_type = 0, is_active = true, created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            ctx.ProductStocks.Add(new ProductStock
            {
                product_id = p.id, size = "M", stock_quantity = stock, reserved_quantity = 0,
                is_active = true, created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return (c.id, p.id);
        }

        // Siparis (online odeme) + odeme baslatma -> token.
        private async Task<(int orderId, string token, decimal amount)> NewPendingPaymentAsync(int qty = 2, int stock = 10)
        {
            var (customerId, productId) = await SeedAsync(stock);

            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(
                new OrderCreateRequestDto
                {
                    customer_id = customerId, coupon_code = "", use_store_credit = 0m,
                    payment_method = 0,   // 0 = Online (Iyzico)
                    items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = qty } }
                }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            int orderId;
            await using (var ctx = NewContext())
                orderId = (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.customer_id == customerId)).id;

            var init = await WithScopeAsync(sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId }, customerId));
            init.Item2.Success.Should().BeTrue($"odeme baslatilmali: {init.Item2.Message}");

            await using (var ctx = NewContext())
            {
                var pay = await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId);
                return (orderId, pay.token!, pay.amount);
            }
        }

        private async Task<(HttpStatusCode code, Divisima.Core.Utilities.Results.Result result)> CallbackAsync(
            string token, string? signature) =>
            await WithScopeAsync(sp => sp.GetRequiredService<IPaymentService>()
                .HandleCallback(new PaymentCallbackRequestDto { token = token, signature = signature }));

        private static async Task<Payment> ReadPaymentAsync(int orderId)
        {
            await using var ctx = NewContext();
            return await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId);
        }

        private static async Task<Order> ReadOrderAsync(int orderId)
        {
            await using var ctx = NewContext();
            return await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId);
        }

        private static async Task<(int physical, int reserved)> ReadStockAsync(int productId)
        {
            await using var ctx = NewContext();
            var s = await ctx.Set<ProductStock>().AsNoTracking().SingleAsync(x => x.product_id == productId);
            return (s.stock_quantity, s.reserved_quantity);
        }

        // ── 1) SAHTE IMZA ────────────────────────────────────────────────────────────────
        [Fact]
        public async Task GecersizImza_Reddedilir_OdemeKaydi_DEGISMEZ()
        {
            if (Skipped()) return;
            var (orderId, token, _) = await NewPendingPaymentAsync();
            var oncesi = await ReadPaymentAsync(orderId);

            var r = await CallbackAsync(token, "deadbeef" + new string('0', 56));
            r.code.Should().Be(HttpStatusCode.BadRequest, "sahte imza en bastan reddedilmeli");
            r.result.Success.Should().BeFalse();

            var sonrasi = await ReadPaymentAsync(orderId);
            sonrasi.payment_status.Should().Be(oncesi.payment_status, "odeme durumu DEGISMEMELI");
            sonrasi.paid_at.Should().BeNull("odeme tamamlanmis sayilmamali");
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Pending,
                "siparis durumu degismemeli");
        }

        // Bos imza da reddedilmeli (cift-anlam kirici: yalniz "yanlis imza" degil, imzasizlik da).
        [Fact]
        public async Task ImzaYOKSA_Reddedilir()
        {
            if (Skipped()) return;
            var (orderId, token, _) = await NewPendingPaymentAsync();

            (await CallbackAsync(token, null)).code.Should().Be(HttpStatusCode.BadRequest);
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Pending);
        }

        // ── 2) REPLAY ────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task BasariliCallback_IkinciKez_YanEtki_SIFIR()
        {
            if (Skipped()) return;
            var (orderId, token, _) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            var productId = (await ReadOrderItemProductIdAsync(orderId));

            // POZITIF OLAY: ilk callback gercekten isliyor.
            var ilk = await CallbackAsync(token, Sign(token));
            ilk.result.Success.Should().BeTrue($"ilk callback basarili olmali: {ilk.result.Message}");

            var ilkPayment = await ReadPaymentAsync(orderId);
            var ilkStok = await ReadStockAsync(productId);
            ilkPayment.payment_status.Should().Be((byte)PaymentStatusEnum.Success);
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Confirmed);

            // AYNI callback tekrar - idempotency halkasi.
            var ikinci = await CallbackAsync(token, Sign(token));
            ikinci.code.Should().Be(HttpStatusCode.OK, "replay OK donebilir (idempotent)");

            // YAN ETKI SIFIR: durum, stok ve odeme kaydi ikinci kez DEGISMEDI.
            var ikinciPayment = await ReadPaymentAsync(orderId);
            ikinciPayment.paid_at.Should().Be(ilkPayment.paid_at, "odeme zamani yeniden yazilmamali");
            ikinciPayment.transaction_id.Should().Be(ilkPayment.transaction_id);
            (await ReadStockAsync(productId)).Should().Be(ilkStok, "stok ikinci kez DUSMEMELI");

            await using var ctx = NewContext();
            (await ctx.Set<Invoice>().CountAsync(i => i.order_id == orderId))
                .Should().BeLessThanOrEqualTo(1, "fatura ikinci kez uretilmemeli");
        }

        private static async Task<int> ReadOrderItemProductIdAsync(int orderId)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(i => i.order_id == orderId)).product_id;
        }

        // ── 3) TOKEN ZAMAN ASIMI (30 dk) ─────────────────────────────────────────────────
        [Fact]
        public async Task Token30dkdanEskiyse_Reddedilir_OdemeFailed()
        {
            if (Skipped()) return;
            var (orderId, token, _) = await NewPendingPaymentAsync();

            // Odemeyi 31 dakika oncesine tarihle.
            await using (var ctx = NewContext())
            {
                var p = await ctx.Set<Payment>().SingleAsync(x => x.order_id == orderId);
                p.created_at = DateTime.Now.AddMinutes(-31);
                await ctx.SaveChangesAsync();
            }

            var r = await CallbackAsync(token, Sign(token));
            r.code.Should().Be(HttpStatusCode.BadRequest, "suresi gecmis token reddedilmeli");
            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Failed,
                "suresi gecen odeme Failed isaretlenmeli");
            (await ReadOrderAsync(orderId)).status.Should().NotBe((byte)OrderStatusEnum.Confirmed,
                "siparis onaylanmamali");
        }

        // ── 4) KILIT YARISI ──────────────────────────────────────────────────────────────
        // D5c'nin asli: ayni siparise 8 paralel callback -> TAM BIR isleme.
        [Fact]
        public async Task AyniSiparise_SekizParalelCallback_KILIT_SERILESTIRMIYOR_SADAKAT_CIFTLENIYOR_PINLENIR()
        {
            if (Skipped()) return;
            const int callers = 8;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            var productId = await ReadOrderItemProductIdAsync(orderId);
            var (fizikselOnce, _) = await ReadStockAsync(productId);

            // GERCEK ag gecidi gibi IDEMPOTENT sonuc: ayni token her sorguda AYNI cevabi verir.
            // (Yerlesik mock token'i ilk sorguda tuketiyor; o bir mock artefakti ve olcumu bozardi.)
            ControllableIyzicoClient.RetrieveCallCount = 0;
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-RACE", PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };

            var sonuclar = await Task.WhenAll(Enumerable.Range(0, callers)
                .Select(_ => CallbackAsync(token, Sign(token))));

            var kodlar = string.Join(",", sonuclar.Select(s => (int)s.code));

            // ASIL SINAV: yan etki TEK. Stok bir kez dusmus, odeme basarili, siparis onayli.
            var (fizikselSonra, rezerveSonra) = await ReadStockAsync(productId);
            fizikselSonra.Should().Be(fizikselOnce - 2,
                $"stok yalniz BIR kez dusmeli (8 paralel callback). Kodlar: {kodlar}");
            rezerveSonra.Should().Be(0, "rezervasyon tam bir kez onaylanmali");

            var payment = await ReadPaymentAsync(orderId);
            payment.payment_status.Should().Be((byte)PaymentStatusEnum.Success,
                $"odeme basarili kalmali. Kodlar: {kodlar}");
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Confirmed);

            await using (var ctx = NewContext())
            {
                (await ctx.Set<Invoice>().CountAsync(i => i.order_id == orderId))
                    .Should().BeLessThanOrEqualTo(1, $"tek fatura uretilmeli. Kodlar: {kodlar}");
                (await ctx.Set<Payment>().CountAsync(p => p.order_id == orderId))
                    .Should().Be(1, "tek odeme kaydi olmali");
                // Kupon kullanilmadi ama kayit sayisi da tekil olmali (cift-isleme izi).
                (await ctx.Set<CouponUsage>().CountAsync(u => u.order_id == orderId))
                    .Should().BeLessThanOrEqualTo(1, "kupon kullanimi cift kaydedilmemeli");
            }

            // ── PINLENEN OLCUM (SUPHELI - DUZELTILMEDI, RAPOR EDILDI) ────────────────────
            // Beklenen: kilit + kilit-sonrasi durum kontrolu sayesinde YALNIZ BIR cagri
            // sunucu-sunucu sorguya ulasir. OLCULEN: SEKIZININ SEKIZI de ulasiyor ve hepsi
            // 200 donuyor - yani basari dali sekiz kez calisiyor.
            //
            // Yan etkilerin cogunu ASAGI KATMANLAR soguruyor (yukaridaki assert'ler bunu
            // dogruluyor): rezervasyon bir kez tuketildigi icin stok bir kez dusuyor, odeme
            // kaydi insert degil UPDATE oldugu icin tekil kaliyor, fatura uretimi idempotent.
            // Ama bu bir TASARIM guvencesi degil, TESADUF: sogurmeyen bir yan etki eklendigi
            // gun (or. kupon used_count artisi, sadakat puani, referans odulu) sekiz kez
            // uygulanir. Bu yuzden mevcut davranis acikca pinleniyor.
            ControllableIyzicoClient.RetrieveCallCount.Should().Be(callers,
                $"MEVCUT DAVRANIS: kilit serilestirmiyor, tum cagrilar sorguya ulasiyor. " +
                $"Ulasan: {ControllableIyzicoClient.RetrieveCallCount}, kodlar: {kodlar}");

            // ── SUPHELI: PARA ETKISI GERCEKTEN CIFTLENIYOR ───────────────────────────────
            // Sogurulmayan yan etki bulundu: SADAKAT PUANI her cagri icin ayri veriliyor.
            // Tek siparis, sekiz paralel callback -> SEKIZ loyalty_transactions satiri.
            // Bu bir tahmin degil olcum; asagida MEVCUT DAVRANIS olarak pinleniyor ki
            // duzeltildigi gun test KIRMIZI olsun ve guncellenmesi zorunlu kalsin.
            // DUZELTILMEDI - rapor edildi (kapsam disi: uretim davranisi degistirilmedi).
            await using (var ctx = NewContext())
            {
                (await ctx.Set<LoyaltyTransaction>().CountAsync(t => t.order_id == orderId))
                    .Should().Be(callers,
                        "MEVCUT DAVRANIS: sadakat puani her paralel callback icin AYRI veriliyor " +
                        "(tek siparise sekiz kazanim kaydi). Bu bir PARA etkisi ciftlenmesidir.");
            }
        }

        private static string Messages_PaymentSuccess() => Divisima.Core.Utilities.Constants.Messages.PaymentSuccess;

        // ── 5) TUTAR UYUSMAZLIGI ─────────────────────────────────────────────────────────
        // EKSIK odeme reddedilir. (Sozlesme: PaidPrice >= amount && <= amount * 2)
        [Fact]
        public async Task EksikOdeme_Reddedilir_SiparisIptal_RezervasyonSerbest()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            var productId = await ReadOrderItemProductIdAsync(orderId);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-EKSIK", PaidPrice = amount - 1m,   // 1 TL eksik
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };

            var r = await CallbackAsync(token, Sign(token));
            r.code.Should().Be(HttpStatusCode.BadRequest, "eksik odeme reddedilmeli");

            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Failed);
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Cancelled,
                "eksik odemede siparis iptal edilmeli");
            (await ReadStockAsync(productId)).reserved.Should().Be(0, "rezervasyon serbest birakilmali");
            (await ReadStockAsync(productId)).physical.Should().Be(10, "fiziksel stok hic dusmemeli");
        }

        // FAZLA odeme MAKUL taksit komisyonu kadarsa KABUL (sozlesme pinlenir).
        [Fact]
        public async Task FazlaOdeme_MakulTaksitKomisyonu_KABUL_EdilirVeKomisyonKaydedilir()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync();

            var komisyonlu = amount * 1.1m;   // %10 taksit komisyonu - 2x ust sinirinin altinda
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-TAKSIT", PaidPrice = komisyonlu,
                Currency = "TRY", FraudStatus = "1", Installment = 3
            };

            var r = await CallbackAsync(token, Sign(token));
            r.result.Success.Should().BeTrue($"makul taksit komisyonu kabul edilmeli: {r.result.Message}");

            var payment = await ReadPaymentAsync(orderId);
            payment.payment_status.Should().Be((byte)PaymentStatusEnum.Success);
            payment.paid_price.Should().Be(komisyonlu);
            payment.installment_count.Should().Be(3, "taksit sayisi kaydedilmeli");
            payment.installment_fee.Should().Be(komisyonlu - amount, "komisyon = odenen - beklenen");
        }

        // ── 6) FRAUD REDDI ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task FraudRed_Siparis_Iptal_Rezervasyon_SerbestKalir()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            var productId = await ReadOrderItemProductIdAsync(orderId);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-FRAUD", PaidPrice = amount,
                Currency = "TRY", FraudStatus = "-1", Installment = 1   // fraud RED
            };

            var r = await CallbackAsync(token, Sign(token));
            r.code.Should().Be(HttpStatusCode.BadRequest, "fraud reddi odemeyi gecirmemeli");

            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Failed);
            (await ReadPaymentAsync(orderId)).fraud_status.Should().Be("-1", "fraud skoru kaydedilmeli");
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Cancelled);
            var stok = await ReadStockAsync(productId);
            stok.reserved.Should().Be(0, "rezervasyon serbest birakilmali");
            stok.physical.Should().Be(10, "fiziksel stok dusmemeli");
        }

        // ── 8) KART IADESI ───────────────────────────────────────────────────────────────
        // Basarili karth odeme sonrasi iade: Iyzico'ya kart payi gonderilir ve odeme kaydindaki
        // transaction_id kullanilir (COD/nakit siparislerde bu yol hic calismaz - orada her sey
        // magaza kredisine doner).
        [Fact]
        public async Task KartIadesi_Iyzicoya_DogruTutarla_Gonderilir()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-IADE-1", PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue("odeme basarili olmali");

            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;

            var order = await ReadOrderAsync(orderId);
            var iade = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, 100m, "test iadesi"));

            iade.Success.Should().BeTrue("kart iadesi basarili olmali");
            ControllableIyzicoClient.RefundCallCount.Should().Be(1, "Iyzico iade cagrisi TAM BIR kez yapilmali");
            ControllableIyzicoClient.RefundedTotal.Should().Be(100m, "kart payi dogru tutarla gonderilmeli");
            iade.OnlineRefunded.Should().Be(100m, "siparis tamamen kartla odendi - hepsi karta doner");
            iade.CreditRefunded.Should().Be(0m, "cuzdan payi yok");
        }

        // ── 10) KUMULATIF IADE (SUPHELI - OLCUM) ─────────────────────────────────────────
        // RefundToSourceAsync TEK CAGRI icinde refundAmount > order.total_price ise kirpiyor.
        // Ama KUMULATIF bir sinir YOK: ardisik kismi iadelerin toplami takip edilmiyor.
        // Bu test mevcut davranisi olcup pinler - duzeltme YAPILMADI (refunded_amount kolonu
        // karari icin girdi olacak, bkz. rapor).
        [Fact]
        public async Task KumulatifIade_ToplamTotalPriceI_ASABILIYOR_PINLENIR()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-IADE-2", PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue();

            var order = await ReadOrderAsync(orderId);
            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;

            // TEK cagri kirpiliyor - once bunu dogrula (pozitif olay + kirpmanin calistigi kaniti).
            var asiri = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price + 500m, "asiri iade denemesi"));
            asiri.Success.Should().BeTrue();
            (asiri.OnlineRefunded + asiri.CreditRefunded).Should().Be(order.total_price,
                "TEK cagri siparis toplamina kirpilmali");

            // ARDISIK ikinci tam iade: kumulatif sinir olsaydi bu 0 olurdu.
            var ikinci = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price, "ikinci tam iade"));
            ikinci.Success.Should().BeTrue();

            // MEVCUT DAVRANIS PINLENIR: toplam iade siparis tutarinin IKI KATI oldu.
            var toplamIade = asiri.OnlineRefunded + asiri.CreditRefunded
                           + ikinci.OnlineRefunded + ikinci.CreditRefunded;
            toplamIade.Should().Be(order.total_price * 2m,
                "MEVCUT DAVRANIS: kumulatif iade siniri YOK - ardisik iadeler siparis toplamini asabiliyor");
            ControllableIyzicoClient.RefundedTotal.Should().Be(order.total_price * 2m,
                "Iyzico'ya da toplam tutarin iki kati iade gonderildi");
        }

        // Para birimi uyusmazligi da ayni dala duser (cift-anlam kirici: "her sey fraud degil").
        [Fact]
        public async Task ParaBirimiUyusmazligi_Reddedilir()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync();

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-USD", PaidPrice = amount,
                Currency = "USD", FraudStatus = "1", Installment = 1
            };

            var r = await CallbackAsync(token, Sign(token));
            r.code.Should().Be(HttpStatusCode.BadRequest, "TRY siparise USD odeme kabul edilmemeli");
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Cancelled);
        }
    }
}
