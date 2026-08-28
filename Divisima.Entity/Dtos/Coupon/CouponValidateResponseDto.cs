using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Coupon
{
    // Açıklayıcı yorum: Kupon doğrulama sonucu - geçerliyse indirim tutarı + tipi döner.
    public class CouponValidateResponseDto : IDto
    {
        public string code { get; set; }
        public string discount_type { get; set; }
        public decimal discount_amount { get; set; }   // hesaplanan indirim (pct ise tutar, fixed ise değer)
        public bool free_shipping { get; set; }         // kargo bedava kuponu mu
        // MANTIK-FIX-1 / K4: kuponun MINIMUM SEPET TUTARI.
        // OLCULEN GEREKCE (A3/2A, ana akis bagimsiz dogruladi): istemcideki kupon nesnesi
        // `min:0` SABITI ile kuruluyordu (index.html couponApplyFrom - nesneyi kuran TEK yer),
        // bu yuzden validateCoupon guardi `cartRaw() < 0` sartina bagliydi ve HICBIR KOSULDA
        // atesleyemiyordu; her renderCartta IKI KEZ kosup HICBIR SEY yapmiyordu. Kok, bu DTOnun
        // degeri TASIMAMASIYDI - istemci telafi EDEMEZDI.
        public decimal min_amount { get; set; }
    }
}
