using System.Net;
using System.Security.Cryptography;
using System.Text;
using Autofac;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.EntityFramework;
using Microsoft.Extensions.Hosting;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Dashboard;
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
    // E2b HARSAT GUNCELLEMESI: RetrieveOverride ile kurulan sahte sonuclar artik
    // ItemTransactionId de tasiyor. Gerekce: gercek Iyzico'da basarili bir odemenin HER ZAMAN
    // bir odeme KIRILIMI (itemTransaction) vardir ve IADE o kimlikle yapilir (paymentId ile
    // DEGIL - E2b'de olculdu). Sahte sonuc bu alani bos birakirsa iade "kimlik yok" dalinda
    // duser ve pin, olcmek istedigi seyi (tutar/clamp) hic olcemez. ASSERT'LER DEGISMEDI -
    // yalniz sahte saglayici gercek sozlesmeye uyduruldu.
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
            // E2b: saglayiciya GONDERILEN kimlik. Iade paymentId ile DEGIL, odeme kiriliminin
            // (itemTransaction) kimligiyle yapilmali - hangisinin gittigi ancak boyle olculur.
            public static string? LastRefundTransactionId { get; set; }

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

            // RefundedTotal = DENENEN toplam; RefundedTotalBasarili = saglayicinin KABUL ettigi toplam.
            // Ikisi ayri olmali: saglayici reddettiginde para GITMEZ, "fazla iade gitti mi" sorusunun
            // dogru cevabi yalniz basarili toplamdir (denenen toplam yaniltir).
            public static decimal RefundedTotalBasarili { get; set; }

            public Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount)
            {
                RefundCallCount++;
                LastRefundTransactionId = paymentTransactionId;
                RefundedTotal += amount;
                var sonuc = RefundOverride != null
                    ? RefundOverride(paymentTransactionId, amount)
                    : new IyzicoRefundResult { Success = true, RefundId = Guid.NewGuid().ToString("N") };
                if (sonuc.Success) RefundedTotalBasarili += amount;
                return Task.FromResult(sonuc);
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
                    // IIyzicoClient Program.cs'te ServiceCollection'a kaydediliyor; buradaki kayit
                    // daha SONRA geldigi icin kazanir.
                    services.AddScoped<IIyzicoClient>(sp =>
                        new ControllableIyzicoClient(sp.GetRequiredService<IConfiguration>()));
                });
            }

            // AUTOFAC KATMANI (S7): asagidaki servisler AutofacBusinessModule'de kayitli.
            // ServiceCollection'a yazmak onlari EZMEZ - AutofacServiceProviderFactory once
            // Populate(services) yapiyor, modul kayitlari SONRA geliyor ve Autofac'te son kayit
            // kazaniyor. ConfigureTestContainer ise minimal hosting'de ATESLENMIYOR (olculdu:
            // sarmalayici yerine gercek uygulama cozuluyordu). Bu yuzden host builder'a MODULDEN
            // SONRA calisacak ek bir ConfigureContainer geri cagrisi ekliyoruz.
            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.ConfigureContainer<ContainerBuilder>(cb =>
                {
                    cb.RegisterType<KontrolluStatusHistory>().As<IOrderStatusHistoryService>().InstancePerLifetimeScope();
                    cb.RegisterType<KontrolluLoyalty>().As<ILoyaltyService>().InstancePerLifetimeScope();
                    cb.RegisterType<KontrolluCreditTxDal>().As<IStoreCreditTransactionDal>().InstancePerLifetimeScope();
                });
                return base.CreateHost(builder);
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
            ControllableIyzicoClient.RefundedTotalBasarili = 0m;
            ControllableIyzicoClient.LastRefundTransactionId = null;
            // S7 HATA ENJEKSIYON BAYRAKLARI - statik olduklari icin test sinirini asarlar.
            // Sifirlanmazsa bir testin enjeksiyonu SONRAKI testleri sessizce bozar (bir kez yasandi:
            // sadakat bayragi acik kaldi ve 8-paralel pini "0 kazanim" ile kirildi).
            KontrolluStatusHistory.RecordPatlasin = false;
            KontrolluLoyalty.EarnPatlasin = false;
            KontrolluCreditTxDal.AddPatlasin = false;
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
        //
        // E2b - KAPSAM DARALDI, AD DEGISTI: CallbackAsync servisi DOGRUDAN cagiriyor ve
        // HandleCallback'in imzaZorunlu varsayilani TRUE. Yani bu pin artik KATI yolu
        // (webhook) olcuyor. Tarayici CF callback yolunda imza ZORUNLU DEGIL - Iyzico orada
        // "signature" alanini HIC gondermiyor (E2b'de olculdu); o yolun pinleri
        // PaymentCallbackRedirectTests icinde, HTTP duzeyinde.
        [Fact]
        public async Task ImzaYOKSA_Reddedilir_KATI_YOL_WEBHOOK()
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
        public async Task AyniSiparise_SekizParalelCallback_TEK_ISLENIR_SADAKAT_TEK_SATIR()
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
                Success = true, PaymentId = "PAY-RACE", ItemTransactionId = "ITX-RACE", ItemTransactionCount = 1, PaidPrice = amount,
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

            // ── SPRINT 6: TEK KAZANAN ────────────────────────────────────────────────────
            // ONCEKI PIN (S5) buraya "sekizinin sekizi de sorguya ulasiyor" yaziyordu ve
            // adinda KILIT_SERILESTIRMIYOR gecerdi. S6 teshisi bunun TESHISI YANLIS oldugunu
            // olctu: kilit MUKEMMEL serilestiriyordu (kritik bolumde ayni anda en fazla 1
            // cagri, sure = 8 x yapay gecikme). Kirik olan sey kilitten SONRAKI "durumu tekrar
            // oku" satiriydi: DAL.GetAsync TRACKED oldugu icin EF identity resolution ayni
            // bayat nesneyi donduruyor, guard hic atesleniyordu. Simdi hem okuma NoTracking
            // hem de Pending->Success gecisi ATOMIK - yan etki hakki TEK cagriya ait.
            ControllableIyzicoClient.RetrieveCallCount.Should().Be(1,
                $"kilit + atomik durum gecisi yalniz BIR cagriyi sunucu-sunucu sorguya birakmali. " +
                $"Ulasan: {ControllableIyzicoClient.RetrieveCallCount}, kodlar: {kodlar}");

            // ── ASIL PIN: PARA ETKISI TEKIL ──────────────────────────────────────────────
            // Sadakat puani sogurulmayan yan etkiydi (S5'te tek siparise SEKIZ satir yaziyordu).
            // Artik TAM 1 satir olmali - hem uygulama katmani (atomik gecis) hem veritabani
            // (filtreli UNIQUE indeks) bunu garanti eder.
            await using (var ctx = NewContext())
            {
                (await ctx.Set<LoyaltyTransaction>()
                    .CountAsync(t => t.order_id == orderId && t.type == (byte)LedgerEntryTypeEnum.Earn))
                    .Should().Be(1,
                        $"sekiz paralel callback -> TAM BIR kazanim satiri. Kodlar: {kodlar}");
            }

            // Puan bakiyesi de tek kazanim kadar artmali (defter ile bakiye mutabik).
            await using (var ctx = NewContext())
            {
                var kazanim = await ctx.Set<LoyaltyTransaction>().AsNoTracking()
                    .SingleAsync(t => t.order_id == orderId && t.type == (byte)LedgerEntryTypeEnum.Earn);
                var musteriId = (await ReadOrderAsync(orderId)).customer_id;
                var musteri = await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == musteriId);
                musteri.loyalty_points.Should().Be(kazanim.points,
                    "bakiye TEK kazanim kadar artmali - defter ile bakiye ayrismamali");
            }
        }

        // CIFT-ANLAM KIRICI: yukaridaki test "retrieve=1" goruyor diye kilit/gecis dogru demek
        // yetmez - sekiz cagrinin hepsi 200 alsa da yalnizca BIRI gercek isi yapmis olmali.
        // Burada ayni siparise SIRAYLA (paralel degil) iki callback gonderilir: ikincisi
        // "zaten islendi" yolundan doner ve sorguya HIC ulasmaz.
        [Fact]
        public async Task ArdisikIkinciCallback_SorguyaULASMAZ_ZatenIslendi_Doner()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync();
            ControllableIyzicoClient.RetrieveCallCount = 0;
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-SIRA", ItemTransactionId = "ITX-SIRA", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };

            var ilk = await CallbackAsync(token, Sign(token));
            ilk.result.Success.Should().BeTrue("ilk callback isi yapmali");
            ControllableIyzicoClient.RetrieveCallCount.Should().Be(1);

            var ikinci = await CallbackAsync(token, Sign(token));
            ikinci.code.Should().Be(HttpStatusCode.OK);
            ControllableIyzicoClient.RetrieveCallCount.Should().Be(1,
                "ikinci callback idempotency guard'inda durmali - sorguya ULASMAMALI");

            await using var ctx = NewContext();
            (await ctx.Set<LoyaltyTransaction>()
                .CountAsync(t => t.order_id == orderId && t.type == (byte)LedgerEntryTypeEnum.Earn))
                .Should().Be(1, "ardisik cagri ikinci kazanim yazmamali");
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
                Success = true, PaymentId = "PAY-EKSIK", ItemTransactionId = "ITX-EKSIK", ItemTransactionCount = 1, PaidPrice = amount - 1m,   // 1 TL eksik
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
                Success = true, PaymentId = "PAY-TAKSIT", ItemTransactionId = "ITX-TAKSIT", ItemTransactionCount = 1, PaidPrice = komisyonlu,
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
                Success = true, PaymentId = "PAY-FRAUD", ItemTransactionId = "ITX-FRAUD", ItemTransactionCount = 1, PaidPrice = amount,
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
                Success = true, PaymentId = "PAY-IADE-1", ItemTransactionId = "ITX-IADE-1", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue("odeme basarili olmali");

            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;
            ControllableIyzicoClient.RefundedTotalBasarili = 0m;
            ControllableIyzicoClient.LastRefundTransactionId = null;

            var order = await ReadOrderAsync(orderId);
            var iade = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, 100m, "test iadesi"));

            iade.Success.Should().BeTrue("kart iadesi basarili olmali");
            ControllableIyzicoClient.RefundCallCount.Should().Be(1, "Iyzico iade cagrisi TAM BIR kez yapilmali");
            ControllableIyzicoClient.RefundedTotal.Should().Be(100m, "kart payi dogru tutarla gonderilmeli");
            iade.OnlineRefunded.Should().Be(100m, "siparis tamamen kartla odendi - hepsi karta doner");
            iade.CreditRefunded.Should().Be(0m, "cuzdan payi yok");
        }

        // ── 10) KUMULATIF IADE SINIRI (SPRINT 6 DUZELTMESI) ──────────────────────────────
        // ONCEKI PIN (S5): "kumulatif sinir YOK" - iki ardisik tam iade siparis tutarinin IKI
        // KATINI geri odeyebiliyordu ve Iyzico'ya da iki kez iade gidiyordu. S6'da
        // orders.refunded_amount eklendi; RefundToSourceAsync kalan hakki ATOMIK rezerve ediyor.
        // Bu test o pini BILINCLI kirar ve yeni sozlesmeyi pinler.
        [Fact]
        public async Task KumulatifIade_ToplamTotalPriceI_ASAMAZ_IYZICOYA_FAZLA_IADE_GITMEZ()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-IADE-2", ItemTransactionId = "ITX-IADE-2", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue();

            var order = await ReadOrderAsync(orderId);
            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;
            ControllableIyzicoClient.RefundedTotalBasarili = 0m;
            ControllableIyzicoClient.LastRefundTransactionId = null;

            // POZITIF OLAY: ilk (asiri) cagri TEK cagri kirpmasiyla tam tutara iniyor ve GERCEKTEN oduyor.
            var asiri = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price + 500m, "asiri iade denemesi"));
            asiri.Success.Should().BeTrue("ilk iade mesru - siparis tutarina kirpilarak odenmeli");
            (asiri.OnlineRefunded + asiri.CreditRefunded).Should().Be(order.total_price,
                "TEK cagri siparis toplamina kirpilmali");

            // ASIL SINAV: ardisik ikinci TAM iade artik REDDEDILIR (kalan hak = 0).
            var ikinci = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price, "ikinci tam iade"));
            ikinci.Success.Should().BeFalse(
                "kumulatif hak tukendi - ikinci tam iade REDDEDILMELI (cagiran rollback eder)");
            (ikinci.OnlineRefunded + ikinci.CreditRefunded).Should().Be(0m, "reddedilen iade para hareketi URETMEZ");

            // CIFT-ANLAM KIRICI: yalniz donen nesneye bakmak yetmez - SAGLAYICIYA giden tutar da olculur.
            ControllableIyzicoClient.RefundedTotalBasarili.Should().Be(order.total_price,
                "Iyzico'ya toplamda siparis tutarindan FAZLA iade GITMEMELI");
            ControllableIyzicoClient.RefundCallCount.Should().Be(1,
                "ikinci iade saglayiciya hic ulasmamali (rezervasyon saglayici cagrisindan ONCE)");

            // Sayac kalici: DB'de kumulatif toplam tam olarak siparis tutari.
            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).refunded_amount
                .Should().Be(order.total_price, "kumulatif sayac kalici ve tam olmali");
        }

        // KISMI IADELER: ucuncu cagri kalan hakka KIRPILIR (reddedilmez) - "hep ya hep yok" degil.
        [Fact]
        public async Task KismiIadeler_UcuncuCagri_KalanHakka_KIRPILIR()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-IADE-3", ItemTransactionId = "ITX-IADE-3", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue();

            var order = await ReadOrderAsync(orderId);
            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;
            ControllableIyzicoClient.RefundedTotalBasarili = 0m;
            ControllableIyzicoClient.LastRefundTransactionId = null;

            var ucte = decimal.Round(order.total_price / 3m, 2);
            var birinci = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, ucte, "kismi-1"));
            var ikinci = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, ucte, "kismi-2"));
            birinci.Success.Should().BeTrue(); ikinci.Success.Should().BeTrue();

            // Ucuncu cagri kalandan COK fazla ister - kirpilarak KABUL edilmeli.
            var ucuncu = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price, "kismi-3-asiri"));
            ucuncu.Success.Should().BeTrue("kalan hak > 0 oldugu icin kirpilarak odenmeli");
            (ucuncu.OnlineRefunded + ucuncu.CreditRefunded).Should().Be(order.total_price - (ucte * 2m),
                "ucuncu iade TAM OLARAK kalan hak kadar olmali");

            ControllableIyzicoClient.RefundedTotalBasarili.Should().Be(order.total_price,
                "uc iadenin saglayici toplami tam olarak siparis tutari");

            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).refunded_amount
                .Should().Be(order.total_price);
        }

        // SAGLAYICI HATASI: para GITMEDIYSE iade hakki BLOKE KALMAZ (rezervasyon geri birakilir).
        // Vakum kirici: sonraki mesru iade GERCEKTEN calisir.
        [Fact]
        public async Task SaglayiciIadesi_BASARISIZSA_IadeHakki_BLOKE_KALMAZ()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-IADE-4", ItemTransactionId = "ITX-IADE-4", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue();

            var order = await ReadOrderAsync(orderId);
            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;
            ControllableIyzicoClient.RefundedTotalBasarili = 0m;
            ControllableIyzicoClient.LastRefundTransactionId = null;

            // 1) Saglayici REDDEDIYOR -> iade basarisiz, sayac ARTMAMALI.
            ControllableIyzicoClient.RefundOverride = (_, __) => new IyzicoRefundResult { Success = false };
            var basarisiz = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price, "saglayici-hatasi"));
            basarisiz.Success.Should().BeFalse("saglayici reddettiyse iade basarisiz olmali");

            await using (var ctx = NewContext())
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).refunded_amount
                    .Should().Be(0m, "para GITMEDI - tahsis geri birakilmali, sayac 0 kalmali");

            // 2) Ayni siparise mesru iade hala YAPILABILMELI (hak bloke degil).
            ControllableIyzicoClient.RefundOverride = null;
            var mesru = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, order.total_price, "yeniden deneme"));
            mesru.Success.Should().BeTrue("hak bloke kalmamali - yeniden deneme calismali");
            ControllableIyzicoClient.RefundCallCount.Should().Be(2, "iki deneme yapildi (biri reddedildi)");
            ControllableIyzicoClient.RefundedTotalBasarili.Should().Be(order.total_price,
                "saglayicinin KABUL ettigi toplam yalniz basarili deneme kadar olmali");

            await using (var ctx = NewContext())
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).refunded_amount
                    .Should().Be(order.total_price);
        }

        // ESZAMANLI IKI TAM IADE: CAS gercekten calisiyor mu (kirpma tek-is-parcacikli bir tesaduf degil).
        [Fact]
        public async Task EszamanliIkiTamIade_ToplamTotalPriceI_ASAMAZ()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-IADE-5", ItemTransactionId = "ITX-IADE-5", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue();

            var order = await ReadOrderAsync(orderId);
            ControllableIyzicoClient.RefundCallCount = 0;
            ControllableIyzicoClient.RefundedTotal = 0m;
            ControllableIyzicoClient.RefundedTotalBasarili = 0m;
            ControllableIyzicoClient.LastRefundTransactionId = null;

            var sonuclar = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                    .RefundToSourceAsync(order, order.total_price, "eszamanli tam iade"))));

            sonuclar.Count(s => s.Success).Should().Be(1, "eszamanli iki tam iadeden yalniz BIRI kazanmali");
            ControllableIyzicoClient.RefundedTotalBasarili.Should().BeLessThanOrEqualTo(order.total_price,
                "saglayiciya toplamda siparis tutarindan fazla iade GITMEMELI");

            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).refunded_amount
                .Should().Be(order.total_price, "kumulatif sayac tam olarak siparis tutarinda durmali");
        }

        // ── SPRINT 7: BASARISIZ ODEMEDE FATURA ───────────────────────────────────────────
        // ONCEKI PIN (S6): "BASARISIZ_ODEMEDE_FATURA_URETILIYOR_PINLENIR" - fraud reddi ile
        // IPTAL edilen siparise de fatura kesiliyor ve saglayiciya Sent olarak gidiyordu.
        // Sebep: ApplyConfirmedSideEffectsAsync dogrulamadan ONCE cagriliyordu ve basarisiz dal
        // iptal yan etkilerini hic tetiklemiyordu. S7'de cagri ONAY dalina tasindi, basarisiz dal
        // ApplyCancelledSideEffectsAsync cagiriyor. O pin BILINCLI kirildi; sozlesme burada.
        [Fact]
        public async Task FraudReddi_FaturaBIRAKMAZ_VeCiroyaGIRMEZ()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync();
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-FRAUD-FATURA", ItemTransactionId = "ITX-FRAUD-FATURA", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "0", Installment = 1
            };

            var r = await CallbackAsync(token, Sign(token));
            r.code.Should().Be(HttpStatusCode.BadRequest, "fraud reddi 400 donmeli");

            await using (var ctx = NewContext())
            {
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).status
                    .Should().Be((byte)OrderStatusEnum.Cancelled, "fraud reddedilen siparis iptal edilmeli");

                // ASIL SOZLESME: ya hic fatura yok, ya da varsa IPTAL edilmis. "Sent" KALMAZ.
                var fatura = await ctx.Set<Invoice>().AsNoTracking()
                    .SingleOrDefaultAsync(i => i.order_id == orderId);
                if (fatura != null)
                    fatura.status.Should().Be((byte)InvoiceStatusEnum.Cancelled,
                        "fatura uretildiyse IPTAL edilmis olmali - saglayicida gecerli fatura KALMAZ");
                else
                    fatura.Should().BeNull("iptal edilen siparise fatura hic kesilmemeli");
            }

            // CIFT-ANLAM KIRICI: fatura tarafi temiz diye ciro tarafi da temiz demek degildir.
            // Gercek rapor servisi surulur - iptal edilen siparis ciroya GIRMEMELI.
            var ozet = await WithScopeAsync(sp => sp.GetRequiredService<IDashboardService>().GetSummary());
            var dto = (ozet.Item2 as SuccessDataResult<DashboardSummaryDto>)!.Data;
            dto.total_revenue.Should().Be(0m, "iptal edilen siparis ciroya girmemeli");
            dto.total_orders.Should().Be(1, "siparis kaydi duruyor (silinmedi) - ciroya sayilmiyor sadece");
        }

        // REGRESYON: duzeltme faturayi OLDURMEDI - basarili odemede fatura AYNEN kesiliyor.
        // (Vakum kirici: yukaridaki test tek basina "fatura hic kesilmiyor" ile de yesil kalirdi.)
        [Fact]
        public async Task BasariliOdeme_FaturaKesilir_Sent_VeCiroyaGirer()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync();
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-FATURA-OK", ItemTransactionId = "ITX-FATURA-OK", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };

            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue("odeme basarili olmali");

            decimal siparisTutari;
            await using (var ctx = NewContext())
            {
                var siparis = await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId);
                siparis.status.Should().Be((byte)OrderStatusEnum.Confirmed);
                siparisTutari = siparis.total_price;

                var fatura = await ctx.Set<Invoice>().AsNoTracking().SingleAsync(i => i.order_id == orderId);
                fatura.status.Should().Be((byte)InvoiceStatusEnum.Sent,
                    "onaylanan siparisin faturasi kesilip saglayiciya gonderilmeli");
            }

            var ozet = await WithScopeAsync(sp => sp.GetRequiredService<IDashboardService>().GetSummary());
            var dto = (ozet.Item2 as SuccessDataResult<DashboardSummaryDto>)!.Data;
            dto.total_revenue.Should().Be(siparisTutari, "onaylanan siparis ciroya girmeli");
        }

        // Para birimi uyusmazligi da ayni dala duser (cift-anlam kirici: "her sey fraud degil").
        [Fact]
        public async Task ParaBirimiUyusmazligi_Reddedilir()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync();

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-USD", ItemTransactionId = "ITX-USD", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "USD", FraudStatus = "1", Installment = 1
            };

            var r = await CallbackAsync(token, Sign(token));
            r.code.Should().Be(HttpStatusCode.BadRequest, "TRY siparise USD odeme kabul edilmemeli");
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Cancelled);
        }

        // ══ SPRINT 7 HATA ENJEKSIYON SARMALAYICILARI ════════════════════════════════════
        // Hepsi GERCEK uygulamadan TUREVdir: bayrak kapaliyken davranis birebir uretim davranisi.
        // Arayuzu yeniden bildirmek (": Gercek, IArayuz") C#'ta arayuz eslemesini yeniden
        // hesaplatir; boylece "new" ile golgelenen uye arayuz uzerinden de cagrilir.

        private sealed class KontrolluStatusHistory : OrderStatusHistoryManager, IOrderStatusHistoryService
        {
            public KontrolluStatusHistory(IOrderStatusHistoryDal historyDal, IOrderDal orderDal)
                : base(historyDal, orderDal) { }

            public static bool RecordPatlasin;

            public new async Task RecordAsync(int orderId, byte status, string note)
            {
                if (RecordPatlasin)
                    throw new InvalidOperationException("TEST: A bolgesinin son adiminda hata");
                await base.RecordAsync(orderId, status, note);
            }
        }

        private sealed class KontrolluLoyalty : LoyaltyManager, ILoyaltyService
        {
            public KontrolluLoyalty(ICustomerDal customerDal, IOrderDal orderDal, ILoyaltyTransactionDal txDal,
                IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork)
                : base(customerDal, orderDal, txDal, creditTxDal, unitOfWork) { }

            public static bool EarnPatlasin;

            public new async Task<(HttpStatusCode, Divisima.Core.Utilities.Results.Result)> EarnFromOrder(
                int customerId, decimal orderTotal, int orderId)
            {
                if (EarnPatlasin)
                    throw new InvalidOperationException("TEST: commit sonrasi sadakat adimi patladi");
                return await base.EarnFromOrder(customerId, orderTotal, orderId);
            }
        }

        private sealed class KontrolluCreditTxDal : EfStoreCreditTransactionDal, IStoreCreditTransactionDal
        {
            public KontrolluCreditTxDal(DivisimaDbContext context) : base(context) { }

            public static bool AddPatlasin;

            public new async Task AddAsync(StoreCreditTransaction entity)
            {
                if (AddPatlasin)
                    throw new InvalidOperationException("TEST: cuzdan defter kaydi patladi");
                await base.AddAsync(entity);
            }
        }

        private static async Task<decimal> ReadStoreCreditAsync(int customerId)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == customerId)).store_credit;
        }

        // Magaza kredisi OLAN musteriyle siparis: kredinin tamami checkout'ta kullanilir, kalan
        // tutar online tahsil edilir. Basarisiz dalin cuzdan iadesi yolunu surmek icin gerekli.
        private async Task<(int orderId, string token, decimal amount, int customerId)>
            NewPendingPaymentWithCreditAsync(int qty, int stock, decimal storeCredit)
        {
            var (customerId, productId) = await SeedAsync(stock);
            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == customerId);
                c.store_credit = storeCredit;
                await ctx.SaveChangesAsync();
            }

            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(
                new OrderCreateRequestDto
                {
                    customer_id = customerId, coupon_code = "", use_store_credit = storeCredit,
                    payment_method = 0,
                    items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = qty } }
                }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            int orderId;
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.customer_id == customerId);
                orderId = o.id;
                o.store_credit_used.Should().Be(storeCredit, "kredi sipariste kullanilmis olmali");
            }

            var init = await WithScopeAsync(sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId }, customerId));
            init.Item2.Success.Should().BeTrue($"odeme baslatilmali: {init.Item2.Message}");

            await using (var ctx2 = NewContext())
            {
                var pay = await ctx2.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId);
                return (orderId, pay.token!, pay.amount, customerId);
            }
        }

        // ══ SPRINT 7 - GERCEK TRANSACTION PINLERI ═══════════════════════════════════════
        // A bolgesi (odeme guncelleme, siparis onayi, stok, kupon kaydi, zaman cizelgesi) artik
        // IUnitOfWork.ExecuteInTransactionAsync ile TEK transaction'da; atomik durum gecisi de
        // ICINDE. B bolgesi (fatura/puan/referans/kupon sayaci) commit sonrasi ve best-effort ama
        // artik SESSIZ degil. Asagidaki dort pin bu sozlesmeyi surer.
        //
        // HATA ENJEKSIYONU: gercek uygulamalardan TUREYEN sarmalayicilar Autofac'e sonradan
        // kaydedilir (son kayit kazanir). Sinifin geri kalani GERCEK kodu kosturur - "her sey
        // sahte" olan bir kurulumda bu testler hicbir sey olcmezdi.

        // (i) A BOLGESININ ORTASINDA hata -> her sey geri alinir, yeniden giris temiz.
        // Enjeksiyon noktasi bilerek A bolgesinin SON adimi (zaman cizelgesi): oncesindeki tum
        // yazmalar (odeme Success, siparis Confirmed, stok dususu) ZATEN yapilmis olur, dolayisiyla
        // rollback'in hepsini geri almasi gerekir. Onceki halde (bos transaction) hicbiri geri
        // alinmazdi - odeme Success kalir, guard yeniden girisi engellerdi: KALICI KISMI DURUM.
        [Fact]
        public async Task ABolgesiOrtasinda_HATA_TAMAMEN_GeriAlinir_YenidenGiris_TEMIZ()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            var productId = await ReadOrderItemProductIdAsync(orderId);
            var (fizikselOnce, rezerveOnce) = await ReadStockAsync(productId);
            rezerveOnce.Should().Be(2, "siparis rezervasyonu kurulmus olmali (pozitif baslangic kosulu)");

            ControllableIyzicoClient.RetrieveCallCount = 0;
            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-TX-1", ItemTransactionId = "ITX-TX-1", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };

            KontrolluStatusHistory.RecordPatlasin = true;
            var hata = await CallbackAsync(token, Sign(token));
            hata.code.Should().Be(HttpStatusCode.InternalServerError, "A bolgesi hatasi 500 donmeli");

            // ROLLBACK: durum gecisi DAHIL hicbir A satiri kalmamali.
            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Pending,
                "atomik durum gecisi transaction ICINDE - rollback onu da geri almali");
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Pending,
                "siparis onayi geri alinmali");
            var (fizikselOrta, rezerveOrta) = await ReadStockAsync(productId);
            fizikselOrta.Should().Be(fizikselOnce, "stok dususu geri alinmali");
            rezerveOrta.Should().Be(rezerveOnce, "rezervasyon Confirmed'e gecmemeli - Active kalmali");
            await using (var ctx = NewContext())
                (await ctx.Set<OrderStatusHistory>().CountAsync(h => h.order_id == orderId
                        && h.status == (byte)OrderStatusEnum.Confirmed))
                    .Should().Be(0, "zaman cizelgesine onay satiri yazilmamali");
            // NOT: bu sipariste kupon YOK (NewPendingPaymentAsync coupon_code=""), bu yuzden
            // CouponUsage sayisi burada bir sey OLCMEZ - vakum assert yazilmadi. Kupon yolunun
            // transaction icinde oldugu kod okunarak degil, kupon senaryosu ayri surulerek
            // dogrulanmali (kapsam disi - bkz. rapor).

            // YENIDEN GIRIS TEMIZ: ayni token, calisan bagimlilikla tekrar gelirse is TAMAMLANIR.
            // (Vakum kirici: yukaridaki "hicbir sey olmadi" iddialari, akis tamamen bozuk olsa da
            //  yesil kalirdi. Burasi akisin GERCEKTEN calistigini kanitliyor.)
            KontrolluStatusHistory.RecordPatlasin = false;
            var ikinci = await CallbackAsync(token, Sign(token));
            ikinci.result.Success.Should().BeTrue($"yeniden giris basarili olmali: {ikinci.result.Message}");

            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Success);
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Confirmed);
            var (fizikselSon, rezerveSon) = await ReadStockAsync(productId);
            fizikselSon.Should().Be(fizikselOnce - 2, "stok TAM BIR kez dusmeli");
            rezerveSon.Should().Be(0, "rezervasyon onaylanmali");
            await using (var ctx = NewContext())
            {
                (await ctx.Set<LoyaltyTransaction>().CountAsync(t => t.order_id == orderId
                        && t.type == (byte)LedgerEntryTypeEnum.Earn))
                    .Should().Be(1, "basarisiz ilk deneme sadakat kazanimini ciftlememeli");
                (await ctx.Set<OrderStatusHistory>().CountAsync(h => h.order_id == orderId
                        && h.status == (byte)OrderStatusEnum.Confirmed))
                    .Should().Be(1, "onay satiri TAM BIR kez yazilmali");
            }
            ControllableIyzicoClient.RetrieveCallCount.Should().Be(2,
                "iki ayri deneme yapildi - ilki rollback'le bittigi icin ikincisi guard'a TAKILMAMALI");
        }

        // (ii) B BOLGESI hatasi -> odeme SUCCESS kalir (para gercekten alindi), hata GORUNUR olur,
        // A bolgesi bozulmaz. Enjeksiyon: sadakat puani (bugun ciplak "catch { }" ile dusuyordu).
        [Fact]
        public async Task BBolgesi_HATASI_OdemeSUCCESS_KALIR_Hata_GORUNUR_ABolgesi_BOZULMAZ()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);
            var productId = await ReadOrderItemProductIdAsync(orderId);
            var (fizikselOnce, _) = await ReadStockAsync(productId);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-TX-2", ItemTransactionId = "ITX-TX-2", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "1", Installment = 1
            };

            KontrolluLoyalty.EarnPatlasin = true;
            var r = await CallbackAsync(token, Sign(token));
            r.result.Success.Should().BeTrue("commit sonrasi yan etki hatasi odemeyi BOZMAZ - para alindi");

            // A bolgesi bozulmadi.
            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Success);
            (await ReadOrderAsync(orderId)).status.Should().Be((byte)OrderStatusEnum.Confirmed);
            (await ReadStockAsync(productId)).physical.Should().Be(fizikselOnce - 2, "stok dususu kalici");

            await using var ctx = NewContext();
            // Patlayan adim GERCEKTEN uygulanmadi (vakum kirici - flag isliyor mu?).
            (await ctx.Set<LoyaltyTransaction>().CountAsync(t => t.order_id == orderId))
                .Should().Be(0, "patlayan adim uygulanmamali");
            // Diger B adimlari etkilenmedi - bir adimin hatasi zinciri KESMEZ.
            (await ctx.Set<Invoice>().CountAsync(i => i.order_id == orderId))
                .Should().Be(1, "fatura adimi patlayan adimdan BAGIMSIZ calismali");

            // ASIL SINAV: hata GORUNUR. Hangi adimin patladigi zaman cizelgesinde ayirt edilebilir.
            var notlar = await ctx.Set<OrderStatusHistory>().AsNoTracking()
                .Where(h => h.order_id == orderId).Select(h => h.note).ToListAsync();
            notlar.Should().Contain(n => n != null && n.Contains("sadakat puani"),
                $"patlayan adim ADIYLA kaydedilmeli - sessiz dusmemeli. Notlar: {string.Join(" | ", notlar)}");
        }

        // (iv) BASARISIZ dalda cuzdan iadesi + defter kaydi ATOMIK.
        // Onceden ambient transaction YOKTU: bakiye artisi (ExecuteUpdate) ve defter kaydi (insert)
        // ayri ayri kaliciliyordu - arada bir hata "bakiye artti ama defterde iz yok" AYRISMASI
        // birakirdi. Simdi ikisi de ayni transaction'da.
        [Fact]
        public async Task BasarisizDal_CuzdanIadesi_VeDefterKaydi_ATOMIK_AyrismaOLMAZ()
        {
            if (Skipped()) return;
            const decimal kredi = 100m;
            var (orderId, token, amount, customerId) =
                await NewPendingPaymentWithCreditAsync(qty: 2, stock: 10, storeCredit: kredi);

            var bakiyeOnce = await ReadStoreCreditAsync(customerId);
            bakiyeOnce.Should().Be(0m, "kredinin tamami sipariste kullanilmis olmali (pozitif baslangic)");

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-TX-3", ItemTransactionId = "ITX-TX-3", ItemTransactionCount = 1, PaidPrice = amount,
                Currency = "TRY", FraudStatus = "0", Installment = 1   // fraud RED -> basarisiz dal
            };

            // 1) Defter kaydi PATLIYOR -> bakiye artisi da geri alinmali.
            KontrolluCreditTxDal.AddPatlasin = true;
            var hata = await CallbackAsync(token, Sign(token));
            hata.code.Should().Be(HttpStatusCode.InternalServerError);

            (await ReadStoreCreditAsync(customerId)).Should().Be(bakiyeOnce,
                "defter kaydi yazilamadiysa BAKIYE de artmamali - ayrisma OLMAZ");
            await using (var ctx = NewContext())
                (await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.order_id == orderId
                        && t.type == (byte)LedgerEntryTypeEnum.Earn))
                    .Should().Be(0, "IADE defter kaydi yok (siparis anindaki HARCAMA satiri ayri - onu saymiyoruz)");
            (await ReadPaymentAsync(orderId)).payment_status.Should().Be((byte)PaymentStatusEnum.Pending,
                "rollback durum gecisini de geri almali");

            // 2) VAKUM KIRICI: calisan defterle ayni akis GERCEKTEN iade yaziyor.
            KontrolluCreditTxDal.AddPatlasin = false;
            var ikinci = await CallbackAsync(token, Sign(token));
            ikinci.code.Should().Be(HttpStatusCode.BadRequest, "fraud reddi 400 donmeli");

            (await ReadStoreCreditAsync(customerId)).Should().Be(bakiyeOnce + kredi,
                "iptal edilen siparisin cuzdan payi iade edilmeli");
            await using (var ctx = NewContext())
                (await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.order_id == orderId
                        && t.type == (byte)LedgerEntryTypeEnum.Earn))
                    .Should().Be(1, "IADE defter kaydi TAM BIR kez - bakiye ile defter mutabik");
        }


        // ── E2b) IADE KIMLIGI: paymentId DEGIL, ODEME KIRILIMI (itemTransaction) ────────
        //
        // OLCUM (gercek Iyzico sandbox): ayni odemede paymentId=37399936 iken
        // itemTransaction paymentTransactionId=39316344 - IKI AYRI KIMLIK. Eskiden refund'a
        // payments.transaction_id (paymentId) gonderiliyordu ve Iyzico
        // "Bu isyerine ait odeme kirilim kaydi bulunamadi" ile REDDEDIYORDU: yani URETIMDE
        // HICBIR KART IADESI CALISMIYORDU. Mock her kimlige Success donduru icin hicbir test
        // bunu goremiyordu (mock da E2b'de sertlestirildi).
        //
        // Bu pin saglayiciya GERCEKTEN GIDEN kimligi olcer. Vakum kirici: iki alan da DOLU ve
        // BIRBIRINDEN FARKLI, yani assert "yanlislikla" gecemez.
        [Fact]
        public async Task Iade_ODEME_KIRILIMI_Kimligiyle_Gonderilir_PaymentId_ILE_DEGIL()
        {
            if (Skipped()) return;
            var (orderId, token, amount) = await NewPendingPaymentAsync(qty: 2, stock: 10);

            ControllableIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = true, PaymentId = "PAY-KIMLIK", ItemTransactionId = "ITX-KIMLIK",
                ItemTransactionCount = 1, PaidPrice = amount, Currency = "TRY",
                FraudStatus = "1", Installment = 1
            };
            (await CallbackAsync(token, Sign(token))).result.Success.Should().BeTrue("odeme basarili olmali");

            // Callback iki kimligi de sakladi mi?
            var odeme = await ReadPaymentAsync(orderId);
            odeme.transaction_id.Should().Be("PAY-KIMLIK");
            odeme.item_transaction_id.Should().Be("ITX-KIMLIK");
            odeme.item_transaction_id.Should().NotBe(odeme.transaction_id,
                "iki kimlik FARKLI olmali - aksi halde bu pin hicbir sey ayirt etmez");

            ControllableIyzicoClient.LastRefundTransactionId = null;
            var order = await ReadOrderAsync(orderId);
            var iade = await WithScopeAsync(sp => sp.GetRequiredService<IRefundService>()
                .RefundToSourceAsync(order, 100m, "kimlik testi"));

            iade.Success.Should().BeTrue($"iade basarili olmali: kimlik dogru gonderilmeli");
            ControllableIyzicoClient.LastRefundTransactionId.Should().Be("ITX-KIMLIK",
                "IADE ODEME KIRILIMI kimligiyle yapilmali");
            ControllableIyzicoClient.LastRefundTransactionId.Should().NotBe("PAY-KIMLIK",
                "paymentId gonderilirse gercek Iyzico 'odeme kirilim kaydi bulunamadi' der");
        }
    }
}
