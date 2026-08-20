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
    // Aciklayici yorum: D3 KALBI - kuponun SON HAKKINA eszamanli yaris. OrderManager check-then-act
    // yapiyor ve bunu coupon:{code} dagitik kilidiyle koruyor. Kilit implementasyonu Redis:Enabled
    // bayragina bagli; TEST ortaminda Redis kapali oldugu icin InMemoryDistributedLock devrede
    // (Program.cs else dali) - tek process icinde gercek serilestirme saglar.
    // BEKLENEN: limit asilinca siparis yine BASARILI olur ama kupon UYGULANMAZ (couponValid=false),
    // yani 8 siparisin hepsi gecer, coupon_code YALNIZ birinde dolu olur.
    [Trait("Category", "Sql")]
    public class CouponRaceTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCouponRaceTest";
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

        private sealed class RaceFactory : WebApplicationFactory<Program>
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

        private RaceFactory? _factory;
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
                _factory = new RaceFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException("DIVISIMA_TEST_SQL verildi ancak kupon yaris testi ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        [Fact]
        public async Task SonHakkaSekizParalelIstek_KuponYALNIZ_BIRINDE_Uygulanir()
        {
            if (Skipped()) return;
            const int callers = 8;

            var productIds = new List<int>();
            string couponCode;
            var customerIds = new List<int>();

            await using (var ctx = NewContext())
            {
                var cat = new Category { name = "K", slug = $"k{Guid.NewGuid():N}", is_active = true, created_at = DateTime.Now };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                // HER cagriya AYRI urun: ayni ProductStock satirina eszamanli yazma, ReserveStock
                // optimistic concurrency retry limitini asip Conflict uretiyor ve KUPON yarisini
                // maskeliyordu (bkz. rapor). Ayri urunle olculen sey yalnizca kupon kilidi olur.
                for (int i = 0; i < callers; i++)
                {
                    var p = new Product
                    {
                        name = "Yaris Urun", brand = "T", category_id = cat.id, price = 100m,
                        description = "d", color_hex = "#000", product_type = 0, is_active = true, created_at = DateTime.Now
                    };
                    ctx.Products.Add(p);
                    await ctx.SaveChangesAsync();
                    productIds.Add(p.id);
                    ctx.ProductStocks.Add(new ProductStock
                    {
                        product_id = p.id, size = "M", stock_quantity = 50, reserved_quantity = 0,
                        is_active = true, created_at = DateTime.Now
                    });
                    await ctx.SaveChangesAsync();
                }

                var cpn = new Coupon
                {
                    code = ("R" + Guid.NewGuid().ToString("N").Substring(0, 11)).ToUpperInvariant(),
                    discount_type = (byte)DiscountTypeEnum.Fixed, value = 30m, min_amount = 0m,
                    usage_limit = 1, per_user_limit = 0, first_order_only = false,
                    is_active = true, created_at = DateTime.Now
                };
                ctx.Set<Coupon>().Add(cpn);
                await ctx.SaveChangesAsync();
                couponCode = cpn.code;

                for (int i = 0; i < callers; i++)
                {
                    var c = new Customer
                    {
                        name = "Yarisci", email = $"race-{Guid.NewGuid():N}@divisima.test", phone = "5550000000",
                        password_hash = new byte[] { 1 }, password_salt = new byte[] { 2 },
                        is_active = true, email_verified = true, store_credit = 0m, created_at = DateTime.Now
                    };
                    ctx.Set<Customer>().Add(c);
                    await ctx.SaveChangesAsync();
                    customerIds.Add(c.id);
                }
            }

            // 8 AYRI musteri, 8 AYRI DI scope -> gercek eszamanlilik. Kupon usage_limit = 1.
            var tasks = customerIds.Select((cid, idx) => Task.Run(async () =>
            {
                using var scope = _factory!.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
                {
                    customer_id = cid,
                    coupon_code = couponCode,
                    use_store_credit = 0m,
                    payment_method = 1,
                    items = new() { new OrderItemRequestDto { product_id = productIds[idx], size = "M", quantity = 1 } }
                });
            }));
            var results = await Task.WhenAll(tasks);

            var basarili = results.Count(r => r.Item2.Success);
            basarili.Should().BeGreaterThan(0, "en az bir siparis gecmeli (vakum engeli)");

            await using (var ctx = NewContext())
            {
                var kuponluSiparis = await ctx.Set<Order>().AsNoTracking()
                    .CountAsync(o => o.coupon_code == couponCode);
                var toplamSiparis = await ctx.Set<Order>().AsNoTracking().CountAsync();

                toplamSiparis.Should().Be(basarili, "her basarili cagri bir siparis satiri yazmali");
                kuponluSiparis.Should().Be(1,
                    "usage_limit=1 kupon coupon lock sayesinde YALNIZ BIR siparise uygulanmali - yaris kaybedenler kuponsuz gecer");

                var kuponlu = await ctx.Set<Order>().AsNoTracking().FirstAsync(o => o.coupon_code == couponCode);
                kuponlu.discount_amount.Should().Be(30m, "kazanan siparis indirimi gercekten almali");

                var kuponsuz = await ctx.Set<Order>().AsNoTracking().Where(o => o.coupon_code == null).ToListAsync();
                kuponsuz.Should().OnlyContain(o => o.discount_amount == 0m, "kuponsuz siparislerde indirim OLMAMALI");
            }
        }
    }
}
