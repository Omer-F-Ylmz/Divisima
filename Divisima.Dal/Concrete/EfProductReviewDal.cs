using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Ürün yorumu DAL implementasyonu. Onaylı yorumlar (review_status=1).
    public class EfProductReviewDal : EfEntityRepositoryBase<ProductReview, DivisimaDbContext>, IProductReviewDal
    {
        public EfProductReviewDal(DivisimaDbContext context) : base(context)
        {
        }

        // Aciklayici yorum: ATOMIK faydali-oy artisi (eszamanli oylar sayaci eksik saymaz).
        public async Task IncrementHelpfulCountAsync(int reviewId)
        {
            await Context.Set<ProductReview>().Where(r => r.id == reviewId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.helpful_count, r => r.helpful_count + 1));
        }

        public async Task<List<ProductReview>> GetApprovedByProductAsync(int productId)
        {
            // Açıklayıcı yorum: review_status = 1 (Approved)
            return await Context.Set<ProductReview>()
                .Where(r => r.product_id == productId && r.review_status == 1 && r.is_active)
                .OrderByDescending(r => r.created_at)
                .ToListAsync();
        }
    }
}
