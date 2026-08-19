using System.Net;
using System.Net.Http.Json;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Sipariş endpoint'i gerçek entegrasyon testleri (gerçek DB container'ı).
    // İstekler TestAuthHelper ile alınan GERÇEK JWT ile atılır - /api/order/place
    // [RequireUserType(Customer)] taşıyor, yetkisiz istek 401 döner.
    public class OrderEndpointTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public OrderEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Açıklayıcı yorum: Ürün + stok tohumla. Müşteri ARTIK burada üretilmiyor: sipariş sahibi
        // controller'da token'dan alınıyor (dto.customer_id = _currentUser.GetRequiredUserId()),
        // yani seed'lenmiş bir müşteri id'si zaten yok sayılırdı.
        // Her test kendi ürününü yaratır -> testler birbirinin stoğunu etkilemez.
        private async Task<int> SeedProductAsync(int stockQuantity = 10)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();

            var category = new Category
            {
                name = $"Test Kategori {Guid.NewGuid():N}",
                slug = $"test-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            db.Set<Category>().Add(category);
            await db.SaveChangesAsync();

            var product = new Product
            {
                name = "Test Elbise",
                brand = "Test",
                category_id = category.id,
                price = 500,
                description = "Test urun aciklamasi",
                color_hex = "#000000",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            db.ProductStocks.Add(new ProductStock
            {
                product_id = product.id,
                size = "M",
                stock_quantity = stockQuantity,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await db.SaveChangesAsync();

            return product.id;
        }

        // Açıklayıcı yorum: Satılabilir stok = fiziksel - rezerve. PlaceOrder fiziksel stoğu DÜŞÜRMEZ,
        // REZERVE eder (ödeme onaylanınca fiziksel düşer). "Stok düştü" bu yüzden müsait stok üzerinden
        // doğrulanır - niyet aynı, model güncel.
        private async Task<(int physical, int reserved, int available)> ReadStockAsync(int productId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var s = await db.ProductStocks.AsNoTracking().FirstAsync(x => x.product_id == productId && x.size == "M");
            return (s.stock_quantity, s.reserved_quantity, s.stock_quantity - s.reserved_quantity);
        }

        [Fact]
        public async Task PlaceOrder_ValidCart_Returns201_And_DecrementsStock()
        {
            var productId = await SeedProductAsync();                        // müsait stok 10
            var auth = await TestAuthHelper.CreateCustomerClientAsync(_factory);

            var dto = new OrderCreateRequestDto
            {
                customer_id = auth.CustomerId,
                coupon_code = "",   // zorunlu binding: DTO alani nullable degil (bkz. rapor)
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 2 } }
            };

            var response = await auth.Client.PostAsJsonAsync("/api/order/place", dto);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var (_, _, available) = await ReadStockAsync(productId);
            available.Should().Be(8, "2 adet sipariş müsait stoktan düşmeli (10 - 2)");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            (await db.Orders.CountAsync(o => o.customer_id == auth.CustomerId)).Should().Be(1);
            (await db.OrderSnapshots.CountAsync()).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task PlaceOrder_InsufficientStock_Returns400_And_NoPartialData()
        {
            var productId = await SeedProductAsync();                        // müsait stok 10
            var auth = await TestAuthHelper.CreateCustomerClientAsync(_factory);

            var dto = new OrderCreateRequestDto
            {
                customer_id = auth.CustomerId,
                coupon_code = "",   // zorunlu binding: DTO alani nullable degil (bkz. rapor)
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 50 } }
            };

            var response = await auth.Client.PostAsJsonAsync("/api/order/place", dto);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // NOT: adet 50 - stok 10, yani stok YETMEZ. 999 kullanilamaz cunku validator tek urunde
            // en fazla 100 adet birakiyor ve istek stok kontrolune VARMADAN dogrulama 400u aliyordu
            // (CI annotations ile teshis edildi).
            // Aciklayici yorum: SADECE 400 gormek YETMEZ - FluentValidation otomatik dogrulamasi da
            // 400 doner (AddFluentValidationAutoValidation acik). Gelen 400'un GERCEKTEN stok
            // yetersizliginden geldigi govdeden dogrulanir; yoksa test yanlis sebeple yesil kalabilir.
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Yetersiz stok", "400, dogrulama hatasindan degil stok yetersizliginden gelmeli");

            // Açıklayıcı yorum: Transaction sayesinde yarım sipariş/rezervasyon kalmamalı
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            (await db.Orders.CountAsync(o => o.customer_id == auth.CustomerId)).Should().Be(0);

            var (physical, reserved, available) = await ReadStockAsync(productId);
            physical.Should().Be(10, "fiziksel stok değişmemeli");
            reserved.Should().Be(0, "başarısız siparişten rezervasyon kalmamalı");
            available.Should().Be(10);
        }

        [Fact]
        public async Task PlaceOrder_ConcurrentRequests_NoOverselling()
        {
            const int stock = 10;
            const int perOrder = 2;
            const int shoppers = 8;                                          // 8 x 2 = 16 talep > 10 stok

            var productId = await SeedProductAsync(stock);

            // Açıklayıcı yorum: AYRI MÜŞTERİLER - gerçek overselling senaryosu farklı alıcıların son
            // stok için yarışmasıdır. Tek müşteriyle de stok yarışı olurdu, ama ayrı müşteriler
            // senaryoyu gerçekçi kılar ve müşteri bazlı herhangi bir serileştirmenin yarışı
            // gizlemesini engeller. (Sipariş sahibi token'dan geldiği için her istemci kendi
            // müşterisi adına sipariş verir.)
            var clients = await Task.WhenAll(
                Enumerable.Range(0, shoppers).Select(_ => TestAuthHelper.CreateCustomerClientAsync(_factory)));

            OrderCreateRequestDto Make(int customerId) => new()
            {
                customer_id = customerId,
                coupon_code = "",   // zorunlu binding: DTO alani nullable degil (bkz. rapor)
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = perOrder } }
            };

            var results = await Task.WhenAll(clients.Select(c => c.Client.PostAsJsonAsync("/api/order/place", Make(c.CustomerId))));

            var successCount = results.Count(r => r.StatusCode == HttpStatusCode.Created);
            var (physical, reserved, available) = await ReadStockAsync(productId);

            // Açıklayıcı yorum: Bu üç iddia birlikte "oversell yok"u GERÇEKTEN sınar. Tek başına
            // "satılan <= stok" iddiası HİÇBİR sipariş geçmese de doğru olurdu (vakum geçiş) -
            // bu yüzden en az bir siparişin başarılı olduğu ayrıca zorunlu tutuluyor.
            successCount.Should().BeGreaterThan(0, "en az bir sipariş başarılı olmalı; hiçbiri geçmezse test hiçbir şey kanıtlamaz");
            successCount.Should().BeLessThanOrEqualTo(stock / perOrder, "stok 10 iken 2 adetlik en fazla 5 siparis karsilanabilir");
            reserved.Should().Be(successCount * perOrder, "rezerve edilen miktar basarili siparis sayisiyla birebir tutmali (ne eksik ne fazla)");
            available.Should().BeGreaterThanOrEqualTo(0, "musait stok asla negatife dusmemeli");
            physical.Should().Be(stock, "odeme onaylanmadan fiziksel stok dusmemeli");
        }
    }
}
