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
    // Açıklayıcı yorum: Mağaza kredisi - bakiye + defter. En kritik test paralel atomiklik:
    // UseCredit "UPDATE ... WHERE store_credit >= amount" ile TOCTOU yarışını kapatmalı.
    // Aciklayici yorum: GERCEK SQL gerektirir - ci.yml adanmis adimi bu trait ile suzuyor.
    [Trait("Category", "Sql")]
    public class StoreCreditTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaCreditTest";

        // Açıklayıcı yorum: Her cagri KENDI DbContext'ini alir - EF DbContext thread-safe DEGIL,
        // paralel testte ortak context kullanmak yarisi olcmek yerine EF'i patlatirdi.
        private (StoreCreditManager mgr, DivisimaDbContext ctx) NewManager()
        {
            var ctx = NewContext();
            var mgr = new StoreCreditManager(new EfCustomerDal(ctx), new EfStoreCreditTransactionDal(ctx), new UnitOfWork(ctx));
            return (mgr, ctx);
        }

        private async Task<int> LedgerCountAsync(int customerId, LedgerEntryTypeEnum type)
        {
            await using var ctx = NewContext();
            return await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == customerId && t.type == (byte)type);
        }

        [Fact]
        public async Task AddCredit_BakiyeyiArtirir_VeDefterYazar()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var (mgr, ctx) = NewManager();
            await using var _ = ctx;

            var (code, result) = await mgr.AddCredit(c.id, 100m, "test-ekle", null);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            (await ReadCreditAsync(c.id)).Should().Be(100m);
            (await LedgerCountAsync(c.id, LedgerEntryTypeEnum.Earn)).Should().Be(1);
        }

        [Fact]
        public async Task UseCredit_YeterliBakiye_Duser_VeDefterYazar()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync(storeCredit: 100m);
            var (mgr, ctx) = NewManager();
            await using var _ = ctx;

            var (code, result) = await mgr.UseCredit(c.id, 30m, "test-harca", null);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            (await ReadCreditAsync(c.id)).Should().Be(70m);
            (await LedgerCountAsync(c.id, LedgerEntryTypeEnum.Redeem)).Should().Be(1);
        }

        [Fact]
        public async Task UseCredit_YetersizBakiye_Reddeder_VeBakiyeyeDOKUNMAZ()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync(storeCredit: 10m);
            var (mgr, ctx) = NewManager();
            await using var _ = ctx;

            // POZITIF KONTROL once: harness gercekten calisiyor mu?
            var ok = await mgr.UseCredit(c.id, 5m, "test-gecerli", null);
            ok.Item2.Success.Should().BeTrue("pozitif kontrol - harness canli olmali");
            (await ReadCreditAsync(c.id)).Should().Be(5m);

            // Simdi yetersiz bakiye
            var (code, result) = await mgr.UseCredit(c.id, 50m, "test-yetersiz", null);

            code.Should().Be(HttpStatusCode.BadRequest);
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Yetersiz kredi bakiyesi.", "yalniz statu koduna guvenilmez - mesaj da dogrulanir");
            (await ReadCreditAsync(c.id)).Should().Be(5m, "reddedilen harcama bakiyeyi degistirmemeli");
            (await LedgerCountAsync(c.id, LedgerEntryTypeEnum.Redeem)).Should().Be(1, "yalniz gecerli harcama defterde olmali");
        }

        [Fact]
        public async Task UseCredit_ParalelSekizIstek_AtomikKalir_BakiyeNegatifOlmaz()
        {
            if (Skipped()) return;
            const decimal start = 100m;
            const decimal each = 20m;
            const int callers = 8;                       // 8 x 20 = 160 talep > 100 bakiye

            var c = await NewCustomerAsync(storeCredit: start);

            // Açıklayıcı yorum: 8 BAGIMSIZ context/manager - gercek eszamanlilik.
            var managers = Enumerable.Range(0, callers).Select(_ => NewManager()).ToList();
            try
            {
                var results = await Task.WhenAll(managers.Select(m => m.mgr.UseCredit(c.id, each, "paralel-test", null)));
                var successCount = results.Count(r => r.Item2.Success);

                var balance = await ReadCreditAsync(c.id);
                var redeemRows = await LedgerCountAsync(c.id, LedgerEntryTypeEnum.Redeem);

                // Vakum engeli: en az bir harcama GECMELI, yoksa test hicbir sey kanitlamaz.
                successCount.Should().BeGreaterThan(0, "en az bir harcama basarili olmali");
                successCount.Should().BeLessThanOrEqualTo((int)(start / each), "100 bakiyeden 20'serlik en fazla 5 harcama gecebilir");
                balance.Should().Be(start - successCount * each, "bakiye basarili harcama sayisiyla birebir tutmali");
                balance.Should().BeGreaterThanOrEqualTo(0m, "bakiye ASLA negatife dusmemeli");
                redeemRows.Should().Be(successCount, "defter kaydi sayisi basarili harcama sayisina esit olmali");
            }
            finally
            {
                foreach (var m in managers) await m.ctx.DisposeAsync();
            }
        }
    }
}
