using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ürün yorumu (Cafixo ProductReview kalıbı). byte review_status ile onay akışı.
    public class ProductReview : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public int customer_id { get; set; }
        public int rating { get; set; } // 1-5 yıldız
        public string comment { get; set; }
        public bool is_verified_purchase { get; set; } // doğrulanmış alıcı (bu ürünü satın almış)
        public int helpful_count { get; set; } // "faydalı" oy sayısı
        public byte review_status { get; set; } // Beklemede - Pending (0), Onaylı - Approved (1), Reddedildi - Rejected (2)
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
