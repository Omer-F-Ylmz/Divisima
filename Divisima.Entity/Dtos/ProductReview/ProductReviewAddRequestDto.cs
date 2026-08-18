using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.ProductReview
{
    // Açıklayıcı yorum: Yorum ekleme isteği (müşteri).
    public class ProductReviewAddRequestDto : IDto
    {
        public int product_id { get; set; }
        public int customer_id { get; set; }
        public int rating { get; set; }
        public string comment { get; set; }
    }
}
