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
    // Aciklayici yorum: Referans odulu. Odul LEDGER uzerinden idempotent - ayni olay iki kez
    // odul yazmaz ve iptal ledger kaydini silmedigi icin farming kapali.
    [Trait("Category", "Sql")]
    public class ReferralRewardTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaReferralTest";
        private const string RefereeReason = "Referans odulu (davet edilen)";

        private async Task<T> WithManagerAsync<T>(Func<ReferralManager, Task<T>> f)
        {
            var ctx = NewContext();
            await using (ctx)
            {
                var mgr = new ReferralManager(new EfCustomerDal(ctx), new EfOrderDal(ctx),
                    new EfStoreCreditTransactionDal(ctx), new UnitOfWork(ctx));
                return await f(mgr);
            }
        }

        private async Task<int> RewardRowsAsync(int customerId)
        {
            await using var ctx = NewContext();
            return await ctx.Set<StoreCreditTransaction>()
                .CountAsync(t => t.customer_id == customerId && t.reason.Contains("Referans"));
        }

        // Aciklayici yorum: Kayit akisi referral_code URETMIYOR (bilinen uretim boslugu - rapora yazildi).
        // Test akisi sinamak icin kodu KENDI seed ediyor; boslugun kendisi bulgu olarak kalir.
        private async Task<(Customer referrer, Customer referee)> SeedPairAsync()
        {
            var referrer = await NewCustomerAsync();
            await using (var ctx = NewContext())
            {
                var r = await ctx.Set<Customer>().SingleAsync(x => x.id == referrer.id);
                r.referral_code = "REF" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
                await ctx.SaveChangesAsync();
            }
            var referee = await NewCustomerAsync();
            await using (var ctx = NewContext())
            {
                var e = await ctx.Set<Customer>().SingleAsync(x => x.id == referee.id);
                e.referred_by = referrer.id;
                await ctx.SaveChangesAsync();
            }
            return (referrer, referee);
        }

        [Fact]
        public async Task IlkOdenmisSiparis_IkiTarafaDaKrediYazar()
        {
            if (Skipped()) return;
            var (referrer, referee) = await SeedPairAsync();
            await NewOrderAsync(referee.id, 300m, status: (byte)OrderStatusEnum.Confirmed);

            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, 1); return 0; });

            (await ReadCreditAsync(referrer.id)).Should().Be(50m, "davet eden odul almali");
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "davet edilen odul almali");
            (await RewardRowsAsync(referee.id)).Should().BeGreaterThan(0, "odul defterde iz birakmali");
        }

        [Fact]
        public async Task Odul_LEDGER_IDEMPOTENT_IkinciCagriKrediYazmaz()
        {
            if (Skipped()) return;
            var (referrer, referee) = await SeedPairAsync();
            await NewOrderAsync(referee.id, 300m, status: (byte)OrderStatusEnum.Confirmed);

            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, 1); return 0; });
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "on kosul: ilk odul verilmis olmali");
            var rowsAfterFirst = await RewardRowsAsync(referee.id);

            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, 2); return 0; });

            (await ReadCreditAsync(referrer.id)).Should().Be(50m, "ikinci cagri davet edene TEKRAR odul yazmamali");
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "ikinci cagri davet edilene TEKRAR odul yazmamali");
            (await RewardRowsAsync(referee.id)).Should().Be(rowsAfterFirst, "ikinci defter kaydi olusmamali");
        }

        [Fact]
        public async Task OdenmisSiparisYokken_OdulVerilmez()
        {
            if (Skipped()) return;
            var (referrer, referee) = await SeedPairAsync();
            await NewOrderAsync(referee.id, 300m, status: (byte)OrderStatusEnum.Pending);

            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, 1); return 0; });
            (await ReadCreditAsync(referee.id)).Should().Be(0m, "odenmemis siparis odul tetiklememeli");

            // POZITIF KONTROL: siparis odenmise donunce odul GELMELI (harness canli)
            await NewOrderAsync(referee.id, 300m, status: (byte)OrderStatusEnum.Delivered);
            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, 2); return 0; });
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "odenmis siparis sonrasi odul verilmeli");
        }

        [Fact]
        public async Task IptalSonrasi_ClawbackYOK_MevcutDavranisPinlenir()
        {
            if (Skipped()) return;
            var (referrer, referee) = await SeedPairAsync();
            var order = await NewOrderAsync(referee.id, 300m, status: (byte)OrderStatusEnum.Confirmed);
            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, order.id); return 0; });
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "on kosul: odul verilmis olmali");

            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().SingleAsync(x => x.id == order.id);
                o.status = (byte)OrderStatusEnum.Cancelled;
                await ctx.SaveChangesAsync();
            }

            // PINLENEN DAVRANIS: referee ilk siparisi IPTAL edilse bile odul GERI ALINMIYOR.
            // Bu bilinen bir acik (rapora yazildi). Ledger kalici oldugu icin farming de kapali:
            // ikinci bir siparisle odul TEKRAR alinamaz. Davranis degisirse test kirmizi olur.
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "iptal sonrasi clawback YOK - mevcut davranis");
            (await ReadCreditAsync(referrer.id)).Should().Be(50m);

            await NewOrderAsync(referee.id, 400m, status: (byte)OrderStatusEnum.Delivered);
            await WithManagerAsync(async m => { await m.RewardOnFirstOrder(referee.id, 999); return 0; });
            (await ReadCreditAsync(referee.id)).Should().Be(50m, "farming kapali: ikinci siparis TEKRAR odul vermemeli");
        }
    }
}
