using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Dtos.StockNotification;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Aciklayici yorum: Rezervasyon yasam dongusu ve stok guardlari. Gercek SQL + gercek EF DAL.
    // Bildirim servisi dis bagimlilik oldugu icin sahte; olculen sey sayaclar ve hareket kayitlari.
    // Aciklayici yorum: GERCEK SQL gerektirir - ci.yml adanmis adimi bu trait ile suzuyor.
    [Trait("Category", "Sql")]
    public class StockReservationTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaStockTest";

        private sealed class FakeStockNotification : IStockNotificationService
        {
            public int NotifyCount;
            public Task NotifyBackInStock(int productId, string size) { NotifyCount++; return Task.CompletedTask; }
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

        private async Task<List<StockMovement>> MovementsAsync(int productId)
        {
            await using var ctx = NewContext();
            return await ctx.Set<StockMovement>().AsNoTracking()
                .Where(m => m.product_id == productId).ToListAsync();
        }

        private async Task<int> ReservationCountAsync(int orderId, ReservationStatusEnum status)
        {
            await using var ctx = NewContext();
            return await ctx.Set<StockReservation>().CountAsync(r => r.order_id == orderId && r.status == (byte)status);
        }

        [Fact]
        public async Task Reserve_Sonra_Confirm_UcSayacBirlikteDogru()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            var reserve = await mgr.ReserveStock(pid, "M", 3, orderId: oid);
            reserve.Item2.Success.Should().BeTrue($"rezervasyon basarili olmali: {reserve.Item2.Message}");
            var afterReserve = await ReadStockAsync(pid);
            afterReserve.physical.Should().Be(10, "REZERVE fiziksel stogu DUSURMEZ");
            afterReserve.reserved.Should().Be(3);
            afterReserve.available.Should().Be(7);

            var confirm = await mgr.ConfirmReservation(oid);
            confirm.Item2.Success.Should().BeTrue();
            var afterConfirm = await ReadStockAsync(pid);
            afterConfirm.physical.Should().Be(7, "ONAY fiziksel stogu dusurur");
            afterConfirm.reserved.Should().Be(0, "onaydan sonra rezerve serbest kalir");
            afterConfirm.available.Should().Be(7);

            (await ReservationCountAsync(oid, ReservationStatusEnum.Confirmed)).Should().Be(1);
            (await MovementsAsync(pid)).Should().ContainSingle(m =>
                m.movement_type == (byte)StockMovementType.Out && m.quantity == 3, "onay bir Out hareketi yazmali");
        }

        [Fact]
        public async Task Reserve_Sonra_Release_FizikselDegismez()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 4, orderId: oid)).Item2.Success.Should().BeTrue();
            (await ReadStockAsync(pid)).reserved.Should().Be(4, "on kosul: rezerve olusmali");

            var release = await mgr.ReleaseReservation(oid);
            release.Item2.Success.Should().BeTrue();

            var after = await ReadStockAsync(pid);
            after.physical.Should().Be(10, "SERBEST BIRAKMA fiziksel stogu DEGISTIRMEZ");
            after.reserved.Should().Be(0);
            after.available.Should().Be(10);
            (await ReservationCountAsync(oid, ReservationStatusEnum.Released)).Should().Be(1);
        }

        [Fact]
        public async Task Confirm_IkiKezCagrilinca_CiftDusumYok()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 3, orderId: oid)).Item2.Success.Should().BeTrue();
            await mgr.ConfirmReservation(oid);
            (await ReadStockAsync(pid)).physical.Should().Be(7, "on kosul: ilk onay dusurmus olmali");

            await mgr.ConfirmReservation(oid);

            var after = await ReadStockAsync(pid);
            after.physical.Should().Be(7, "ikinci onay stogu TEKRAR dusurmemeli");
            after.reserved.Should().Be(0);
            (await MovementsAsync(pid)).Count(m => m.movement_type == (byte)StockMovementType.Out)
                .Should().Be(1, "ikinci onay IKINCI hareket kaydi yazmamali");
        }

        [Fact]
        public async Task Release_IkiKezCagrilinca_CiftSerbestBirakmaYok()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 4, orderId: oid)).Item2.Success.Should().BeTrue();
            await mgr.ReleaseReservation(oid);
            (await ReadStockAsync(pid)).reserved.Should().Be(0, "on kosul: ilk serbest birakma calismali");

            await mgr.ReleaseReservation(oid);

            var after = await ReadStockAsync(pid);
            after.reserved.Should().Be(0, "rezerve negatife dusmemeli");
            after.physical.Should().Be(10);
            after.available.Should().Be(10, "cift serbest birakma HAYALET stok uretmemeli");
        }

        [Fact]
        public async Task BedenIzolasyonu_MBedenindekiRezervasyon_LBedeniniEtkilemez()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10, "M", "L");
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 6, orderId: oid)).Item2.Success.Should().BeTrue();

            var m = await ReadStockAsync(pid, "M");
            var l = await ReadStockAsync(pid, "L");
            m.reserved.Should().Be(6, "M bedeninde rezervasyon olusmali (vakum engeli)");
            m.available.Should().Be(4);
            l.physical.Should().Be(10, "L bedeninin fizikseli DEGISMEMELI");
            l.reserved.Should().Be(0, "L bedeninde rezerve OLUSMAMALI");
            l.available.Should().Be(10);
        }

        [Fact]
        public async Task AdjustStock_NegatifMiktar_Reddedilir_SayaclarDegismez()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            // POZITIF KONTROL: harness canli mi?
            var ok = await mgr.AdjustStock(pid, "M", 12, "pozitif kontrol");
            ok.Item2.Success.Should().BeTrue();
            (await ReadStockAsync(pid)).physical.Should().Be(12);

            var bad = await mgr.AdjustStock(pid, "M", -5, "negatif deneme");

            bad.Item1.Should().Be(HttpStatusCode.BadRequest);
            bad.Item2.Success.Should().BeFalse();
            bad.Item2.Message.Should().NotBeNullOrWhiteSpace("yalniz statu koduna guvenilmez - mesaj da dolu olmali");
            (await ReadStockAsync(pid)).physical.Should().Be(12, "reddedilen ayarlama stogu DEGISTIRMEMELI");
        }

        [Fact]
        public async Task AdjustStock_RezerveAltina_Reddedilir()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 6, orderId: oid)).Item2.Success.Should().BeTrue();

            var bad = await mgr.AdjustStock(pid, "M", 2, "rezerve altina indirme denemesi");

            bad.Item1.Should().Be(HttpStatusCode.BadRequest);
            bad.Item2.Success.Should().BeFalse();
            var after = await ReadStockAsync(pid);
            after.physical.Should().Be(10, "rezerve altina indirme REDDEDILMELI");
            after.reserved.Should().Be(6);
            after.available.Should().Be(4);
        }

        [Fact]
        public async Task AdjustStock_Basarili_HareketKaydiDogruTipVeMiktar()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var res = await mgr.AdjustStock(pid, "M", 25, "yeni sevkiyat");

            res.Item2.Success.Should().BeTrue();
            var after = await ReadStockAsync(pid);
            after.physical.Should().Be(25);
            after.reserved.Should().Be(0);
            after.available.Should().Be(25, "ayarlama sonrasi musait tutarli olmali");

            var mv = await MovementsAsync(pid);
            mv.Should().ContainSingle(m => m.movement_type == (byte)StockMovementType.Adjustment,
                "ayarlama Adjustment tipinde TEK hareket yazmali");
            // DALGA-2-FIX (B11): gerekce metni DUZELTILDI. Beklenen DEGER degismedi (bu senaryo bir
            // ARTIS: 10 -> 25), ama "mutlak fark" ifadesi artik YANLIS olurdu - defter azalisi
            // NEGATIF yaziyor. Isaretli davranisin kendisi LedgerAndRevenueSpecTests'te pinli.
            mv.Single(m => m.movement_type == (byte)StockMovementType.Adjustment).quantity
                .Should().Be(15, "hareket miktari ISARETLI fark olmali (25 - 10 = +15)");
        }

        [Fact]
        public async Task IptalSonrasi_IncreaseStock_FizikselArtar_InHareketiYazar()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            // Odemesi onaylanmis siparis: rezervasyon onaylanmis, fiziksel dusmus
            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 4, orderId: oid)).Item2.Success.Should().BeTrue();
            await mgr.ConfirmReservation(oid);
            (await ReadStockAsync(pid)).physical.Should().Be(6, "on kosul: onay fizikseli dusurmus olmali");

            var inc = await mgr.IncreaseStock(pid, "M", 4, referenceId: oid);

            inc.Item2.Success.Should().BeTrue();
            var after = await ReadStockAsync(pid);
            after.physical.Should().Be(10, "iptalde fiziksel stok geri donmeli");
            after.reserved.Should().Be(0);
            after.available.Should().Be(10);
            (await MovementsAsync(pid)).Should().Contain(m =>
                m.movement_type == (byte)StockMovementType.In && m.quantity == 4, "iade bir In hareketi yazmali");
        }

        [Fact]
        public async Task KismiIptal_IkiKalemdenBiriIadeEdilir_SayaclarTutarli()
        {
            if (Skipped()) return;
            var pid = await NewProductWithStockAsync(10);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            // 5 adetlik siparis onaylanmis -> fiziksel 5
            var oid = await GercekSiparisAsync(ctx);   // D-SEMA-FIX: uydurma id yerine GERCEK siparis
            (await mgr.ReserveStock(pid, "M", 5, orderId: oid)).Item2.Success.Should().BeTrue();
            await mgr.ConfirmReservation(oid);
            (await ReadStockAsync(pid)).physical.Should().Be(5, "on kosul");

            // KISMI iptal: 5 adetten yalniz 2 adet iade
            (await mgr.IncreaseStock(pid, "M", 2, referenceId: oid)).Item2.Success.Should().BeTrue();

            var after = await ReadStockAsync(pid);
            after.physical.Should().Be(7, "kismi iadede YALNIZ iade edilen adet geri donmeli (5 + 2)");
            after.reserved.Should().Be(0);
            after.available.Should().Be(7);
            (await MovementsAsync(pid)).Count(m => m.movement_type == (byte)StockMovementType.In)
                .Should().Be(1, "kismi iade tek In hareketi yazmali");
        }
    }
}
