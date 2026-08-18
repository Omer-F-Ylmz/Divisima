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
    public class OrderEndpointTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public OrderEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Açıklayıcı yorum: Test verisi tohumla (ürün + stok)
        private async Task<(int productId, int customerId)> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var product = new Product { name = "Test Elbise", brand = "Test", category_id = 1, price = 500, product_type = 0, is_active = true, created_at = DateTime.Now };
            db.Products.Add(product);
            var customer = new Customer { name = "Test Müşteri", email = $"t{Guid.NewGuid():N}@test.com", phone = "5550000000", password_hash = new byte[1], password_salt = new byte[1], is_active = true, created_at = DateTime.Now };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            db.ProductStocks.Add(new ProductStock { product_id = product.id, size = "M", stock_quantity = 10, is_active = true, created_at = DateTime.Now });
            await db.SaveChangesAsync();
            return (product.id, customer.id);
        }

        [Fact]
        public async Task PlaceOrder_ValidCart_Returns201_And_DecrementsStock()
        {
            // Arrange
            var (productId, customerId) = await SeedAsync();
            var client = _factory.CreateClient();
            var dto = new OrderCreateRequestDto
            {
                customer_id = customerId,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 2 } }
            };

            // Act — not: gerçek testte JWT token header'ı eklenir; burada akış örneği
            var response = await client.PostAsJsonAsync("/api/order/place", dto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var stock = await db.ProductStocks.FirstAsync(s => s.product_id == productId && s.size == "M");
            stock.stock_quantity.Should().Be(8); // 10 - 2
            (await db.Orders.CountAsync(o => o.customer_id == customerId)).Should().Be(1);
            (await db.OrderSnapshots.CountAsync()).Should().BeGreaterThan(0); // snapshot alındı
        }

        [Fact]
        public async Task PlaceOrder_InsufficientStock_Returns400_And_NoPartialData()
        {
            var (productId, customerId) = await SeedAsync();
            var client = _factory.CreateClient();
            var dto = new OrderCreateRequestDto
            {
                customer_id = customerId,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 999 } }
            };

            var response = await client.PostAsJsonAsync("/api/order/place", dto);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            // Açıklayıcı yorum: Transaction sayesinde yarım sipariş/stok kalmamalı
            (await db.Orders.CountAsync(o => o.customer_id == customerId)).Should().Be(0);
            var stock = await db.ProductStocks.FirstAsync(s => s.product_id == productId);
            stock.stock_quantity.Should().Be(10); // değişmemiş
        }

        [Fact]
        public async Task PlaceOrder_ConcurrentRequests_NoOverselling()
        {
            var (productId, customerId) = await SeedAsync(); // stok 10
            var client = _factory.CreateClient();
            OrderCreateRequestDto Make(int qty) => new()
            {
                customer_id = customerId,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = qty } }
            };

            // Açıklayıcı yorum: 8 paralel sipariş x 2 adet = 16 talep, stok 10 -> en fazla 5 başarılı olmalı
            var tasks = Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/order/place", Make(2)));
            var results = await Task.WhenAll(tasks);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            var stock = await db.ProductStocks.FirstAsync(s => s.product_id == productId);
            // Açıklayıcı yorum: Optimistic concurrency sayesinde stok asla negatife düşmez
            stock.stock_quantity.Should().BeGreaterThanOrEqualTo(0);
            var successCount = results.Count(r => r.StatusCode == HttpStatusCode.Created);
            (successCount * 2).Should().BeLessThanOrEqualTo(10); // toplam satılan <= başlangıç stoğu
        }
    }
}
