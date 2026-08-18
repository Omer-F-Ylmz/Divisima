using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün ekl/güncelle sırasında beden-stok satırı (frontend beden seçimi + stok)
    public class ProductStockDto : IDto
    {
        public string size { get; set; }
        public int stock_quantity { get; set; }
    }
}
