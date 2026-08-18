using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // Açıklayıcı yorum: Stok uyarısı - eşik altındaki ürün/beden (yeniden sipariş için).
    public class LowStockDto : IDto
    {
        public int product_id { get; set; }
        public string product_name { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
    }
}
