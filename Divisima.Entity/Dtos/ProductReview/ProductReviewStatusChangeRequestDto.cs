using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.ProductReview
{
    // Açıklayıcı yorum: Yorum onay durumu değiştirme (admin - onayla/reddet).
    public class ProductReviewStatusChangeRequestDto : IDto
    {
        public int id { get; set; }
        public ReviewStatusEnum review_status { get; set; }
    }
}
