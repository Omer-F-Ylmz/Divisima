using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.ProductReview;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün yorumu servisi. Ekle (müşteri) + onayla/reddet (admin) + ürüne göre listele.
    public interface IProductReviewService
    {
        Task<(HttpStatusCode, Result)> Add(ProductReviewAddRequestDto dto);
        Task<(HttpStatusCode, Result)> Approve(int id);
        Task<(HttpStatusCode, Result)> Reject(int id);
        Task<(HttpStatusCode, Result)> GetByProduct(int productId);
        Task<(HttpStatusCode, Result)> VoteHelpful(int reviewId, int customerId);
    }
}
