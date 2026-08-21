using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Abstract;

namespace Divisima.Bussiness.Events
{
    // SPRINT 8 MADDE 3 - dort yan etkinin TEK uygulayicisi. Bkz. IPaymentConfirmedSideEffects.
    public class PaymentConfirmedSideEffects : IPaymentConfirmedSideEffects
    {
        private readonly IOrderConfirmationService _orderConfirmation;
        private readonly ILoyaltyService _loyaltyService;
        private readonly IReferralService _referralService;
        private readonly ICouponDal _couponDal;

        public PaymentConfirmedSideEffects(IOrderConfirmationService orderConfirmation,
            ILoyaltyService loyaltyService, IReferralService referralService, ICouponDal couponDal)
        {
            _orderConfirmation = orderConfirmation;
            _loyaltyService = loyaltyService;
            _referralService = referralService;
            _couponDal = couponDal;
        }

        public async Task ApplyAsync(PaymentConfirmedEvent evt)
        {
            // SIRA ONEMLI DEGIL ama DETERMINISTIK tutuldu: bir teslimat yarida coktugunde
            // hangi adimlarin tamamlandigini okumak kolaylassin. Adimlar birbirinden BAGIMSIZ;
            // biri patlarsa istisna yukari gider, outbox mesaji Pending'e doner ve TAMAMI
            // yeniden calisir - idempotent olduklari icin tamamlananlar fazla etki uretmez.

            // 1) FATURA. Odeme dogrulandiktan sonra kesilir (S7). Idempotent: "bu siparis icin
            //    fatura zaten var" kontrolu + Sprint 8 madde 2'nin durum guard'i.
            await _orderConfirmation.ApplyConfirmedSideEffectsAsync(evt.order_id);

            // 2) SADAKAT PUANI. Idempotent dayanagi IKI KATMANLI: EarnFromOrder ONCE "bu siparis
            //    icin kazanim zaten var mi" diye sorar ve varsa BASARI doner; yaris durumunda
            //    UX_loyalty_transactions_order_earn (Sprint 6) devreye girer.
            //    SONUC KONTROL EDILIR: bu servis istisna FIRLATMIYOR, hata durumunu DONUYOR.
            //    Kontrol etmezsek gercek bir hata "basarili" sayilir, mesaj Processed olur ve
            //    outbox'in yeniden deneme kazanci o adim icin KAYBOLURDU.
            var sadakat = await _loyaltyService.EarnFromOrder(evt.customer_id, evt.total_price, evt.order_id);
            if (!sadakat.Item2.Success)
                throw new InvalidOperationException(
                    $"Sadakat puani adimi basarisiz: {sadakat.Item2.Message}");

            // 3) REFERANS ODULU. Idempotent dayanagi UX_store_credit_referee_reward (madde 3):
            //    davet edilen musteriye ikinci bir "davet edilen" odulu yazilamaz. Onceden tek
            //    koruma uygulama katmanindaki oku-sonra-davran guard'iydi; at-least-once bir
            //    mekanizmada o guard TEK BASINA yeterli degildi - kisit DB duzeyine indirildi.
            await _referralService.RewardOnFirstOrder(evt.customer_id, evt.order_id);

            // 4) KUPON SAYACI. coupon_usages satirlarindan TURETILIR (madde 1) - tanimi geregi
            //    idempotent; kac kez kosarsa kossun ayni sonucu verir.
            if (!string.IsNullOrWhiteSpace(evt.coupon_code))
            {
                var coupon = await _couponDal.GetByCodeAsync(evt.coupon_code);
                if (coupon != null) await _couponDal.SyncUsedCountAsync(coupon.id);
            }
        }
    }
}
