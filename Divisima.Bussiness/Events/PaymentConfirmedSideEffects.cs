using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Events
{
    // SPRINT 8 MADDE 3 - dort yan etkinin TEK uygulayicisi. Bkz. IPaymentConfirmedSideEffects.
    //
    // DALGA-2-FIX (B10): artik YALNIZ kart yolunun degil, TUM onay yollarinin uygulayicisi.
    // Kupon KULLANIM SATIRI da buraya tasindi (4. adim) - onceden yalniz IyzicoPaymentManager'in
    // transaction'i yaziyordu, dolayisiyla kart disi onaylarda satir HIC olusmuyordu ve ondan
    // TURETILEN used_count kalici olarak 0 kaliyordu.
    public class PaymentConfirmedSideEffects : IPaymentConfirmedSideEffects
    {
        private readonly IOrderConfirmationService _orderConfirmation;
        private readonly ILoyaltyService _loyaltyService;
        private readonly IReferralService _referralService;
        private readonly ICouponDal _couponDal;
        private readonly ICouponUsageDal _couponUsageDal;

        public PaymentConfirmedSideEffects(IOrderConfirmationService orderConfirmation,
            ILoyaltyService loyaltyService, IReferralService referralService, ICouponDal couponDal,
            ICouponUsageDal couponUsageDal)
        {
            _orderConfirmation = orderConfirmation;
            _loyaltyService = loyaltyService;
            _referralService = referralService;
            _couponDal = couponDal;
            _couponUsageDal = couponUsageDal;
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

            // 4) KUPON KULLANIM SATIRI + SAYAC.
            //
            //    DALGA-2-FIX (B10) - SATIRIN YAZIMI BURAYA TASINDI. Onceden satiri
            //    IyzicoPaymentManager'in A-bolgesi transaction'i yaziyordu; sayac ise BURADAN
            //    turetiliyordu. Kart disi onay yollari o transaction'a hic ugramadigi icin satir
            //    OLUSMUYOR, turetme de dogal olarak 0 buluyordu (olculdu: siparis #13 kapida odeme
            //    + E2YUZDE kuponu -> coupon_usages 0 satir, used_count 0; kupon admin panelinde
            //    "hic kullanilmamis" gorunuyordu).
            //
            //    NEDEN TEK YAZICI: satiri her onay yoluna AYRI AYRI eklemek ayni mantigin dort
            //    kopyasi olurdu - bu dalganin duzeltmeye calistigi kusurun ta kendisi.
            //
            //    BEDELI (durust kayit): kart yolunda satir artik odeme transaction'inda DEGIL,
            //    outbox teslimatinda (~1 dk) yaziliyor. Aradaki pencerede satiri okuyan bir tuketici
            //    YOK (olculdu: depoda coupon_usages'i okuyan tek yer SyncUsedCountAsync'tir; kupon
            //    LIMITLERI siparis sayimiyla denetlenir, satirla degil), bu yuzden davranissal
            //    gerileme olusmuyor.
            //
            //    IDEMPOTENTLIK IKI KATMANLI: once "bu siparis icin satir var mi" sorulur; yaris
            //    durumunda UX_coupon_usages_coupon_order (Sprint 8 madde 1) devreye girer.
            //    Sayac TURETILDIGI icin (madde 1) tanimi geregi zaten idempotenttir.
            if (!string.IsNullOrWhiteSpace(evt.coupon_code))
            {
                var coupon = await _couponDal.GetByCodeAsync(evt.coupon_code);
                if (coupon != null)
                {
                    var mevcut = await _couponUsageDal.GetAsync(u =>
                        u.coupon_id == coupon.id && u.order_id == evt.order_id);
                    if (mevcut == null)
                    {
                        await _couponUsageDal.AddAsync(new CouponUsage
                        {
                            coupon_id = coupon.id,
                            customer_id = evt.customer_id,
                            order_id = evt.order_id,
                            discount_applied = evt.discount_amount,
                            created_at = DateTime.Now
                        });
                    }
                    await _couponDal.SyncUsedCountAsync(coupon.id);
                }
            }
        }
    }
}
