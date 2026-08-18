using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün sayfalı liste dönüşü. Senin {X}PagingListResponseDto kalıbın:
    // liste + sayfalama meta bilgisi tek DTO'da (generic PagingResponseDto yerine).
    public class ProductPagingListResponseDto : IDto
    {
        public List<ProductListResponseDto> items { get; set; }
        public int total_count { get; set; }
        public int page { get; set; }
        public int size { get; set; }
        public int total_pages { get; set; }
    }
}
