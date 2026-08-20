using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ürün entity'si (frontend PRODUCTS). Düz yapı - nav property yok, ilişkiler serviste kurulur.
    public class Product : IEntity
    {
        public int id { get; set; }
        public string name { get; set; }
        public string brand { get; set; }
        public int category_id { get; set; }
        public int? sub_category_id { get; set; }
        public decimal price { get; set; }
        public decimal? sale_price { get; set; } // flash sale fiyatı (aktif pencerede geçerli)
        public DateTime? sale_start { get; set; } // flash sale başlangıç
        public DateTime? sale_end { get; set; } // flash sale bitiş
        public decimal? old_price { get; set; } // indirim öncesi fiyat; null ise indirim yok
        public string description { get; set; }
        public string color_hex { get; set; }
        public string? variant_group_id { get; set; } // aynı ürünün renk varyantlarını bağlar (aynı grup = varyant)
        public string? image_url { get; set; } // ürün görseli (URL veya data-uri) - frontend grid/detay
        public byte product_type { get; set; } // Giysi (0), Aksesuar (1) - beden stok mantığı buna göre
        // KDV oranı OVERRIDE'ı (0.20 = %20). NULL = kategorinin oranı kullanılır.
        // Efektif oran zinciri: Product.vat_rate ?? Category.vat_rate ?? EInvoice:KdvRate (0.20).
        public decimal? vat_rate { get; set; }
        public decimal average_rating { get; set; } = 0m;  // onayli yorumlarin ortalamasi (frontend yildiz)
        public int review_count { get; set; } = 0;         // onayli yorum sayisi
        public int? seller_id { get; set; } // hangi satıcıya ait (null = platform/admin ürünü). Marketplace izolasyonu.
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
