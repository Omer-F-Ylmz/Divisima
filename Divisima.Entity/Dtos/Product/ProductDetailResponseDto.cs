using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün detay dönüşü (frontend openDetail). Bedenler + stok + puan özeti dahil.
    public class ProductDetailResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string brand { get; set; }
        public int category_id { get; set; }
        public string category_name { get; set; }
        public int? sub_category_id { get; set; }
        public string sub_category_name { get; set; }
        public decimal price { get; set; }
        public decimal? old_price { get; set; }
        public string description { get; set; }
        public string color_hex { get; set; }
        public string product_type { get; set; }
        public List<ProductStockDto> stocks { get; set; }
        public double review_average { get; set; }
        public int review_count { get; set; }
        public decimal average_rating { get; set; }
        public bool is_active { get; set; }
    }
}
