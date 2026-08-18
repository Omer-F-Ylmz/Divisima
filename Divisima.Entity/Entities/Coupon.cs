using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Kupon (frontend COUPONS + Cafixo WebCoupon semantiği). byte discount_type,
    // son kullanma tarihi, kullanım limiti/sayacı, yüzde kuponlarda indirim tavanı.
    public class Coupon : IEntity
    {
        public int id { get; set; }
        public string code { get; set; }
        public byte discount_type { get; set; } // Yüzde - Percentage (0), Sabit - Fixed (1), Kargo - FreeShipping (2)
        public decimal value { get; set; }
        public decimal min_amount { get; set; } // minimum sepet tutarı (frontend min)
        public decimal? max_discount_amount { get; set; } // yüzde kuponlarda indirim üst limiti (WebCoupon kalıbı)
        public DateTime? expire_date { get; set; } // son kullanma; null ise süresiz
        public int usage_limit { get; set; }
        public int per_user_limit { get; set; } = 0; // kullanici basina kullanim limiti (0 = sinirsiz) // toplam kaç kez kullanılabilir (0 = sınırsız)
        public int used_count { get; set; } // başarılı kullanım sayısı
        public bool first_order_only { get; set; } // sadece ilk siparişte geçerli
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public byte[] row_version { get; set; } // optimistic concurrency (used_count lost-update önleme)
    }
}
