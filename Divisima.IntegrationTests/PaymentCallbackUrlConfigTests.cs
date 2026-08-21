using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Constants;
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
    // E2b - CALLBACK ADRESI COZUMU
    //
    // Olculen engel: Checkout Form init'i BOS callbackUrl ile gidiyordu (storefront callback_url
    // gondermiyor, manager de dto.callback_url ?? bos-dize yaziyordu). Gercek Iyzico bos
    // callbackUrl kabul etmez -> sandbox turu hic baslamiyordu. DTO'dan adres vermek de cozum
    // degildi: UrlValidator.IsSafePublicHttpsUrl yalniz PUBLIC HTTPS kabul eder, dev adresi
    // (http://localhost:5000/...) reddedilir.
    //
    // Karar (kullanici): DTO doluysa MEVCUT davranis aynen (guard dahil); BOS ise operator girdisi
    // olan Iyzico:CallbackUrl kullanilir - config degeri kullanici girdisi olmadigi icin DTO
    // guard'ina TABI DEGILDIR.
    //
    // Bu sinif iddiayi ISTEMCIYE GIDEN ISTEK uzerinden olcer: IIyzicoClient sarmalanir ve
    // IyzicoCheckoutInitRequest.CallbackUrl YAKALANIR. IyzicoClient bu degeri birebir
    // CreateCheckoutFormInitializeRequest.CallbackUrl'e yaziyor (IyzicoClient.cs satir 92) -
    // yani yakalanan deger SDK'ya giden degerdir.
    [Trait("Category", "Sql")]
    public class PaymentCallbackUrlConfigTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCallbackUrlTest";

        // Dev ortaminin GERCEK degeri: http + localhost. Guard bunu REDDEDER - pin bilerek bu
        // degeri kullaniyor ki "config guard'a tabi degil" iddiasi vakumda kalmasin.
        private const string ConfigCallback = "http://localhost:5000/api/payment/callback";
        // DTO'dan gecebilen tek bicim: public HTTPS.
        private const string DtoCallback = "https://odeme.divisima-pin.com/api/payment/callback";

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

        // Init istegini YAKALAYAN istemci. Imza/retrieve gercek (mock) istemciye devredilir -
        // olculen tek sey manager'in istemciye GONDERDIGI callback adresi.
        private sealed class YakalayanIyzicoClient : IIyzicoClient
        {
            private readonly IyzicoClient _real;
            public YakalayanIyzicoClient(IConfiguration config)
                => _real = new IyzicoClient(config, NullLogger<IyzicoClient>.Instance);

            public static string? SonCallbackUrl;
            public static int InitCagriSayisi;

            public Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request)
            {
                SonCallbackUrl = request.CallbackUrl;
                InitCagriSayisi++;
                return _real.InitializeCheckoutFormAsync(request);
            }

            public bool VerifyCallbackSignature(string token, string signature) => _real.VerifyCallbackSignature(token, signature);
            public Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token) => _real.RetrievePaymentResultAsync(token);
            public Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount) => _real.RefundAsync(paymentTransactionId, amount);
        }

        private sealed class CallbackUrlFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Iyzico:UseRealSdk", "false");         // mock mod - gercek SDK cagrilmaz
                builder.UseSetting("Iyzico:CallbackUrl", ConfigCallback); // OPERATOR girdisi (deterministik)
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    // IIyzicoClient Program.cs'te ServiceCollection'a kayitli; buradaki kayit SONRA geldigi icin kazanir.
                    services.AddScoped<IIyzicoClient>(sp =>
                        new YakalayanIyzicoClient(sp.GetRequiredService<IConfiguration>()));
                });
            }
        }

        private CallbackUrlFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            // Statik yakalayici alanlar test sinirini asar - her testte SIFIRLANIR (S7 tuzagi).
            YakalayanIyzicoClient.SonCallbackUrl = null;
            YakalayanIyzicoClient.InitCagriSayisi = 0;
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new CallbackUrlFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak callback adresi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            YakalayanIyzicoClient.SonCallbackUrl = null;
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

        // Her Initialize AYRI siparis ister: ayni siparise ikinci bekleyen odeme Conflict doner.
        private async Task<(int orderId, int customerId)> NewPendingOrderAsync()
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
                    name = "Callback Kategori",
                    slug = $"cb-{Guid.NewGuid():N}",
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "Callback Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 500m,
                    description = "callback testi urunu",
                    color_hex = "#121212",
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
                    stock_quantity = 10,
                    reserved_quantity = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
                customerId = c.id;
                productId = p.id;
            }

            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(
                new OrderCreateRequestDto
                {
                    customer_id = customerId,
                    coupon_code = "",
                    use_store_credit = 0m,
                    payment_method = 0,
                    items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 1 } }
                }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            await using (var ctx2 = NewContext())
            {
                var order = await ctx2.Set<Order>().AsNoTracking().SingleAsync(o => o.customer_id == customerId);
                return (order.id, customerId);
            }
        }

        // PIN (i): DTO BOS + config DOLU -> istemciye giden istek CONFIG adresini tasir.
        // Config degeri http+localhost oldugu icin ayni deger DTO'dan GECEMEZDI: pin hem
        // "config kazanir" hem "config guard'a tabi degil" iddiasini birlikte olcer.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task DtoBOS_ConfigDOLU_IstemciyeGidenIstek_CONFIG_ADRESINI_TASIR()
        {
            if (Skipped()) return;
            var (orderId, customerId) = await NewPendingOrderAsync();

            var init = await WithScopeAsync(sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId }, customerId));

            init.Item1.Should().Be(HttpStatusCode.OK, $"odeme baslatilmali: {init.Item2.Message}");
            init.Item2.Success.Should().BeTrue();

            // VAKUM KIRICI: istemci gercekten cagrildi mi?
            YakalayanIyzicoClient.InitCagriSayisi.Should().Be(1, "checkout form init TAM BIR KEZ cagrilmali");
            // E2b ONCESI DAVRANIS: burasi bos dize idi - gercek Iyzico bos callbackUrl kabul etmez.
            YakalayanIyzicoClient.SonCallbackUrl.Should().Be(ConfigCallback);
            YakalayanIyzicoClient.SonCallbackUrl.Should().NotBeNullOrWhiteSpace();

            // Odeme satiri gercekten olustu (pozitif olay kosulu).
            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().CountAsync(p => p.order_id == orderId))
                .Should().Be(1);
        }

        // PIN (ii): DTO DOLU -> DTO KAZANIR (regresyon).
        // Ikinci bolum cift-anlam kiricidir: guard'a takilan bir DTO degeri 400 ile doner ve
        // config fallback'i onu KURTARMAZ - yani config yolu guard'i devre disi birakmiyor.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task DtoDOLU_DTO_KAZANIR_GecersizDTO_Configle_KURTARILMAZ()
        {
            if (Skipped()) return;

            // (a) Gecerli public HTTPS DTO -> DTO kazanir
            var (orderId, customerId) = await NewPendingOrderAsync();
            var init = await WithScopeAsync(sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId, callback_url = DtoCallback }, customerId));

            init.Item1.Should().Be(HttpStatusCode.OK, $"odeme baslatilmali: {init.Item2.Message}");
            YakalayanIyzicoClient.InitCagriSayisi.Should().Be(1);
            YakalayanIyzicoClient.SonCallbackUrl.Should().Be(DtoCallback);
            YakalayanIyzicoClient.SonCallbackUrl.Should().NotBe(ConfigCallback, "DTO doluyken config KULLANILMAZ");

            // (b) Guard'a takilan DTO -> 400 + istemciye HIC gidilmez + odeme satiri OLUSMAZ
            var (orderId2, customerId2) = await NewPendingOrderAsync();
            var kotu = await WithScopeAsync(sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId2, callback_url = ConfigCallback }, customerId2));

            kotu.Item1.Should().Be(HttpStatusCode.BadRequest);
            kotu.Item2.Message.Should().Be(Messages.PaymentInvalidCallbackUrl,
                "400 baska bir sebepten de gelebilir - govde mesaji guard'i adiyla dogrular");
            YakalayanIyzicoClient.InitCagriSayisi.Should().Be(1, "gecersiz DTO istemciye HIC ulasmamali");

            await using var ctx = NewContext();
            (await ctx.Set<Payment>().AsNoTracking().CountAsync(p => p.order_id == orderId2))
                .Should().Be(0, "gecersiz callback adresinde odeme satiri OLUSMAMALI");
        }
    }
}
