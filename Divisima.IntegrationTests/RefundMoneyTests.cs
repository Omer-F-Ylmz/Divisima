using Divisima.Bussiness.Concrete;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: RefundToSourceAsync - MERKEZİ para iadesi. Gerçek SQL + gerçek EF DAL'ları.
    // Iyzico dış servis olduğu için sahte; para yolunun DB tarafı gerçek.
    // Aciklayici yorum: GERCEK SQL gerektirir - ci.yml adanmis adimi bu trait ile suzuyor.
    [Trait("Category", "Sql")]
    public class RefundMoneyTests : SqlBackedTestBase
    {
        protected override string DatabaseName => "DivisimaRefundTest";

        private sealed class FakeIyzico : IIyzicoClient
        {
            public bool RefundSucceeds = true;
            public decimal LastRefundAmount = -1m;
            public Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount)
            {
                LastRefundAmount = amount;
                return Task.FromResult(new IyzicoRefundResult { Success = RefundSucceeds, RefundId = "rf-test" });
            }
            public Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request)
                => throw new NotSupportedException("Para testlerinde kullanilmaz.");
            public Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token)
                => throw new NotSupportedException("Para testlerinde kullanilmaz.");
            public bool VerifyCallbackSignature(string token, string signature) => true;
        }

        private (RefundManager mgr, FakeIyzico iyzico, DivisimaDbContext ctx) NewManager()
        {
            var ctx = NewContext();
            var iyz = new FakeIyzico();
            var mgr = new RefundManager(new EfPaymentDal(ctx), iyz, new EfCustomerDal(ctx), new EfStoreCreditTransactionDal(ctx), new EfOrderDal(ctx));
            return (mgr, iyz, ctx);
        }

        private async Task<int> LedgerCountAsync(int customerId)
        {
            await using var ctx = NewContext();
            return await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == customerId);
        }

        [Fact]
        public async Task Refund_KartsizCODSiparis_TamamiStoreCredite()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 120m);          // Payment satiri YOK -> kart yok

            var (mgr, iyz, ctx) = NewManager();
            await using var _ = ctx;
            var outcome = await mgr.RefundToSourceAsync(o, 120m, "test-cod-iade");

            outcome.Success.Should().BeTrue();
            outcome.CreditRefunded.Should().Be(120m, "kart yoksa iadenin TAMAMI store credit'e gitmeli");
            outcome.OnlineRefunded.Should().Be(0m);
            iyz.LastRefundAmount.Should().Be(-1m, "kart olmadan Iyzico'ya iade cagrisi YAPILMAMALI");
            (await ReadCreditAsync(c.id)).Should().Be(120m);
            (await LedgerCountAsync(c.id)).Should().Be(1, "iade defterde iz birakmali");
        }

        [Fact]
        public async Task Refund_SiparisToplaminiASAMAZ_UstSinirUygulanir()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 120m);

            var (mgr, _, ctx) = NewManager();
            await using var __ = ctx;
            var outcome = await mgr.RefundToSourceAsync(o, 999m, "test-fazla-iade");   // toplamdan COK fazla

            outcome.Success.Should().BeTrue();
            outcome.CreditRefunded.Should().Be(120m, "iade siparis toplamina KIRPILMALI (para sizmasi engeli)");
            (await ReadCreditAsync(c.id)).Should().Be(120m, "bakiye 999 degil 120 artmali");
        }

        [Fact]
        public async Task Refund_KartliSiparis_KartVeCuzdanPayinaBOLUNUR()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 120m, storeCreditUsed: 40m, onlinePaid: true);
            await using (var seed = NewContext())
            {
                seed.Set<Payment>().Add(new Payment
                {
                    order_id = o.id, payment_provider = "iyzico",
                    payment_status = (byte)PaymentStatusEnum.Success,
                    amount = 120m, transaction_id = "tx-test", item_transaction_id = "itx-test", created_at = DateTime.Now
                });
                await seed.SaveChangesAsync();
            }

            var (mgr, iyz, ctx) = NewManager();
            await using var _ = ctx;
            var outcome = await mgr.RefundToSourceAsync(o, 120m, "test-kart-iade");

            outcome.Success.Should().BeTrue();
            // online orani = (120 - 40) / 120 -> 120 * 0.6667 = 80
            outcome.OnlineRefunded.Should().Be(80m, "kartla odenen pay Iyzico'ya donmeli");
            outcome.CreditRefunded.Should().Be(40m, "cuzdanla odenen pay store credit'e donmeli");
            iyz.LastRefundAmount.Should().Be(80m, "Iyzico'ya SADECE kart payi gitmeli");
            (await ReadCreditAsync(c.id)).Should().Be(40m, "cuzdan bakiyesi yalniz cuzdan payi kadar artmali");
        }

        [Fact]
        public async Task Refund_IyzicoBasarisizsa_TumIslemBasarisiz_VeCuzdanaDOKUNULMAZ()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 100m, storeCreditUsed: 20m, onlinePaid: true);
            await using (var seed = NewContext())
            {
                seed.Set<Payment>().Add(new Payment
                {
                    order_id = o.id, payment_provider = "iyzico",
                    payment_status = (byte)PaymentStatusEnum.Success,
                    amount = 100m, transaction_id = "tx-fail", item_transaction_id = "itx-fail", created_at = DateTime.Now
                });
                await seed.SaveChangesAsync();
            }

            var (mgr, iyz, ctx) = NewManager();
            await using var _ = ctx;
            iyz.RefundSucceeds = false;
            var outcome = await mgr.RefundToSourceAsync(o, 100m, "test-iyzico-hata");

            outcome.Success.Should().BeFalse("kart iadesi basarisizsa cagiran rollback edebilmeli");
            (await ReadCreditAsync(c.id)).Should().Be(0m, "kart iadesi basarisizken cuzdana kredi YAZILMAMALI");
            (await LedgerCountAsync(c.id)).Should().Be(0, "basarisiz iade defter kaydi birakmamali");
        }

        [Fact]
        public async Task Refund_SifirTutar_NoOp_AmaAyniHarnesteGercekIadeCalisir()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 50m);

            var (mgr, _, ctx) = NewManager();
            await using var __ = ctx;

            // 1) sifir tutar -> mesru no-op
            var noop = await mgr.RefundToSourceAsync(o, 0m, "test-sifir");
            noop.Success.Should().BeTrue();
            (await ReadCreditAsync(c.id)).Should().Be(0m);
            (await LedgerCountAsync(c.id)).Should().Be(0);

            // 2) POZITIF KONTROL: ayni harness'te gercek iade calisiyor mu? (vakum gecis engeli -
            //    yukaridaki iddialar harness bozuk olsa da yesil kalabilirdi)
            var real = await mgr.RefundToSourceAsync(o, 50m, "test-gercek");
            real.Success.Should().BeTrue();
            (await ReadCreditAsync(c.id)).Should().Be(50m, "gercek iade bakiyeyi artirmali");
            (await LedgerCountAsync(c.id)).Should().Be(1);
        }

        // SPRINT 6 - KUMULATIF SAYAC BAYAT NESNEYE KURBAN GITMEZ.
        // refunded_amount atomik UPDATE ile (ExecuteUpdateAsync) artiyor; bu change-tracker'i ATLAR.
        // Cagiran elindeki Order nesnesini tam-varlik olarak guncellerse (UpdateAsync tum kolonlari
        // yazar) sayac SIFIRA donerdi ve kumulatif sinir sessizce kaybolurdu. Bu test o yolu surer.
        [Fact]
        public async Task KumulatifSayac_CagiranSiparisiGuncellese_de_KAYBOLMAZ()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 120m);

            var (mgr, _, ctx) = NewManager();
            await using var __ = ctx;
            var orderDal = new EfOrderDal(ctx);

            var ilk = await mgr.RefundToSourceAsync(o, 50m, "kismi-iade");
            ilk.Success.Should().BeTrue("kismi iade calismali");
            ilk.CreditRefunded.Should().Be(50m);

            // Cagiran, ELINDEKI nesne uzerinden siparisi guncelliyor (gercek iptal akisinin yaptigi sey).
            o.status = (byte)OrderStatusEnum.Cancelled;
            await orderDal.UpdateAsync(o);

            await using (var fresh = NewContext())
                (await fresh.Set<Order>().AsNoTracking().SingleAsync(x => x.id == o.id)).refunded_amount
                    .Should().Be(50m, "tam-varlik guncellemesi kumulatif sayaci SIFIRLAMAMALI");

            // CIFT-ANLAM KIRICI: sayac yalniz "duruyor" degil, SINIR olarak da isliyor.
            var ikinci = await mgr.RefundToSourceAsync(o, 999m, "kalan-iade");
            ikinci.Success.Should().BeTrue();
            ikinci.CreditRefunded.Should().Be(70m, "kalan hak (120-50) kadar kirpilmali");
            (await ReadCreditAsync(c.id)).Should().Be(120m,
                "toplam bakiye artisi siparis tutarini ASMAMALI");

            var ucuncu = await mgr.RefundToSourceAsync(o, 10m, "hak-bitti");
            ucuncu.Success.Should().BeFalse("hak tukendi - ucuncu iade REDDEDILMELI");
            (await ReadCreditAsync(c.id)).Should().Be(120m, "reddedilen iade bakiyeyi degistirmemeli");
        }

        // ── E2b) KIMLIK YOKSA IADE SESSIZCE CUZDANA KAYMAZ ─────────────────────────────
        //
        // Kartla odenmis bir siparisin iadesini magaza kredisine cevirmek, musteriye parasini
        // GERI VERMEMEK demektir. Eski kayitlarda (E2b oncesi) item_transaction_id yok; o
        // odemeler API uzerinden iade EDILEMEZ ve bu GURULTULU olmali - operasyon Iyzico
        // panelinden elle iade edebilsin diye cagiran zaman cizelgesine KRITIK not duser.
        [Fact]
        public async Task Refund_KirilimKimligi_YOKSA_GURULTULU_DUSER_CuzdanaKAYMAZ()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 100m, onlinePaid: true);
            await using (var seed = NewContext())
            {
                seed.Set<Payment>().Add(new Payment
                {
                    order_id = o.id, payment_provider = "iyzico",
                    payment_status = (byte)PaymentStatusEnum.Success,
                    amount = 100m, transaction_id = "tx-eski",   // item_transaction_id YOK (eski kayit)
                    created_at = DateTime.Now
                });
                await seed.SaveChangesAsync();
            }

            var (mgr, iyz, ctx) = NewManager();
            await using var _ = ctx;
            var outcome = await mgr.RefundToSourceAsync(o, 100m, "kimliksiz-eski-kayit");

            outcome.Success.Should().BeFalse("kirilim kimligi olmadan iade YAPILAMAZ");
            iyz.LastRefundAmount.Should().Be(-1m, "saglayiciya HIC cagri gitmemeli - yanlis kimlikle deneme yok");
            (await ReadCreditAsync(c.id)).Should().Be(0m,
                "kart iadesi yapilamiyorsa TUTAR SESSIZCE MAGAZA KREDISINE CEVRILMEMELI");
            (await LedgerCountAsync(c.id)).Should().Be(0, "yapilmayan iade defterde iz birakmamali");

            await using var oku = NewContext();
            (await oku.Set<Order>().AsNoTracking().SingleAsync(x => x.id == o.id)).refunded_amount
                .Should().Be(0m, "para gitmedi - iade hakki tuketilmis sayilmamali");
        }

        // ── E2b/B2) SERBEST BIRAKILAN HAK SONRAKI SaveChanges'TE GERI YAZILMAZ ─────────
        //
        // OLCULEN ZARAR: ExecuteUpdateAsync change-tracker'i ATLAR. Saglayici reddettiginde
        // ReleaseRefundedAmountAsync DB'de hakki serbest birakiyordu, ama CAGIRANIN elindeki
        // IZLENEN Order nesnesi hala +granted tasiyordu; iadeden sonra kosan herhangi bir
        // SaveChanges (zaman cizelgesi, sadakat geri alma, fatura iptali) bayat degeri GERI
        // YAZIYORDU. Olculdu: serbestBirakma=0,00 bellek=100,00 saveChanges=100,00.
        // Sonuc: basarisiz bir iade musterinin iade hakkini KALICI tuketiyordu.
        //
        // Bu pin canli yolun sartini birebir kurar: siparis manager'in context'inde IZLENIYOR
        // ve iadeden SONRA bir SaveChanges kosuyor. S6 pini (SaglayiciIadesi_..._BLOKE_KALMAZ)
        // bunu goremiyordu cunku her cagriyi AYRI scope'ta yapiyor ve order orada DETACHED.
        [Fact]
        public async Task Refund_SaglayiciReddedince_SerbestBirakilanHak_SonrakiSaveChangesTe_GERI_YAZILMAZ()
        {
            if (Skipped()) return;
            var c = await NewCustomerAsync();
            var o = await NewOrderAsync(c.id, total: 100m, onlinePaid: true);
            await using (var seed = NewContext())
            {
                seed.Set<Payment>().Add(new Payment
                {
                    order_id = o.id, payment_provider = "iyzico",
                    payment_status = (byte)PaymentStatusEnum.Success,
                    amount = 100m, transaction_id = "tx-b2", item_transaction_id = "itx-b2",
                    created_at = DateTime.Now
                });
                await seed.SaveChangesAsync();
            }

            await using var ctx = NewContext();
            // CANLI YOLUN SARTI: siparis manager'in kullandigi context'te IZLENIYOR
            // (OrderManager.ChangeOrderStatus da GetAsync ile izlenen nesne aliyor).
            var izlenen = await ctx.Set<Order>().SingleAsync(x => x.id == o.id);
            var iyz = new FakeIyzico { RefundSucceeds = false };
            var mgr = new RefundManager(new EfPaymentDal(ctx), iyz, new EfCustomerDal(ctx),
                new EfStoreCreditTransactionDal(ctx), new EfOrderDal(ctx));

            var outcome = await mgr.RefundToSourceAsync(izlenen, 100m, "b2-saglayici-hatasi");
            outcome.Success.Should().BeFalse("saglayici reddetti");
            iyz.LastRefundAmount.Should().Be(100m, "POZITIF OLAY: saglayiciya gercekten cagri gitti");

            // Canli akista iadeden SONRA yan etkiler kosuyor ve AYNI context uzerinden
            // SaveChanges tetikliyor. Bayat deger geri yazilmamali.
            await ctx.SaveChangesAsync();

            await using var oku = NewContext();
            (await oku.Set<Order>().AsNoTracking().SingleAsync(x => x.id == o.id)).refunded_amount
                .Should().Be(0m, "SaveChanges sonrasi da hak SERBEST kalmali - musteri iade hakkini KAYBETMEMELI");
            izlenen.refunded_amount.Should().Be(0m, "bellekteki deger de esitlenmeli - bayat kalirsa geri yazilir");
        }
    }
}
