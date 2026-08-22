using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Dtos.StockNotification;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
            // SPRINT 8 MADDE 10: abonelik yonetimi uclari eklendi. Bu sahte, stok testlerinde
            // yalniz NotifyBackInStock icin var; digerleri cagrilirsa GURULTULU duser (sessiz
            // bir varsayilan donmek, testin yanlis yolu olctugunu gizlerdi).
            public Task<(HttpStatusCode, Result)> GetMine(string email)
                => throw new NotSupportedException("Stok testlerinde kullanilmaz.");
            public Task<(HttpStatusCode, Result)> RemoveMine(int id, string email)
                => throw new NotSupportedException("Stok testlerinde kullanilmaz.");
            public Task<(HttpStatusCode, Result)> UnsubscribeByToken(string token)
                => throw new NotSupportedException("Stok testlerinde kullanilmaz.");
        }

        // Aciklayici yorum: HER cagri KENDI context/manager ciftini alir - EF DbContext thread-safe
        // DEGIL. Ortak context kullanmak yarisi olcmek yerine EF'i patlatirdi.
        private (StockManager mgr, DivisimaDbContext ctx) NewManager()
        {
            var ctx = NewContext();
            var mgr = new StockManager(new EfProductStockDal(ctx), new EfStockMovementDal(ctx),
                new EfStockReservationDal(ctx), new FakeStockNotification(),
                // SUPHELI #18: StockManager artik "odeme alindi ama stok yok" uyarisini zaman
                // cizelgesine de dusuyor. SAHTE degil GERCEK manager veriliyor - uyari yolunun
                // gercekten yazilabildigi bu siniflarda da surulsun.
                new OrderStatusHistoryManager(new EfOrderStatusHistoryDal(ctx), new EfOrderDal(ctx)),
                NullLogger<StockManager>.Instance);
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

        // SPRINT 2 - ATOMIK REZERVASYON (CAS) PINI.
        // ESKI DESEN: oku -> bellekte artir -> row_version ile yaz -> DbUpdateConcurrencyException
        // -> 3 kez dene -> pes et ve 409 don. Bol stok VARKEN bile eszamanli cagrilarin cogu
        // "Stok guncelleme cakismasi" aliyordu; musteri bunu "stok yok" diye okur.
        // YENI DESEN: tek atomik UPDATE (WHERE available >= qty). Cekisme satir kilidiyle cozulur,
        // concurrency istisnasi URETILMEZ - dolayisiyla stok yeterliyken KAYBEDEN CAGRI OLMAZ.
        [Fact]
        public async Task ReserveStock_BolStokla_SekizParalelIstek_HEPSI_Basarili_SIFIR_Cakisma()
        {
            if (Skipped()) return;
            const int stock = 500;
            const int each = 1;
            const int callers = 8;

            var pid = await NewProductWithStockAsync(stock);
            var managers = Enumerable.Range(0, callers).Select(_ => NewManager()).ToList();
            try
            {
                var results = await Task.WhenAll(managers.Select((m, i) =>
                    m.mgr.ReserveStock(pid, "M", each, orderId: 7300 + i)));

                var basarili = results.Count(r => r.Item2.Success);
                var cakisma = results.Count(r => r.Item1 == System.Net.HttpStatusCode.Conflict);

                basarili.Should().Be(callers,
                    $"stok bol - sekiz cagrinin HEPSI rezerve edebilmeli. Kodlar: {string.Join(",", results.Select(r => (int)r.Item1))}");
                cakisma.Should().Be(0, "atomik CAS ile concurrency cakismasi (409) HIC olusmamali");

                var after = await ReadStockAsync(pid);
                after.reserved.Should().Be(callers * each, "rezerve sayaci basarili istek sayisiyla birebir");
                after.physical.Should().Be(stock, "rezervasyon fiziksel stogu degistirmez");
            }
            finally
            {
                foreach (var m in managers) await m.ctx.DisposeAsync();
            }
        }

        // Stok TAM N iken: N cagri gecer, kalanlar "yetersiz stok" (400) alir - 409 DEGIL.
        // Ayrim onemli: 400 dogru ve anlasilir bir cevap ("stok kalmadi"), 409 ise altyapi
        // cakismasini musteriye stok sorunu gibi gosteren yanlis sinyaldi.
        [Fact]
        public async Task ReserveStock_TamNStok_TamN_Basarili_Kalanlar400_YetersizStok()
        {
            if (Skipped()) return;
            const int stock = 5;
            const int each = 1;
            const int callers = 8;

            var pid = await NewProductWithStockAsync(stock);
            var managers = Enumerable.Range(0, callers).Select(_ => NewManager()).ToList();
            try
            {
                var results = await Task.WhenAll(managers.Select((m, i) =>
                    m.mgr.ReserveStock(pid, "M", each, orderId: 7400 + i)));

                var kodlar = string.Join(",", results.Select(r => (int)r.Item1));
                results.Count(r => r.Item2.Success).Should().Be(stock,
                    $"tam stok kadar cagri gecmeli. Kodlar: {kodlar}");
                results.Count(r => r.Item1 == System.Net.HttpStatusCode.BadRequest).Should().Be(callers - stock,
                    $"kalan cagrilar YETERSIZ STOK (400) almali. Kodlar: {kodlar}");
                results.Count(r => r.Item1 == System.Net.HttpStatusCode.Conflict).Should().Be(0,
                    $"hicbir cagri cakisma (409) ALMAMALI. Kodlar: {kodlar}");

                var after = await ReadStockAsync(pid);
                after.reserved.Should().Be(stock, "rezerve tam stoka esitlenmeli");
                after.available.Should().Be(0, "musait sifira inmeli - overselling yok");
            }
            finally
            {
                foreach (var m in managers) await m.ctx.DisposeAsync();
            }
        }
    }
}
