using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Ürün yorumu DAL. Ürüne göre onaylı yorumlar.
    public interface IProductReviewDal : IEntityRepository<ProductReview>
    {
        // Aciklayici yorum: ATOMIK faydali-oy artisi (lost-update yok).
        Task IncrementHelpfulCountAsync(int reviewId);
        Task<List<ProductReview>> GetApprovedByProductAsync(int productId);
    }
}
