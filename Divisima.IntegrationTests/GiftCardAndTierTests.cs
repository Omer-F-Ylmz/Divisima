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
    // Aciklayici yorum: Hediye karti. SEMANTIK koddan cikarildi: Redeem TAM BAKIYEYI bozdurur
    // (amount = card.balance) ve TryRedeemAsync compare-and-swap ile karti sifirlar. Yani kart
    // TEK KULLANIMLIK - eszamanli 8 istekte TAM 1 basari beklenir, digerleri Conflict alir.
    [Trait("Category", "Sql")]
    public class GiftCardTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaGiftTest";

        private (GiftCardManager mgr, DivisimaDbContext ctx) NewManager()
        {
            var ctx = NewContext();
            var mgr = new GiftCardManager(new EfGiftCardDal(ctx), new EfCustomerDal(ctx),
                new EfStoreCreditTransactionDal(ctx), new UnitOfWork(ctx));
            return (mgr, ctx);
        }

        private async Task<GiftCard> NewCardAsync(decimal amount)
        {
            await using var ctx = NewContext();
            var g = new GiftCard
            {
                code = ("GC" + Guid.NewGuid().ToString("N").Substring(0, 10)).ToUpperInvariant(),
                initial_amount = amount,
                balance = amount,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<GiftCard>().Add(g);
            await ctx.SaveChangesAsync();
            return g;
        }

        private async Task<GiftCard> ReadCardAsync(int id)
        {
            await using var ctx = NewContext();
            return await ctx.Set<GiftCard>().AsNoTracking().SingleAsync(g => g.id == id);
        }

        [Fact]
        public async Task Redeem_BakiyeyiKrediyeCevirir_VeRedeemedByYazar()
        {
            if (Skipped()) return;
            var cust = await NewCustomerAsync();
            var card = await NewCardAsync(250m);
            var (mgr, ctx) = NewManager();
            await using var d = ctx;

            var res = await mgr.Redeem(cust.id, card.code);

            res.Item1.Should().Be(HttpStatusCode.OK);
            res.Item2.Success.Should().BeTrue();
            (await ReadCreditAsync(cust.id)).Should().Be(250m, "kart bakiyesi magaza kredisine gecmeli");
            var after = await ReadCardAsync(card.id);
            after.balance.Should().Be(0m, "tam bakiye bozdurulunca kart sifirlanmali");
            after.redeemed_by.Should().Be(cust.id, "bozduran musteri kaydedilmeli");
        }

        [Fact]
        public async Task Redeem_ZatenBozdurulmusKart_Reddedilir()
        {
            if (Skipped()) return;
            var ilk = await NewCustomerAsync();
            var ikinci = await NewCustomerAsync();
            var card = await NewCardAsync(100m);

            var (m1, c1) = NewManager();
            await using (c1) (await m1.Redeem(ilk.id, card.code)).Item2.Success
                .Should().BeTrue("pozitif kontrol - ilk bozdurma calismali");

            var (m2, c2) = NewManager();
            await using var d2 = c2;
            var res = await m2.Redeem(ikinci.id, card.code);

            res.Item2.Success.Should().BeFalse("bakiyesi biten kart tekrar bozdurulamaz");
            res.Item2.Message.Should().NotBeNullOrWhiteSpace("yalniz statu koduna guvenilmez");
            (await ReadCreditAsync(ikinci.id)).Should().Be(0m, "ikinci musteriye kredi YAZILMAMALI");
        }

        [Fact]
        public async Task Redeem_ParalelSekizIstek_TAM_BIR_Basari()
        {
            if (Skipped()) return;
            const int callers = 8;
            var card = await NewCardAsync(300m);
            var customers = new List<Customer>();
            for (int i = 0; i < callers; i++) customers.Add(await NewCustomerAsync());

            var managers = Enumerable.Range(0, callers).Select(_ => NewManager()).ToList();
            try
            {
                var results = await Task.WhenAll(managers.Select((m, i) => m.mgr.Redeem(customers[i].id, card.code)));
                var successCount = results.Count(r => r.Item2.Success);

                successCount.Should().Be(1, "kart TEK KULLANIMLIK - eszamanli 8 istekten tam biri basarili olmali");

                var after = await ReadCardAsync(card.id);
                after.balance.Should().Be(0m);
                after.redeemed_by.Should().NotBeNull("bozduran musteri kaydedilmeli");

                decimal toplamKredi = 0m;
                foreach (var c in customers) toplamKredi += await ReadCreditAsync(c.id);
                toplamKredi.Should().Be(300m, "toplam dagitilan kredi kart bakiyesini ASAMAZ ve esit olmali");
            }
            finally
            {
                foreach (var m in managers) await m.ctx.DisposeAsync();
            }
        }
    }

    // Aciklayici yorum: Sadakat KADEME CARPANI gercekten uygulaniyor mu. EarnFromOrder taban puani
    // (total/10) hesaplayip LoyaltyTierHelper.PointMultiplier ile carpar. Kademe TESLIM EDILMIS
    // siparislerin toplamindan belirlenir: Bronze 1.0x, Silver 1.2x, Gold 1.5x, Platinum 2.0x.
    [Trait("Category", "Sql")]
    public class LoyaltyMultiplierTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaTierTest";

        private async Task<T> WithManagerAsync<T>(Func<LoyaltyManager, Task<T>> f)
        {
            var ctx = NewContext();
            await using (ctx)
            {
                var mgr = new LoyaltyManager(new EfCustomerDal(ctx), new EfOrderDal(ctx),
                    new EfLoyaltyTransactionDal(ctx), new EfStoreCreditTransactionDal(ctx), new UnitOfWork(ctx));
                return await f(mgr);
            }
        }

        [Fact]
        public async Task FarkliKademedekiMusteriler_AyniSiparistenFARKLI_PuanKazanir()
        {
            if (Skipped()) return;

            // Bronze: hic teslim edilmis siparis yok -> carpan 1.0
            var bronze = await NewCustomerAsync();
            // Gold: 12000 TL teslim edilmis -> 10000-25000 araligi -> carpan 1.5
            var gold = await NewCustomerAsync();
            await NewOrderAsync(gold.id, 12000m, status: (byte)OrderStatusEnum.Delivered);

            var siparis = await NewOrderAsync(bronze.id, 250m, status: (byte)OrderStatusEnum.Confirmed);
            var goldSiparis = await NewOrderAsync(gold.id, 250m, status: (byte)OrderStatusEnum.Confirmed);

            await WithManagerAsync(async m => await m.EarnFromOrder(bronze.id, 250m, siparis.id));
            await WithManagerAsync(async m => await m.EarnFromOrder(gold.id, 250m, goldSiparis.id));

            var bronzePuan = await ReadPointsAsync(bronze.id);
            var goldPuan = await ReadPointsAsync(gold.id);

            bronzePuan.Should().Be(25, "250 / 10 = 25 taban puan, Bronze carpani 1.0");
            goldPuan.Should().Be(37, "ayni siparis Gold kademede 25 * 1.5 = 37 (asagi yuvarlanir)");
            goldPuan.Should().BeGreaterThan(bronzePuan, "KADEME CARPANI gercekten uygulanmali - kozmetik degil");
        }
    }
}
