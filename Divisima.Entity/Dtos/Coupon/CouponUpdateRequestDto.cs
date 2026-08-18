using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.Coupon
{
    // Açıklayıcı yorum: Kupon güncelleme isteği (admin).
    public class CouponUpdateRequestDto : IDto
    {
        public int id { get; set; }
        public string code { get; set; }
        public DiscountTypeEnum discount_type { get; set; }
        public decimal value { get; set; }
        public decimal min_amount { get; set; }
        public decimal? max_discount_amount { get; set; }
        public DateTime? expire_date { get; set; }
        public int usage_limit { get; set; }
        public int per_user_limit { get; set; }
    }
}
