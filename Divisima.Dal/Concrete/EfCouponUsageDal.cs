using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Kupon kullanım DAL implementasyonu.
    public class EfCouponUsageDal : EfEntityRepositoryBase<CouponUsage, DivisimaDbContext>, ICouponUsageDal
    {
        public EfCouponUsageDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
