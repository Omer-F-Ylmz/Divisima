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

        public RefundManager(IPaymentDal paymentDal, IIyzicoClient iyzico, ICustomerDal customerDal, IStoreCreditTransactionDal creditTxDal)
        {
            _paymentDal = paymentDal;
            _iyzico = iyzico;
            _customerDal = customerDal;
            _creditTxDal = creditTxDal;
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

            // SAVUNMA (FAZLA-İADE engeli): iade tutarı sipariş toplamını AŞAMAZ. Çağıranlar (iptal/return) bugün doğru tutar
            // veriyor AMA bu finansal yol için üst sınır da zorunlu - hatalı/kötü-niyetli bir çağrı toplamdan fazla iade
            // ederek (Iyzico kart iadesi + cüzdan kredisi) para SIZDIRMASIN. Tek satırlık merkezi savunma.
            if (refundAmount > order.total_price)
                refundAmount = order.total_price;

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
                var r = await _iyzico.RefundAsync(payment.transaction_id, onlineRefund);
                if (!r.Success)
                    return RefundOutcome.Fail();   // caller rollback etmeli
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
                    return RefundOutcome.Fail();
                await _creditTxDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = order.customer_id, amount = creditRefund, type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = reason, order_id = order.id, created_at = DateTime.Now
                });
            }

            return outcome;
        }
    }
}
