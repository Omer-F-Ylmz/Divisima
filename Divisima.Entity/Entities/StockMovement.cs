using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Stok hareket kaydı. reference_id ilgili sipariş id'si.
    public class StockMovement : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public byte movement_type { get; set; } // Giriş - In (1), Çıkış - Out (2)
        public int quantity { get; set; }
        public int? reference_id { get; set; } // sipariş id
        public string? note { get; set; }
        public DateTime created_at { get; set; }
    }
}
