using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Recommendation
{
    // Açıklayıcı yorum: Öneri widget'ı için hafif ürün DTO'su (grid kartı verisi).
    public class RecommendedProductDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string category_name { get; set; }
        public decimal price { get; set; }
        public decimal? old_price { get; set; }
        public string image_url { get; set; }
    }
}
