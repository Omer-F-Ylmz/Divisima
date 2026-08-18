using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Snapshot kalemi (Cafixo OrderSnapshotItem kalıbı) - ürün adı/fiyat dondurulmuş.
    public class OrderSnapshotItem : IEntity
    {
        public int id { get; set; }
        public int order_snapshot_id { get; set; } // Hangi snapshot'a ait
        public int product_id { get; set; }
        public string product_name { get; set; } // Ürün adı snapshot
        public string brand { get; set; }
        public decimal product_price { get; set; } // Ürün birim fiyatı snapshot
        public string size { get; set; }
        public int quantity { get; set; }
        public DateTime created_at { get; set; }
    }
}
