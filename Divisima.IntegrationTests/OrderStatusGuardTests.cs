using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Dtos.Shipping;
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
    // D5-2 - DURUM MAKINESI GUARD LARI, YAZAN TUM YOLLARDA
    //
    // OrderStatusMachine in KENDISI zaten birim testli (OrderStatusMachineTests). Burada olculen
    // sey farkli: status YAZAN yollar makineye gercekten DANISIYOR mu.
    //   - OrderManager.ChangeOrderStatus  -> IsValidTransition cagiriyor
    //   - ShipmentManager.CreateShipment  -> IsValidTransition cagiriyor (dogrudan status=Shipped yaziyor)
    // Kargo yolu ozellikle onemli: guard olmasa ODENMEMIS (Pending) siparis kargolanabilir ya da
    // iptal edilmis siparis Shipped e canlanabilirdi.
    [Trait("Category", "Sql")]
    public class OrderStatusGuardTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaStatusGuardTest";
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

        private sealed class GuardFactory : WebApplicationFactory<Program>
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

        private GuardFactory? _factory;
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
                _factory = new GuardFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak durum guard testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // Aciklayici yorum: Belirtilen durumda bir siparis tohumla (her test kendi musterisi + siparisi).
        private static async Task<int> NewOrderAsync(OrderStatusEnum status)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Guard Testi", email = $"guard-{Guid.NewGuid():N}@divisima.test", phone = "5550000000",
                password_hash = new byte[] { 1 }, password_salt = new byte[] { 2 },
                is_active = true, email_verified = true, created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            await ctx.SaveChangesAsync();

            var o = new Order
            {
                customer_id = c.id,
                order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                status = (byte)status,
                subtotal = 200m, total_price = 200m, store_credit_used = 0m,
                is_online_payment_done = false, currency = "TRY", created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();
            return o.id;
        }

        private static async Task<byte> ReadStatusAsync(int orderId)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).status;
        }

        [Fact]
        public async Task ChangeStatus_PendingDenDelivered_Reddedilir_DurumDEGISMEZ()
        {
            if (Skipped()) return;
            var orderId = await NewOrderAsync(OrderStatusEnum.Pending);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                new OrderStatusChangeRequestDto { id = orderId, order_status = OrderStatusEnum.Delivered }));

            r.Item2.Success.Should().BeFalse("Pending -> Delivered gecersiz gecis");
            (await ReadStatusAsync(orderId)).Should().Be((byte)OrderStatusEnum.Pending,
                "reddedilen gecis veritabanina YAZILMAMALI");
        }

        [Fact]
        public async Task ChangeStatus_DeliveredDenCancelled_Reddedilir_DurumDEGISMEZ()
        {
            if (Skipped()) return;
            var orderId = await NewOrderAsync(OrderStatusEnum.Delivered);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                new OrderStatusChangeRequestDto { id = orderId, order_status = OrderStatusEnum.Cancelled }));

            r.Item2.Success.Should().BeFalse("Delivered terminal - iptale gecilemez");
            (await ReadStatusAsync(orderId)).Should().Be((byte)OrderStatusEnum.Delivered);
        }

        // POZITIF KONTROL: makine gecerli dedigi gecisi GERCEKTEN uyguluyor. Bu olmadan ustteki
        // iki test "her sey reddediliyor" durumunda da yesil kalirdi.
        [Fact]
        public async Task ChangeStatus_ConfirmedDenPreparing_Uygulanir()
        {
            if (Skipped()) return;
            var orderId = await NewOrderAsync(OrderStatusEnum.Confirmed);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                new OrderStatusChangeRequestDto { id = orderId, order_status = OrderStatusEnum.Preparing }));

            r.Item2.Success.Should().BeTrue($"Confirmed -> Preparing gecerli: {r.Item2.Message}");
            (await ReadStatusAsync(orderId)).Should().Be((byte)OrderStatusEnum.Preparing);
        }

        [Fact]
        public async Task Kargo_PendingSiparise_Reddedilir_KargoKaydiOLUSMAZ()
        {
            if (Skipped()) return;
            var orderId = await NewOrderAsync(OrderStatusEnum.Pending);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IShipmentService>().CreateShipment(
                new ShipmentCreateDto { order_id = orderId, carrier = 0, tracking_number = "TRK-PENDING-1" }));

            r.Item2.Success.Should().BeFalse("odenmemis (Pending) siparis kargolanamaz");
            (await ReadStatusAsync(orderId)).Should().Be((byte)OrderStatusEnum.Pending,
                "reddedilen kargo siparisi Shipped e CEKMEMELI");

            await using var ctx = NewContext();
            (await ctx.Set<Shipment>().CountAsync(s => s.order_id == orderId))
                .Should().Be(0, "reddedilen istek kargo kaydi OLUSTURMAMALI");
        }

        [Fact]
        public async Task Kargo_PreparingSiparise_Uygulanir_DurumShipped_KargoKaydiOlusur()
        {
            if (Skipped()) return;
            var orderId = await NewOrderAsync(OrderStatusEnum.Preparing);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IShipmentService>().CreateShipment(
                new ShipmentCreateDto { order_id = orderId, carrier = 0, tracking_number = "TRK-PREP-1" }));

            r.Item2.Success.Should().BeTrue($"Preparing -> Shipped gecerli: {r.Item2.Message}");
            (await ReadStatusAsync(orderId)).Should().Be((byte)OrderStatusEnum.Shipped);

            await using var ctx = NewContext();
            (await ctx.Set<Shipment>().CountAsync(s => s.order_id == orderId))
                .Should().Be(1, "kargo kaydi olusmali");
        }
    }
}
