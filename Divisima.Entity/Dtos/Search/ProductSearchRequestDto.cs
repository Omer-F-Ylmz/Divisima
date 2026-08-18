using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Search
{
    // Açıklayıcı yorum: Ürün arama isteği (metin + filtreler + sayfalama). Frontend arama çubuğu + filtre paneli.
    public class ProductSearchRequestDto : PagingRequestDto
    {
        public string? query { get; set; }          // ad/marka içinde arama
        public int? category_id { get; set; }
        public int? sub_category_id { get; set; }
        public decimal? min_price { get; set; }
        public decimal? max_price { get; set; }
        public bool? in_stock_only { get; set; }
        public string? sort_by { get; set; }         // "price_asc", "price_desc", "newest"
    }
}
