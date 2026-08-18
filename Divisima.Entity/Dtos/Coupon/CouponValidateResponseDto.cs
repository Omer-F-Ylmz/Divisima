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
    }
}
