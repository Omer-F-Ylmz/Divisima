using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sipariş kalemi (Cafixo OrderItem kalıbı). Düz yapı.
    public class OrderItem : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; } // hangi sipariş
        public int product_id { get; set; } // hangi ürün
        public string size { get; set; } // hangi beden
        public int quantity { get; set; } // kaç adet
        public decimal unit_price { get; set; } // sipariş anındaki birim fiyat
        public int? seller_id { get; set; } // sipariş anında ürünün satıcısı (denormalize - satıcı bazlı satış/gelir sorgusu için)
        public bool is_cancelled { get; set; } // kısmi iptal - bu kalem iptal edildi mi
        public DateTime created_at { get; set; }
    }
}
