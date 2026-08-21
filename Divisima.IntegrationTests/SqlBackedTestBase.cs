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
            byte status = 1, bool onlinePaid = false, string? couponCode = null)
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
                coupon_code = couponCode,
                currency = "TRY",
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();
            return o;
        }


        // Aciklayici yorum: Urun + beden bazli stok tohumla. Her cagri KENDI kategorisini ve urununu
        // yaratir - testler ayni satiri paylasmaz. description/color_hex zorunlu alanlar dolu.
        protected async Task<int> NewProductWithStockAsync(int stockQuantity, params string[] sizes)
        {
            if (sizes == null || sizes.Length == 0) sizes = new[] { "M" };
            await using var ctx = NewContext();
            var cat = new Category
            {
                name = $"Stok Kategori {Guid.NewGuid():N}",
                slug = $"stok-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Stok Test Urun",
                brand = "T",
                category_id = cat.id,
                price = 100m,
                description = "stok testi urunu",
                color_hex = "#123456",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            foreach (var s in sizes)
            {
                ctx.ProductStocks.Add(new ProductStock
                {
                    product_id = p.id,
                    size = s,
                    stock_quantity = stockQuantity,
                    reserved_quantity = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
            }
            await ctx.SaveChangesAsync();
            return p.id;
        }

        // Aciklayici yorum: Uc sayac BIRLIKTE okunur - fiziksel, rezerve ve musait ayni anda dogrulanmali.
        protected async Task<(int physical, int reserved, int available)> ReadStockAsync(int productId, string size = "M")
        {
            await using var ctx = NewContext();
            var s = await ctx.ProductStocks.AsNoTracking().SingleAsync(x => x.product_id == productId && x.size == size);
            return (s.stock_quantity, s.reserved_quantity, s.stock_quantity - s.reserved_quantity);
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
