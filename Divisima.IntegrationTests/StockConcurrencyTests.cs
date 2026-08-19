using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Dtos.StockNotification;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Aciklayici yorum: D2 KALBI - oversell yarisi. Kisitli stoga eszamanli talep gelince
    // musait stok ASLA negatife dusmemeli ve rezerve edilen toplam basarili isteklerle
    // birebir tutmalı. ReserveStock optimistic concurrency (row_version) + 3 retry kullaniyor.
    [Trait("Category", "Sql")]
    public class StockConcurrencyTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaStockRaceTest";

        private sealed class FakeStockNotification : IStockNotificationService
        {
            public Task NotifyBackInStock(int productId, string size) => Task.CompletedTask;
            public Task<(HttpStatusCode, Result)> Subscribe(StockNotificationSubscribeRequestDto dto)
                => throw new NotSupportedException("Stok testlerinde kullanilmaz.");
        }

        // Aciklayici yorum: HER cagri KENDI context/manager ciftini alir - EF DbContext thread-safe
        // DEGIL. Ortak context kullanmak yarisi olcmek yerine EF'i patlatirdi.
        private (StockManager mgr, DivisimaDbContext ctx) NewManager()
        {
            var ctx = NewContext();
            var mgr = new StockManager(new EfProductStockDal(ctx), new EfStockMovementDal(ctx),
                new EfStockReservationDal(ctx), new FakeStockNotification());
            return (mgr, ctx);
        }

        [Fact]
        public async Task ReserveStock_ParalelSekizIstek_OversellYok()
        {
            if (Skipped()) return;
            const int stock = 10;
            const int each = 2;
            const int callers = 8;                 // 8 x 2 = 16 talep > 10 stok

            var pid = await NewProductWithStockAsync(stock);
            var managers = Enumerable.Range(0, callers).Select(_ => NewManager()).ToList();
            try
            {
                // Her cagri AYRI siparis id ile rezerve etmeye calisir
                var results = await Task.WhenAll(managers.Select((m, i) =>
                    m.mgr.ReserveStock(pid, "M", each, orderId: 7100 + i)));

                var successCount = results.Count(r => r.Item2.Success);
                var after = await ReadStockAsync(pid);

                // VAKUM ENGELI: en az bir rezervasyon GECMELI, yoksa test hicbir sey kanitlamaz.
                successCount.Should().BeGreaterThan(0, "en az bir rezervasyon basarili olmali");
                successCount.Should().BeLessThanOrEqualTo(stock / each, "10 stoktan 2 adetlik en fazla 5 rezervasyon gecebilir");
                after.reserved.Should().Be(successCount * each, "rezerve toplami basarili istek sayisiyla BIREBIR tutmali");
                after.available.Should().BeGreaterThanOrEqualTo(0, "musait stok ASLA negatife dusmemeli");
                after.physical.Should().Be(stock, "rezervasyon fiziksel stogu DEGISTIRMEZ");
            }
            finally
            {
                foreach (var m in managers) await m.ctx.DisposeAsync();
            }
        }

        [Fact]
        public async Task TryDirectDeduct_ParalelSekizIstek_ToplamDususStokuASMAZ()
        {
            if (Skipped()) return;
            const int stock = 10;
            const int each = 2;
            const int callers = 8;

            var pid = await NewProductWithStockAsync(stock);
            var contexts = Enumerable.Range(0, callers).Select(_ => NewContext()).ToList();
            try
            {
                // TryDirectDeductAsync atomik "UPDATE ... WHERE stock_quantity >= quantity" olmali;
                // etkilenen satir sayisi 0 ise dusum YAPILMAMIS demektir.
                var affected = await Task.WhenAll(contexts.Select(c =>
                    new EfProductStockDal(c).TryDirectDeductAsync(pid, "M", each)));

                var successCount = affected.Count(a => a > 0);
                var after = await ReadStockAsync(pid);

                successCount.Should().BeGreaterThan(0, "en az bir dusum basarili olmali (vakum engeli)");
                successCount.Should().BeLessThanOrEqualTo(stock / each, "10 stoktan 2 adetlik en fazla 5 dusum gecebilir");
                after.physical.Should().Be(stock - successCount * each, "fiziksel stok basarili dusum sayisiyla BIREBIR tutmali");
                after.physical.Should().BeGreaterThanOrEqualTo(0, "fiziksel stok ASLA negatife dusmemeli");
            }
            finally
            {
                foreach (var c in contexts) await c.DisposeAsync();
            }
        }

        [Fact]
        public async Task ReserveStock_ParalelIstekler_BedenIzolasyonunuBozmaz()
        {
            if (Skipped()) return;
            const int stock = 10;
            var pid = await NewProductWithStockAsync(stock, "M", "L");
            var managers = Enumerable.Range(0, 6).Select(_ => NewManager()).ToList();
            try
            {
                // 6 eszamanli istek YALNIZ M bedenine
                var results = await Task.WhenAll(managers.Select((m, i) =>
                    m.mgr.ReserveStock(pid, "M", 2, orderId: 7200 + i)));
                var successCount = results.Count(r => r.Item2.Success);

                var m2 = await ReadStockAsync(pid, "M");
                var l = await ReadStockAsync(pid, "L");

                successCount.Should().BeGreaterThan(0, "en az bir rezervasyon gecmeli");
                m2.reserved.Should().Be(successCount * 2, "M bedeninde rezerve birebir tutmali");
                l.reserved.Should().Be(0, "L bedeni eszamanli yaristan ETKILENMEMELI");
                l.physical.Should().Be(stock);
                l.available.Should().Be(stock);
            }
            finally
            {
                foreach (var m in managers) await m.ctx.DisposeAsync();
            }
        }
    }
}
