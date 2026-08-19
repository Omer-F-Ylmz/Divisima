using System.Net;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Sadakat puanı - siparişte kazanım, iptalde geri alım.
    // Kritik iki özellik: ReverseForOrder IDEMPOTENT (çift geri alma yok) ve CLAMP'li (bakiye negatif olmaz).
    // Aciklayici yorum: GERCEK SQL gerektirir - ci.yml adanmis adimi bu trait ile suzuyor.
    [Trait("Category", "Sql")]
    public class LoyaltyReversalTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaLoyaltyTest";

        private const string ReverseReason = "Sipariş iptali - puan geri alımı";

        private (LoyaltyManager mgr, DivisimaDbContext ctx) NewManager()
        {
            var ctx = NewContext();
            var mgr = new LoyaltyManager(new EfCustomerDal(ctx), new EfOrderDal(ctx),
                new EfLoyaltyTransactionDal(ctx), new EfStoreCreditTransactionDal(ctx), new UnitOfWork(ctx));
            return (mgr, ctx);
        }

        // Açıklayıcı yorum: HER cagri KENDI context'inde kosar. Uretimde Earn (odeme isteği) ve
        // Reverse (iptal isteği) AYRI scope'larda olur. Ayni context'te kosturmak gercekci degil
        // ve ayrica bayat-izleme tuzagina duser (bkz. rapor: ExecuteUpdate change tracker'i atlar).
        private async Task<T> WithManagerAsync<T>(Func<LoyaltyManager, Task<T>> f)
        {
            var (mgr, ctx) = NewManager();
            await using (ctx) return await f(mgr);
        }
        private async Task<int> ReverseRowCountAsync(int customerId, int orderId)
        {
            await using var ctx = NewContext();
            return await ctx.Set<LoyaltyTransaction>().CountAsync(t =>
                t.customer_id == customerId && t.order_id == orderId &&
                t.type == (byte)LedgerEntryTypeEnum.Redeem && t.reason == ReverseReason);
        }

        [Fact]
        public async Task EarnFromOrder_PuanKazandirir_VeDefterYazar()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 250m);
            var (code, result) = await WithManagerAsync(m => m.EarnFromOrder(c.id, 250m, o.id));

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            // 250 / 10 = 25 taban puan; teslim edilmis siparis yok -> en dusuk kademe carpani
            var points = await ReadPointsAsync(c.id);
            points.Should().BeGreaterThan(0, "siparisten puan KAZANILMALI (vakum engeli)");
            points.Should().Be(25, "250 TL / 10 = 25 taban puan (kademe carpani 1x)");
        }

        [Fact]
        public async Task ReverseForOrder_IDEMPOTENT_IkiKezCagrilinca_CiftGeriAlmaYok()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 250m);
            await WithManagerAsync(m => m.EarnFromOrder(c.id, 250m, o.id));
            (await ReadPointsAsync(c.id)).Should().Be(25, "on kosul: puan kazanilmis olmali");

            var first = await WithManagerAsync(m => m.ReverseForOrder(c.id, o.id));
            first.Item2.Success.Should().BeTrue();
            (await ReadPointsAsync(c.id)).Should().Be(0, "ilk geri alim puani sifirlamali");
            (await ReverseRowCountAsync(c.id, o.id)).Should().Be(1);

            // Ikinci cagri - hicbir sey degismemeli
            var second = await WithManagerAsync(m => m.ReverseForOrder(c.id, o.id));
            second.Item2.Success.Should().BeTrue();
            (await ReadPointsAsync(c.id)).Should().Be(0, "ikinci geri alim bakiyeyi DEGISTIRMEMELI");
            (await ReverseRowCountAsync(c.id, o.id)).Should().Be(1, "ikinci cagri IKINCI defter kaydi yazMAMALI");
        }

        [Fact]
        public async Task ReverseForOrder_CLAMP_PuanHarcanmissa_BakiyeNegatifOlmaz()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 250m);
            await WithManagerAsync(m => m.EarnFromOrder(c.id, 250m, o.id));
            (await ReadPointsAsync(c.id)).Should().Be(25);

            // Musteri puanlarin cogunu harcadi -> bakiye 10'a dustu
            await using (var spend = NewContext())
            {
                var cust = await spend.Set<Customer>().SingleAsync(x => x.id == c.id);
                cust.loyalty_points = 10;
                await spend.SaveChangesAsync();
            }

            var rev = await WithManagerAsync(m => m.ReverseForOrder(c.id, o.id));

            rev.Item2.Success.Should().BeTrue();
            var after = await ReadPointsAsync(c.id);
            after.Should().Be(0, "yalniz MEVCUT bakiye kadar geri alinmali (25 degil 10)");
            after.Should().BeGreaterThanOrEqualTo(0, "bakiye ASLA negatife dusmemeli");
            (await ReverseRowCountAsync(c.id, o.id)).Should().Be(1, "geri alim defterde iz birakmali");
        }

        [Fact]
        public async Task ReverseForOrder_KazanimYoksa_NoOp_AmaHarnessCanli()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var bos = await NewOrderAsync(c.id, total: 250m);   // bu siparis icin KAZANIM yok
            var dolu = await NewOrderAsync(c.id, total: 250m);
            var noop = await WithManagerAsync(m => m.ReverseForOrder(c.id, bos.id));
            noop.Item2.Success.Should().BeTrue();
            (await ReadPointsAsync(c.id)).Should().Be(0);
            (await ReverseRowCountAsync(c.id, bos.id)).Should().Be(0, "kazanim yokken geri alim kaydi olusmamali");

            // POZITIF KONTROL: ayni harness'te gercek kazanim + geri alim calisiyor mu?
            await WithManagerAsync(m => m.EarnFromOrder(c.id, 250m, dolu.id));
            (await ReadPointsAsync(c.id)).Should().Be(25, "pozitif kontrol - kazanim calismali");
            await WithManagerAsync(m => m.ReverseForOrder(c.id, dolu.id));
            (await ReverseRowCountAsync(c.id, dolu.id)).Should().Be(1, "pozitif kontrol - geri alim calismali");
        }
    }
}
