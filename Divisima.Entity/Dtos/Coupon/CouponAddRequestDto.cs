using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.Coupon
{
    // Açıklayıcı yorum: Kupon ekleme isteği (admin). Gerçek kupon semantiği (WebCoupon kalıbı).
    public class CouponAddRequestDto : IDto
    {
        public string code { get; set; }
        public DiscountTypeEnum discount_type { get; set; }
        public decimal value { get; set; }
        public decimal min_amount { get; set; }
        public decimal? max_discount_amount { get; set; } // yüzde kuponlarda tavan
        public DateTime? expire_date { get; set; }        // son kullanma (null = süresiz)
        public int usage_limit { get; set; }
        public int per_user_limit { get; set; }              // 0 = sınırsız
    }
}
