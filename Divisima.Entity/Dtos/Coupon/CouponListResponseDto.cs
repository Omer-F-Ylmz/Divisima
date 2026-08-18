using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Coupon
{
    // Açıklayıcı yorum: Kupon liste dönüşü (admin).
    public class CouponListResponseDto : IDto
    {
        public int id { get; set; }
        public string code { get; set; }
        public string discount_type { get; set; }
        public decimal value { get; set; }
        public decimal min_amount { get; set; }
        public decimal? max_discount_amount { get; set; }
        public DateTime? expire_date { get; set; }
        public int usage_limit { get; set; }
        public int used_count { get; set; }
        public bool is_active { get; set; }
    }
}
