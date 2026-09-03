using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Order;
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
    // Aciklayici yorum: Siparis iptalinin PARA yolu. OrderManager 22 bagimlilik aliyor; elle kurmak
    // kirilgan olurdu, bu yuzden uygulamanin GERCEK DI kabi kullaniliyor (WebApplicationFactory) ve
    // IOrderService oradan cozuluyor. HTTP katmani atlanir - olculen sey para/stok tutarliligi.
    // Aciklayici yorum: GERCEK SQL gerektirir - ci.yml adanmis adimi bu trait ile suzuyor.
    [Trait("Category", "Sql")]
    public class OrderCancellationMoneyTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCancelMoneyTest";
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

        private sealed class MoneyFactory : WebApplicationFactory<Program>
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

        private MoneyFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                // DB host kurulmadan ONCE olusturulur: Services erisimi host kurar ve acilis kodu
                // (Hangfire vb.) hemen baglanmaya calisir.
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new MoneyFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak iptal-para testleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch
            {
                _sqlAvailable = false;
            }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try
            {
                await using var ctx = NewContext();
                await TestDbKurulum.SilAsync(ctx.Database);
            }
            catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private async Task<(int customerId, int productId)> SeedAsync(decimal credit, int stock, decimal price)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Iptal Testi",
                email = $"cancel-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                store_credit = credit,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            var cat = new Category { name = "K", slug = $"k{Guid.NewGuid():N}", is_active = true, created_at = DateTime.Now };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Iptal Urun",
                brand = "T",
                category_id = cat.id,
                price = price,
                description = "d",
                color_hex = "#000",
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
            return (c.id, p.id);
        }

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        private async Task<(decimal credit, int physical, int reserved)> ReadStateAsync(int customerId, int productId)
        {
            await using var ctx = NewContext();
            var c = await ctx.Set<Customer>().AsNoTracking().SingleAsync(x => x.id == customerId);
            var s = await ctx.ProductStocks.AsNoTracking().SingleAsync(x => x.product_id == productId && x.size == "M");
            return (c.store_credit, s.stock_quantity, s.reserved_quantity);
        }

        [Fact]
        public async Task TamIptal_CODSiparis_YalnizCuzdanPayiIadeEdilir_StokGeriDoner()
        {
            if (Skipped()) return;
            var (customerId, productId) = await SeedAsync(credit: 100m, stock: 10, price: 50m);

            // 2 x 50 = 100 toplam; 40 cuzdandan, kalan 60 kapida NAKIT (henuz tahsil edilmedi)
            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
            {
                customer_id = customerId,
                coupon_code = "",
                use_store_credit = 40m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 2 } }
            }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            (await ReadStateAsync(customerId, productId)).credit.Should().Be(60m, "cuzdandan 40 dusmeli");

            int orderId;
            await using (var ctx = NewContext())
                orderId = (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.customer_id == customerId)).id;

            var cancel = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                new OrderStatusChangeRequestDto { id = orderId, order_status = OrderStatusEnum.Cancelled }));
            cancel.Item2.Success.Should().BeTrue($"iptal basarili olmali: {cancel.Item2.Message}");

            var after = await ReadStateAsync(customerId, productId);
            after.credit.Should().Be(100m,
                "YALNIZ cuzdan payi (40) iade edilmeli - COD nakdi hic tahsil edilmedigi icin iade EDILMEMELI");
            (after.physical - after.reserved).Should().Be(10, "iptal sonrasi musait stok tamamen geri donmeli");

            await using (var ctx2 = NewContext())
            {
                var o = await ctx2.Set<Order>().AsNoTracking().SingleAsync(x => x.id == orderId);
                o.status.Should().Be((byte)OrderStatusEnum.Cancelled);
                (await ctx2.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == customerId))
                    .Should().BeGreaterThan(0, "para hareketi defterde iz birakmali");
            }
        }

        [Fact]
        public async Task KismiIptal_SonKalemIptali_IadeVeStokTutarli()
        {
            if (Skipped()) return;
            var (customerId, productId) = await SeedAsync(credit: 200m, stock: 10, price: 50m);

            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
            {
                customer_id = customerId,
                coupon_code = "",
                use_store_credit = 200m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 3 } }
            }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            int orderId, itemId;
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.customer_id == customerId);
                orderId = o.id;
                itemId = (await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(i => i.order_id == orderId)).id;
            }
            var beforeCredit = (await ReadStateAsync(customerId, productId)).credit;

            var cancelItem = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .CancelItem(orderId, itemId, customerId));
            cancelItem.Item2.Success.Should().BeTrue($"kalem iptali basarili olmali: {cancelItem.Item2.Message}");

            var after = await ReadStateAsync(customerId, productId);
            after.credit.Should().BeGreaterThan(beforeCredit, "kalem iptali cuzdana iade YAPMALI");
            after.credit.Should().BeLessThanOrEqualTo(200m, "iade odenen tutari ASAMAZ");
            (after.physical - after.reserved).Should().Be(10, "tek kalem iptal edilince stok tamamen geri donmeli");
        }

        // ══ DALGA-2-FIX (B12) - TAM IPTALDE MUHASEBE KIMLIGI KORUNUR ═════════════════════════
        //
        // OLCULEN DURUM (Dalga 2, gercek dev veritabani): tam iptal edilmis YEDI siparis
        //     subtotal=0.00  discount=0.00  shipping=49.90  total=0.00
        // tasiyordu. Kalem iptalleri subtotal'i ve indirimi dusuruyor, tam iptal total'i
        // sifirliyor, ama KARGO kolonu ilk degerinde kaliyordu; yani
        //     total_price = subtotal - discount_amount + shipping_cost
        // kimligi KIRILIYORDU. Musterinin siparis detayi ve fatura govdesi 0,00 TL'lik iptal
        // sipariste "Kargo: 49,90 TL" yaziyordu.
        [Fact]
        public async Task TamIptal_KARGO_BEDELI_de_SIFIRLANIR_MuhasebeKimligi_KORUNUR()
        {
            if (Skipped()) return;
            // 1 x 50 = 50 subtotal -> 2000 esiginin ALTINDA, yani kargo 49.90 UYGULANIR.
            var (customerId, productId) = await SeedAsync(credit: 500m, stock: 10, price: 50m);

            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
            {
                customer_id = customerId,
                coupon_code = "",
                use_store_credit = 0m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 1 } }
            }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            int orderId, itemId;
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.customer_id == customerId);
                orderId = o.id;
                // VAKUM KIRICI: kargo GERCEKTEN uygulanmis olmali - yoksa "iptalden sonra kargo 0"
                // iddiasi bombos kalirdi (hic kargo yoksa zaten 0'dir).
                o.shipping_cost.Should().Be(49.90m, "2000 esiginin altindaki sipariste kargo UYGULANIR");
                o.total_price.Should().Be(99.90m, "50 + 49.90");
                itemId = (await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(i => i.order_id == orderId)).id;
            }

            var cancelItem = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .CancelItem(orderId, itemId, customerId));
            cancelItem.Item2.Success.Should().BeTrue($"kalem iptali basarili olmali: {cancelItem.Item2.Message}");

            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.id == orderId);
                o.status.Should().Be((byte)OrderStatusEnum.Cancelled, "son kalem iptal edilince siparis tumuyle iptal olur");
                o.shipping_cost.Should().Be(0m,
                    "TAM IPTALDE kargo bedeli de sifirlanmali - aksi halde 0,00 TL'lik iptal sipariste " +
                    "musteriye 'Kargo: 49,90 TL' gosterilir");
                (o.subtotal - o.discount_amount + o.shipping_cost).Should().Be(o.total_price,
                    "MUHASEBE KIMLIGI: total_price = subtotal - indirim + kargo. Yedi canli satirda " +
                    "bu kimlik KIRIKTI (0 <> 0 - 0 + 49.90)");
            }
        }

        // ── PARA YOLU DEGISMEDI (B12'nin cift-anlam kiricisi) ────────────────────────────────
        //
        // B12 yalniz DEFTERLENEN kolonu duzeltir. Kargonun MUSTERIYE IADESI (H44'te eklenen
        // "Tam iptal - kalan (kargo) iadesi") aynen surmeli. Bu pin olmadan, "kargoyu sifirla"
        // duzeltmesi yanlislikla iade tutarinin ONUNE gecse (kargo iade edilmese) fark edilmezdi.
        [Fact]
        public async Task TamIptal_ODENMIS_SIPARISTE_KARGO_IADESI_DEGISMEDI()
        {
            if (Skipped()) return;
            var (customerId, productId) = await SeedAsync(credit: 500m, stock: 10, price: 50m);

            // Cuzdanla TAM odeme: 50 + 49.90 = 99.90 -> odenmis siparis; iptalde TAMAMI geri gelmeli.
            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
            {
                customer_id = customerId,
                coupon_code = "",
                use_store_credit = 99.90m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 1 } }
            }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            (await ReadStateAsync(customerId, productId)).credit.Should().Be(400.10m,
                "500 - 99.90 (kargo DAHIL tam odeme cuzdandan dusmeli)");

            int orderId, itemId;
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.customer_id == customerId);
                orderId = o.id;
                itemId = (await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(i => i.order_id == orderId)).id;
            }

            var cancelItem = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .CancelItem(orderId, itemId, customerId));
            cancelItem.Item2.Success.Should().BeTrue($"kalem iptali basarili olmali: {cancelItem.Item2.Message}");

            (await ReadStateAsync(customerId, productId)).credit.Should().Be(500m,
                "KARGO DAHIL odenen TUM tutar iade edilmeli - B12 yalnizca defterlenen kargo " +
                "kolonunu sifirlar, PARA YOLUNA DOKUNMAZ (kargo sifirlamasi iade hesabindan SONRA yapilir)");
        }
    }
}
