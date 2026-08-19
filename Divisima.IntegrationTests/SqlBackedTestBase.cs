using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: GERÇEK SQL'e karşı koşan para testleri için ortak taban.
    // Bağlantı deseni InvoiceCancellationTests ile BİREBİR aynı:
    //  - DIVISIMA_TEST_SQL VERİLMİŞSE (CI): SQL zorunlu; bağlanılamazsa sessizce atlanmaz, PATLAR.
    //  - VERİLMEMİŞSE (yerel): LocalDB denenir; yoksa testler atlanır.
    // Her test SINIFI kendi veritabanını kullanır (xUnit sınıfları paralel koşar; ortak DB
    // olsaydı biri digerinin EnsureDeleted'i ile silinirdi).
    public abstract class SqlBackedTestBase : IAsyncLifetime
    {
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        protected abstract string DatabaseName { get; }

        private bool _sqlAvailable;

        protected string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = DatabaseName }.ConnectionString;
            }
        }

        protected DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using var ctx = NewContext();
                await ctx.Database.EnsureDeletedAsync();
                await ctx.Database.EnsureCreatedAsync();
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    $"DIVISIMA_TEST_SQL verildi ancak SQL Server'a baglanilamadi ({DatabaseName}) - " +
                    "para testleri ATLANMAMALI.", ex);
            }
            catch
            {
                _sqlAvailable = false;
            }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try
            {
                await using var ctx = NewContext();
                await ctx.Database.EnsureDeletedAsync();
            }
            catch { /* temizlik best-effort */ }
        }

        protected bool Skipped() => !_sqlAvailable;

        // Açıklayıcı yorum: Her test KENDI musterisini benzersiz alanlarla yaratir - var olan
        // satirlara guvenmek yok (testler paralel kosabilir).
        protected async Task<Customer> NewCustomerAsync(decimal storeCredit = 0m, int loyaltyPoints = 0)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Para Testi",
                email = $"money-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                store_credit = storeCredit,
                loyalty_points = loyaltyPoints,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            await ctx.SaveChangesAsync();
            return c;
        }

        protected async Task<Order> NewOrderAsync(int customerId, decimal total, decimal storeCreditUsed = 0m,
            byte status = 1, bool onlinePaid = false)
        {
            await using var ctx = NewContext();
            var o = new Order
            {
                customer_id = customerId,
                order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                status = status,
                subtotal = total,
                total_price = total,
                store_credit_used = storeCreditUsed,
                is_online_payment_done = onlinePaid,
                currency = "TRY",
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();
            return o;
        }

        protected async Task<decimal> ReadCreditAsync(int customerId)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == customerId)).store_credit;
        }

        protected async Task<int> ReadPointsAsync(int customerId)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == customerId)).loyalty_points;
        }
    }
}
