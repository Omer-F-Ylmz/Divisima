using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Beden bazlı stok (frontend sizeStockOf). Düz yapı.
    public class ProductStock : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; } // "XS/S/M/L/XL" veya sayısal ("38"); aksesuarda boş
        public int stock_quantity { get; set; }
        public int reserved_quantity { get; set; } // rezerve (ödeme bekleyen) - müsait = stock_quantity - reserved_quantity
        // Açıklayıcı yorum: Optimistic concurrency token - eşzamanlı stok güncellemesinde çakışmayı yakalar
        public byte[] row_version { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
