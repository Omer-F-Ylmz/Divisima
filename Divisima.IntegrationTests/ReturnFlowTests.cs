using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Return;
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
    // D5-3 - IADE TALEBI AKISI (ReturnManager.CreateReturn)
    //
    // KAPSAM SINIRI: D1 dalgasi RefundToSourceAsync i (paranin karta/cuzdana BOLUNMESI) olcmustu.
    // Burada olculen sey farkli ve ustundeki katman: iade TALEBININ kabul kurallari -
    //   1) pencere hangi tarihten sayiliyor (delivered_at mi created_at mi),
    //   2) talep edilen adet siparis edilen adedi asabiliyor mu,
    //   3) kaydedilen refund_amount indirimle orantili mi.
    [Trait("Category", "Sql")]
    public class ReturnFlowTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaReturnFlowTest";
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

        private sealed class ReturnFactory : WebApplicationFactory<Program>
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

        private ReturnFactory? _factory;
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
                _factory = new ReturnFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak iade akisi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // Teslim edilmis bir siparis + tek kalem tohumla. createdDaysAgo ve deliveredDaysAgo AYRI
        // verilebiliyor - pencerenin HANGI tarihten sayildigini ancak boyle ayirt edebiliriz.
        private static async Task<(int customerId, int orderId, int productId)> SeedDeliveredOrderAsync(
            int createdDaysAgo, int? deliveredDaysAgo, int quantity = 2,
            decimal unitPrice = 100m, decimal subtotal = 200m, decimal discount = 0m)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Iade Testi", email = $"return-{Guid.NewGuid():N}@divisima.test", phone = "5550000000",
                password_hash = new byte[] { 1 }, password_salt = new byte[] { 2 },
                is_active = true, email_verified = true, created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            var cat = new Category
            {
                name = "Iade Kategori", slug = $"iade-{Guid.NewGuid():N}",
                is_active = true, created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Iade Urun", brand = "T", category_id = cat.id, price = unitPrice,
                description = "iade testi urunu", color_hex = "#303030",
                product_type = 0, is_active = true, created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            var o = new Order
            {
                customer_id = c.id,
                order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                status = (byte)OrderStatusEnum.Delivered,
                subtotal = subtotal, total_price = subtotal - discount, discount_amount = discount,
                store_credit_used = 0m, is_online_payment_done = true, currency = "TRY",
                created_at = DateTime.Now.AddDays(-createdDaysAgo),
                delivered_at = deliveredDaysAgo.HasValue ? DateTime.Now.AddDays(-deliveredDaysAgo.Value) : null
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();

            ctx.Set<OrderItem>().Add(new OrderItem
            {
                order_id = o.id, product_id = p.id, size = "M", quantity = quantity,
                unit_price = unitPrice, is_cancelled = false, created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return (c.id, o.id, p.id);
        }

        private static ReturnCreateRequestDto Talep(int customerId, int orderId, int productId, int quantity) => new()
        {
            customer_id = customerId, order_id = orderId, product_id = productId,
            size = "M", quantity = quantity, reason = 0, return_type = 0, description = "iade talebi"
        };

        // Pencere 14 gun. Iki yon birden sinanir: created_at eski ama teslim yeni -> KABUL;
        // created_at yeni ama teslim eski -> RED. Tek yon olsa "her sey kabul/red" ile de yesil kalirdi.
        [Fact]
        public async Task IadePenceresi_DELIVERED_AT_EsasAlinir_CreatedAt_Degil()
        {
            if (Skipped()) return;

            // 1) Siparis 40 gun once verilmis (created_at penceresi ASMIS) ama 2 gun once teslim edilmis.
            var (c1, o1, p1) = await SeedDeliveredOrderAsync(createdDaysAgo: 40, deliveredDaysAgo: 2);
            var gecTeslim = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c1, o1, p1, 1)));
            gecTeslim.Item2.Success.Should().BeTrue(
                $"pencere TESLIM tarihinden sayilmali - gec teslim edilen siparis iade edilebilmeli: {gecTeslim.Item2.Message}");

            // 2) Siparis bugun verilmis ama 20 gun once teslim edilmis (imkansiz ama tarih alanlarini
            //    ayristirmak icin kasitli): pencere teslimden sayilirsa RED gelmeli.
            var (c2, o2, p2) = await SeedDeliveredOrderAsync(createdDaysAgo: 0, deliveredDaysAgo: 20);
            var eskiTeslim = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c2, o2, p2, 1)));
            eskiTeslim.Item2.Success.Should().BeFalse(
                "teslimden 20 gun gecmis - 14 gunluk pencere kapali olmali");

            await using var ctx = NewContext();
            (await ctx.Set<ReturnRequest>().CountAsync(r => r.order_id == o1))
                .Should().Be(1, "kabul edilen talep kayit olusturmali");
            (await ctx.Set<ReturnRequest>().CountAsync(r => r.order_id == o2))
                .Should().Be(0, "reddedilen talep kayit OLUSTURMAMALI");
        }

        [Fact]
        public async Task IadeMiktari_SiparisEdilenAdedi_ASAMAZ_KismiIadeSonrasiKalanKadar()
        {
            if (Skipped()) return;
            var (c, o, p) = await SeedDeliveredOrderAsync(createdDaysAgo: 1, deliveredDaysAgo: 1, quantity: 2);

            var fazla = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c, o, p, 3)));
            fazla.Item2.Success.Should().BeFalse("2 adet alinmis urunden 3 adet iade edilemez");

            // POZITIF OLAY: sinir icindeki talep GERCEKTEN kabul ediliyor.
            var birinci = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c, o, p, 1)));
            birinci.Item2.Success.Should().BeTrue($"1 adet iade edilebilmeli: {birinci.Item2.Message}");

            // Kalan 1 adet - ikinci talep de gecer.
            var ikinci = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c, o, p, 1)));
            ikinci.Item2.Success.Should().BeTrue($"kalan 1 adet de iade edilebilmeli: {ikinci.Item2.Message}");

            // Kalan sifir - ucuncu talep reddedilmeli (cift iade engeli).
            var ucuncu = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c, o, p, 1)));
            ucuncu.Item2.Success.Should().BeFalse("kalan adet bitti - yeni iade talebi kabul edilmemeli");

            await using var ctx = NewContext();
            var kayitlar = await ctx.Set<ReturnRequest>().AsNoTracking()
                .Where(r => r.order_id == o).ToListAsync();
            kayitlar.Count.Should().Be(2, "yalniz iki talep kaydi olusmali");
            kayitlar.Sum(r => r.quantity).Should().Be(2, "toplam iade adedi siparis adedini asmamali");
        }

        // subtotal 200, indirim 50, kalem birim fiyati 100, 1 adet:
        // 100 * 1 * (200 - 50) / 200 = 75. Liste fiyati (100) yazilsaydi FAZLA IADE olurdu.
        [Fact]
        public async Task IadeTutari_KalemePayDusen_IndirimOraniyla_Hesaplanir()
        {
            if (Skipped()) return;
            var (c, o, p) = await SeedDeliveredOrderAsync(
                createdDaysAgo: 1, deliveredDaysAgo: 1, quantity: 2,
                unitPrice: 100m, subtotal: 200m, discount: 50m);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IReturnService>()
                .CreateReturn(Talep(c, o, p, 1)));
            r.Item2.Success.Should().BeTrue($"talep kabul edilmeli: {r.Item2.Message}");

            await using var ctx = NewContext();
            var kayit = await ctx.Set<ReturnRequest>().AsNoTracking().SingleAsync(x => x.order_id == o);
            kayit.refund_amount.Should().Be(75m,
                "indirim kaleme orantili dusulmeli - liste fiyati (100) yazilirsa fazla iade olur");
        }
    }
}
