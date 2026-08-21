using System;
using System.Threading.Tasks;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Pricing;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Merkezi iade servisi - ödeme kaynağına göre iade (kart/cüzdan). ReturnManager + OrderManager kullanır.
    public class RefundManager : IRefundService
    {
        private readonly IPaymentDal _paymentDal;
        private readonly IIyzicoClient _iyzico;
        private readonly ICustomerDal _customerDal;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IOrderDal _orderDal;

        public RefundManager(IPaymentDal paymentDal, IIyzicoClient iyzico, ICustomerDal customerDal,
            IStoreCreditTransactionDal creditTxDal, IOrderDal orderDal)
        {
            _paymentDal = paymentDal;
            _iyzico = iyzico;
            _customerDal = customerDal;
            _creditTxDal = creditTxDal;
            _orderDal = orderDal;
        }

        public async Task<RefundOutcome> RefundToSourceAsync(Order order, decimal refundAmount, string reason)
        {
            // SOZLESME SERTLESTIRME (H53): "order == null" ile "iade edilecek tutar yok" AYNI SEY DEGIL.
            // Oncesinde ikisi de Success=TRUE donuyordu -> cagiran, para gitmedigi halde islemi BASARILI sayip
            // iadeyi Completed isaretliyor, stogu geri yukluyordu (sessiz para kaybi). Finansal yolda
            // "bulunamayan siparis" bir HATADIR, no-op degil.
            if (order == null)
                return RefundOutcome.Fail();
            if (refundAmount <= 0)
                return new RefundOutcome { Success = true };   // gercekten iade edilecek tutar yok (mesru no-op)

            // KUMULATIF SINIR (S6): eskiden burada TEK CAGRI icin kirpma vardi (refundAmount > total_price
            // ise kirp). Ardisik cagrilarin TOPLAMI takip EDILMIYORDU - olculdu: iki ardisik tam iade
            // siparis tutarinin IKI KATINI geri odedi ve Iyzico'ya da iki kez iade gitti.
            // Artik kalan iade hakki orders.refunded_amount uzerinden ATOMIK rezerve edilir; hicbir
            // yol (eszamanli dahil) toplamda total_price'i asamaz. Rezervasyon SAGLAYICI CAGRISINDAN
            // ONCE yapilir - "once para gonder sonra hesapla" sirasi fazla iadeyi engelleyemezdi.
            var granted = await ReserveRefundQuotaAsync(order.id, refundAmount);
            if (granted <= 0)
                return RefundOutcome.Fail();   // iade hakki tukendi - cagiran islemi geri almali
            refundAmount = granted;

            // BAYAT NESNE TUZAGI: ExecuteUpdateAsync change-tracker'i ATLAR. Cagiranin elindeki
            // Order ornegi izleniyorsa refunded_amount'i hala ESKI degerde tutar; o nesne uzerinden
            // yapilacak bir tam-varlik guncellemesi (UpdateAsync tum kolonlari yazar) sayaci SIFIRA
            // geri dondururdu - kumulatif sinir sessizce kaybolurdu. Bellekteki degeri de esitliyoruz.
            order.refunded_amount += granted;

            // Açıklayıcı yorum: Ödeme kaynağına göre böl (kart payı + cüzdan payı)
            var (onlineRefund, creditRefund) = PricingHelper.SplitRefund(order.total_price, order.store_credit_used, refundAmount);

            // Açıklayıcı yorum: İade edilecek KART var mı? COD/havale (nakit) siparişte online ödeme kaydı yoktur ->
            // TÜM iade store credit'e (aksi halde nakit kısım kaybolurdu).
            var payment = await _paymentDal.GetAsync(p => p.order_id == order.id && p.payment_status == (byte)PaymentStatusEnum.Success);
            bool hasCard = payment != null && !string.IsNullOrEmpty(payment.transaction_id);
            if (!hasCard)
            {
                creditRefund = refundAmount;
                onlineRefund = 0m;
            }

            var outcome = new RefundOutcome { Success = true, OnlineRefunded = onlineRefund, CreditRefunded = creditRefund };

            // 1) Kart iadesi (Iyzico)
            if (hasCard && onlineRefund > 0)
            {
                // E2b - DOGRU KIMLIK: Iyzico refund odeme KIRILIMININ (itemTransaction)
                // kimligini ister; paymentId ile cagrilirsa "Bu isyerine ait odeme kirilim kaydi
                // bulunamadi" ile REDDEDER (gercek sandbox turunda olculdu).
                // KIMLIK YOKSA SESSIZCE CUZDANA KAYDIRMIYORUZ: kartla odenmis bir siparisin
                // iadesini magaza kredisine cevirmek musterinin parasini geri VERMEMEK demektir.
                // Gurultulu basarisizlik dogru davranis - cagiran zaman cizelgesine KRITIK not duser.
                if (string.IsNullOrWhiteSpace(payment.item_transaction_id))
                {
                    await _orderDal.ReleaseRefundedAmountAsync(order.id, granted);
                    order.refunded_amount -= granted;   // B2: bellekteki bayat deger de geri alinir
                    return RefundOutcome.Fail();
                }

                var r = await _iyzico.RefundAsync(payment.item_transaction_id, onlineRefund);
                if (!r.Success)
                {
                    // Saglayiciya para GITMEDI -> tahsis edilen iade hakki bloke kalmamali.
                    await _orderDal.ReleaseRefundedAmountAsync(order.id, granted);
                    // B2 (E2b - OLCULDU): ExecuteUpdateAsync change-tracker'i ATLAR. DB'de hak
                    // serbest birakilir ama CAGIRANIN elindeki IZLENEN Order nesnesi hala
                    // +granted tasir; iadeden sonra kosan herhangi bir SaveChanges (zaman
                    // cizelgesi, sadakat geri alma, fatura iptali) bayat degeri GERI YAZAR.
                    // Olculdu: serbestBirakmaSonrasi=0,00 bellek=100,00 saveChangesSonrasi=100,00.
                    // Sonuc: basarisiz bir iade musterinin iade hakkini KALICI tuketiyordu.
                    order.refunded_amount -= granted;
                    return RefundOutcome.Fail();   // caller rollback etmeli
                }
                outcome.RefundId = r.RefundId;
            }

            // 2) Cüzdan iadesi (ATOMİK + ledger)
            if (creditRefund > 0)
            {
                // SESSIZ PARA KAYBI FIX (H54): IncrementStoreCreditAsync ETKILENEN SATIR SAYISI doner ve
                // hicbir cagiran bunu kontrol etmiyordu. Musteri satiri yoksa (KVKK ile anonimlestirilmis/
                // silinmis hesap, hatali id) guncelleme 0 satir etkiler -> BAKIYE ARTMAZ ama asagida defter
                // kaydi "X TL iade edildi" diye YAZILIR: muhasebe defteri ile gercek bakiye AYRISIR ve musteri
                // parasiz kalir. (Karsitlik: TryDecrement... her yerde kontrol ediliyordu - "para eklemek her
                // zaman basarilidir" varsayimi yanlisti.) Artik 0 satir = BASARISIZ, cagiran rollback eder.
                var credited = await _customerDal.IncrementStoreCreditAsync(order.customer_id, creditRefund);
                if (credited == 0)
                {
                    // Cuzdana YAZILAMADI -> yalniz cuzdan payini serbest birak. Kart payi (varsa) gercekten
                    // gonderildi, onun tahsisi TUKETILMIS kalir; aksi halde ayni para ikinci kez iade edilebilirdi.
                    await _orderDal.ReleaseRefundedAmountAsync(order.id, creditRefund);
                    // B2: cuzdan payi geri birakilirken bellekteki deger de esitlenir (yukaridaki
                    // gerekcenin aynisi - aksi halde sonraki SaveChanges bayat degeri geri yazar).
                    order.refunded_amount -= creditRefund;
                    return RefundOutcome.Fail();
                }
                await _creditTxDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = order.customer_id,
                    amount = creditRefund,
                    type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = reason,
                    order_id = order.id,
                    created_at = DateTime.Now
                });
            }

            return outcome;
        }

        // KALAN IADE HAKKINI ATOMIK REZERVE ET.
        // Taze anlik goruntu (NoTracking - cagiranin izledigi bayat Order nesnesi sonucu bozmasin) ->
        // kalan = total_price - refunded_amount -> istenen ile kucugu kadar CAS ile artir.
        // CAS 0 donerse araya baska bir iade girmistir; taze degerle yeniden denenir.
        // Donen deger: GERCEKTEN tahsis edilen tutar (0 = iade hakki kalmadi).
        private async Task<decimal> ReserveRefundQuotaAsync(int orderId, decimal requested)
        {
            const int maxAttempt = 3;
            for (int attempt = 0; attempt < maxAttempt; attempt++)
            {
                var snapshot = (await _orderDal.GetListNoTrackingAsync(o => o.id == orderId)).FirstOrDefault();
                if (snapshot == null)
                    return 0m;

                var remaining = snapshot.total_price - snapshot.refunded_amount;
                if (remaining <= 0)
                    return 0m;   // toplam iade zaten siparis tutarina ulasti

                var grant = Math.Min(requested, remaining);
                if (await _orderDal.TryAddRefundedAmountAsync(orderId, grant, snapshot.refunded_amount) > 0)
                    return grant;
            }
            return 0m;   // ucuncu denemede de kaybettik - iade YAPILMAZ (fazla iade riskine girilmez)
        }
    }
}
