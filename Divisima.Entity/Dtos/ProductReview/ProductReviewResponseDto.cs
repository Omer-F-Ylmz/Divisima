using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.ProductReview
{
    // Açıklayıcı yorum: Yorum dönüşü.
    public class ProductReviewResponseDto : IDto
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public int rating { get; set; }
        public string comment { get; set; }
        public string review_status { get; set; }
        public DateTime created_at { get; set; }
    }
}
