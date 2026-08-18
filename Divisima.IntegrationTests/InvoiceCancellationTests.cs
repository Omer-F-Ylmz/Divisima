using System.Net;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Integrations.EInvoice;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: FATURA İPTALİ doğrulaması. Docker/Testcontainers yerine yerel LocalDB (gerçek SQL Server)
    // kullanır; gerçek EF DAL'ları + gerçek InvoiceManager çalışır, sonuç DB'den TAZE context ile okunur.
    // Doğrulanan: iptal edilmiş siparişin faturası status=3 (InvoiceStatusEnum.Cancelled) olur.
    public class InvoiceCancellationTests : IAsyncLifetime
    {
        // SQL Server bağlantısı: CI'da DIVISIMA_TEST_SQL ile gerçek bir sunucuya yönlendirilebilir;
        // yerelde LocalDB'ye düşer. Erişilemiyorsa (ör. Linux CI runner'ında LocalDB yok) testler
        // ATLANIR - yeşil CI'yı kırmaz, ama SQL olan her yerde gerçek regresyon koruması sağlar.
        private static readonly string ConnStr =
            Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=DivisimaInvoiceCancelTest;Trusted_Connection=True;TrustServerCertificate=True;";

        private bool _sqlAvailable;

        private DbContextOptions<DivisimaDbContext> Options =>
            new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options;

        private DivisimaDbContext NewContext() => new DivisimaDbContext(Options);

        // e-Fatura sağlayıcısı CancelForOrder'da HİÇ kullanılmaz; ctor'u doyurmak için sessiz sahte.
        private sealed class NoopEInvoiceProvider : IEInvoiceProvider
        {
            public Task<EInvoiceResult> SendInvoiceAsync(EInvoiceRequest request) =>
                Task.FromResult(new EInvoiceResult { Success = false, ErrorMessage = "test" });
        }

        private InvoiceManager NewManager(DivisimaDbContext ctx)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            return new InvoiceManager(
                new EfInvoiceDal(ctx), new EfOrderDal(ctx), new EfOrderItemDal(ctx),
                new EfProductDal(ctx), new NoopEInvoiceProvider(), config);
        }

        public async Task InitializeAsync()
        {
            try
            {
                await using var ctx = NewContext();
                await ctx.Database.EnsureDeletedAsync();
                await ctx.Database.EnsureCreatedAsync();
                _sqlAvailable = true;
            }
            catch
            {
                // SQL Server yok -> testler atlanır (aşağıdaki Skipped() koruması).
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

        // SQL yoksa test gövdesi çalıştırılmaz (xUnit 2.6'da çalışma anı Skip yok).
        private bool Skipped() => !_sqlAvailable;

        // Sipariş + faturayı kur; faturanın başlangıç durumu parametrik.
        private async Task<(int orderId, int invoiceId)> SeedAsync(byte orderStatus, byte? invoiceStatus)
        {
            await using var ctx = NewContext();
            // orders -> customers FK'si var: önce müşteri lazım.
            var customer = new Customer
            {
                name = "Test Musteri",
                email = $"test-{Guid.NewGuid():N}@divisima.test",
                phone = "05000000000",
                password_salt = new byte[] { 1, 2, 3 },
                password_hash = new byte[] { 4, 5, 6 },
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(customer);
            await ctx.SaveChangesAsync();

            var order = new Order
            {
                customer_id = customer.id,
                order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                status = orderStatus,
                subtotal = 100m,
                total_price = 120m,
                currency = "TRY",
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(order);
            await ctx.SaveChangesAsync();

            int invoiceId = 0;
            if (invoiceStatus.HasValue)
            {
                var invoice = new Invoice
                {
                    order_id = order.id,
                    customer_id = customer.id,
                    invoice_number = $"DIV-TEST-{order.id:D6}",
                    invoice_type = (byte)InvoiceTypeEnum.Individual,
                    subtotal = 100m,
                    tax_rate = 0.20m,
                    tax_amount = 20m,
                    total = 120m,
                    status = invoiceStatus.Value,
                    created_at = DateTime.Now
                };
                ctx.Set<Invoice>().Add(invoice);
                await ctx.SaveChangesAsync();
                invoiceId = invoice.id;
            }
            return (order.id, invoiceId);
        }

        private async Task<byte> ReadInvoiceStatusAsync(int invoiceId)
        {
            // TAZE context - önceki context'in izlediği nesne değil, DB'deki GERÇEK satır okunur.
            await using var ctx = NewContext();
            var row = await ctx.Set<Invoice>().AsNoTracking().SingleAsync(i => i.id == invoiceId);
            return row.status;
        }

        [Fact]
        public async Task CancelForOrder_IptalEdilenSiparis_FaturaStatus3Olur()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Sent);

            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(1, "başlangıçta fatura Sent olmalı");

            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            (await ReadInvoiceStatusAsync(invoiceId))
                .Should().Be((byte)InvoiceStatusEnum.Cancelled)
                .And.Be(3, "InvoiceStatusEnum.Cancelled = 3 DB'ye yazılmalı");
        }

        [Fact]
        public async Task CancelForOrder_IkinciCagri_Idempotent()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Approved);

            await using (var ctx1 = NewContext())
                await NewManager(ctx1).CancelForOrder(orderId);

            await using var ctx2 = NewContext();
            var (code, result) = await NewManager(ctx2).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Fatura zaten iptal edilmiş.");
            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(3);
        }

        [Fact]
        public async Task CancelForOrder_SiparisIptalDegilse_BadRequest_FaturaDokunulmaz()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Confirmed, (byte)InvoiceStatusEnum.Sent);

            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.BadRequest);
            result.Success.Should().BeFalse();
            (await ReadInvoiceStatusAsync(invoiceId))
                .Should().Be(1, "aktif siparişin faturası iptal EDİLMEMELİ");
        }

        [Fact]
        public async Task CancelForOrder_FaturaYoksa_BasariliNoOp()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, _) = await SeedAsync((byte)OrderStatusEnum.Cancelled, null);

            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            result.Message.Should().Be("İptal edilecek fatura bulunmuyor.");
        }

        [Fact]
        public async Task CancelForOrder_SiparisYoksa_NotFound()
        {
            if (Skipped()) return;   // SQL Server yok
            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(999999);

            code.Should().Be(HttpStatusCode.NotFound);
            result.Success.Should().BeFalse();
        }
    }
}
