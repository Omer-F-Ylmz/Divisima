using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün filtre + sayfalama isteği (frontend catState filtreleri).
    public class ProductFilterRequestDto : IDto
    {
        public int? category_id { get; set; }
        public int? sub_category_id { get; set; }
        public List<string> sizes { get; set; }
        public List<string> colors { get; set; }
        public decimal? min_price { get; set; }
        public decimal? max_price { get; set; }
        public bool? on_sale { get; set; }
        public bool? in_stock { get; set; }
        public string sort { get; set; }          // price-asc | price-desc | new | old
        public int page { get; set; } = 1;
        public int size { get; set; } = 12;
    }
}
