using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Kupon DAL. Koda göre getirme özel sorgusu.
    public interface ICouponDal : IEntityRepository<Coupon>
    {
        Task<Coupon> GetByCodeAsync(string code);
    }
}
