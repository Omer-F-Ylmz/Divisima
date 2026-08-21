using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.ProductReview;
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
    // SPRINT 1 - YORUM MODERASYONU + AGREGASYON + DOGRULANMIS ALICI
    //
    // Bu testler ancak Add duzeltildikten SONRA yazilabildi: eskiden her yorum ekleme
    // AutoMapperMappingException ile dusuyordu, dolayisiyla Approve/Reject yollarina hic
    // yorum ulasmiyordu. Olculen sey:
    //   - RecalculateProductRatingAsync: Product.review_count / average_rating yalnizca
    //     ONAYLI yorumlardan hesaplaniyor mu, ret sonrasi geri dusuyor mu,
    //   - HasPurchasedAsync: IPTAL EDILMIS kalem "dogrulanmis alici" saymiyor mu (H48 somurusu).
    [Trait("Category", "Sql")]
    public class ProductReviewModerationTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaReviewModerationTest";
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

        private sealed class ModerationFactory : WebApplicationFactory<Program>
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

        private ModerationFactory? _factory;
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
                _factory = new ModerationFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak yorum moderasyonu testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        private static async Task<int> NewCustomerAsync()
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Moderasyon Testi",
                email = $"mod-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            await ctx.SaveChangesAsync();
            return c.id;
        }

        private static async Task<int> NewProductAsync()
        {
            await using var ctx = NewContext();
            var cat = new Category
            {
                name = "Mod Kategori",
                slug = $"mod-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Mod Urun",
                brand = "T",
                category_id = cat.id,
                price = 100m,
                description = "moderasyon testi urunu",
                color_hex = "#0B0B0B",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();
            return p.id;
        }

        private async Task<int> AddReviewAsync(int customerId, int productId, int rating)
        {
            var r = await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>()
                .Add(new ProductReviewAddRequestDto
                {
                    customer_id = customerId,
                    product_id = productId,
                    rating = rating,
                    comment = "Urun beklentimi karsiladi."
                }));
            r.Item2.Success.Should().BeTrue($"yorum eklenebilmeli: {r.Item2.Message}");

            await using var ctx = NewContext();
            return (await ctx.Set<ProductReview>().AsNoTracking()
                .SingleAsync(x => x.customer_id == customerId && x.product_id == productId)).id;
        }

        private static async Task<(int count, decimal avg)> ReadAggregateAsync(int productId)
        {
            await using var ctx = NewContext();
            var p = await ctx.Products.AsNoTracking().SingleAsync(x => x.id == productId);
            return (p.review_count, p.average_rating);
        }

        [Fact]
        public async Task Approve_OnayliYorumSayisi_VeOrtalamaPuani_Gunceller()
        {
            if (Skipped()) return;
            var productId = await NewProductAsync();
            var r1 = await AddReviewAsync(await NewCustomerAsync(), productId, 5);
            var r2 = await AddReviewAsync(await NewCustomerAsync(), productId, 3);

            // VAKUM KIRICI: onaydan ONCE agregat sifir - yorumlar Pending geldigi icin sayilmaz.
            var before = await ReadAggregateAsync(productId);
            before.count.Should().Be(0, "onaylanmamis yorum agregata girmemeli");
            before.avg.Should().Be(0m);

            (await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Approve(r1)))
                .Item2.Success.Should().BeTrue();
            (await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Approve(r2)))
                .Item2.Success.Should().BeTrue();

            var after = await ReadAggregateAsync(productId);
            after.count.Should().Be(2, "iki onayli yorum sayilmali");
            after.avg.Should().Be(4.00m, "(5 + 3) / 2 = 4.00");
        }

        [Fact]
        public async Task Reject_OnceOnaylananYorum_OrtalamadanDUSER()
        {
            if (Skipped()) return;
            var productId = await NewProductAsync();
            var r1 = await AddReviewAsync(await NewCustomerAsync(), productId, 5);
            var r2 = await AddReviewAsync(await NewCustomerAsync(), productId, 3);

            await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Approve(r1));
            await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Approve(r2));

            // VAKUM KIRICI: reddetmeden onceki deger gercekten olculur.
            var before = await ReadAggregateAsync(productId);
            before.count.Should().Be(2);
            before.avg.Should().Be(4.00m);

            (await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Reject(r2)))
                .Item2.Success.Should().BeTrue();

            var after = await ReadAggregateAsync(productId);
            after.count.Should().Be(1, "reddedilen yorum agregattan cikmali");
            after.avg.Should().Be(5.00m, "geriye yalniz 5 yildizli yorum kalir");
        }

        // CIFT-ANLAM KIRICI: Reject her durumda agregati degistiriyor gibi gorunmesin.
        // Zaten onaylanmamis bir yorumun reddi agregata DOKUNMAMALI.
        [Fact]
        public async Task Reject_HicOnaylanmamisYorum_AgregatiDEGISTIRMEZ()
        {
            if (Skipped()) return;
            var productId = await NewProductAsync();
            var onayli = await AddReviewAsync(await NewCustomerAsync(), productId, 5);
            var bekleyen = await AddReviewAsync(await NewCustomerAsync(), productId, 1);

            await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Approve(onayli));
            var before = await ReadAggregateAsync(productId);
            before.count.Should().Be(1);
            before.avg.Should().Be(5.00m);

            (await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>().Reject(bekleyen)))
                .Item2.Success.Should().BeTrue();

            var after = await ReadAggregateAsync(productId);
            after.count.Should().Be(1, "zaten onaysiz yorumun reddi sayiyi degistirmemeli");
            after.avg.Should().Be(5.00m, "ortalama da degismemeli");
        }

        // H48 SOMURUSU: tek siparise cok urun koy -> hepsini iptal edip parasini geri al ->
        // kalan tek urun teslim edilince siparis Delivered olur -> IPTAL EDILEN urunlere de
        // "dogrulanmis alici" rozetiyle yorum yazilabilirdi. Kalem bayragi da bakilmali.
        [Fact]
        public async Task DogrulanmisAlici_IptalEdilenKalem_SAYILMAZ()
        {
            if (Skipped()) return;
            var customerId = await NewCustomerAsync();
            var iptalEdilen = await NewProductAsync();
            var teslimEdilen = await NewProductAsync();

            await using (var ctx = NewContext())
            {
                var o = new Order
                {
                    customer_id = customerId,
                    order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                    status = (byte)OrderStatusEnum.Delivered,
                    subtotal = 200m,
                    total_price = 200m,
                    store_credit_used = 0m,
                    is_online_payment_done = true,
                    currency = "TRY",
                    created_at = DateTime.Now,
                    delivered_at = DateTime.Now
                };
                ctx.Set<Order>().Add(o);
                await ctx.SaveChangesAsync();

                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = o.id,
                    product_id = iptalEdilen,
                    size = "M",
                    quantity = 1,
                    unit_price = 100m,
                    is_cancelled = true,
                    created_at = DateTime.Now
                });
                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = o.id,
                    product_id = teslimEdilen,
                    size = "M",
                    quantity = 1,
                    unit_price = 100m,
                    is_cancelled = false,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            await AddReviewAsync(customerId, iptalEdilen, 1);
            await AddReviewAsync(customerId, teslimEdilen, 5);

            await using var son = NewContext();
            (await son.Set<ProductReview>().AsNoTracking()
                .SingleAsync(r => r.customer_id == customerId && r.product_id == iptalEdilen))
                .is_verified_purchase.Should().BeFalse("iptal edilmis kalem satin alma SAYILMAZ");

            // POZITIF OLAY: ayni siparisin iptal EDILMEMIS kalemi rozeti hak eder - yani kural
            // "hicbir seye rozet verme" degil, dogru ayrimi yapiyor.
            (await son.Set<ProductReview>().AsNoTracking()
                .SingleAsync(r => r.customer_id == customerId && r.product_id == teslimEdilen))
                .is_verified_purchase.Should().BeTrue("teslim edilen kalem dogrulanmis alici yapar");
        }
    }
}
