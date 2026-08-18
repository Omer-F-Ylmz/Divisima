using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Kupon kullanım kaydı (senin WebCouponUsage kalıbın).
    public class CouponUsage : IEntity
    {
        public int id { get; set; }
        public int coupon_id { get; set; }
        public int customer_id { get; set; }
        public int order_id { get; set; }
        public decimal discount_applied { get; set; }
        public DateTime created_at { get; set; }
    }
}
