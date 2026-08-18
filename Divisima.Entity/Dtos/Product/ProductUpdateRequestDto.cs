using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün güncelleme isteği (admin). id zorunlu.
    public class ProductUpdateRequestDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string brand { get; set; }
        public int category_id { get; set; }
        public int? sub_category_id { get; set; }
        public decimal price { get; set; }
        public decimal? old_price { get; set; }
        public decimal? sale_price { get; set; }
        public string description { get; set; }
        public string color_hex { get; set; }
        public ProductTypeEnum product_type { get; set; }
        public List<ProductStockDto> stocks { get; set; }
    }
}
