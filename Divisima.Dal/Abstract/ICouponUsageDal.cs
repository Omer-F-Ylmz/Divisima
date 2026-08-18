using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Kupon kullanım DAL. Ortak CRUD yeterli.
    public interface ICouponUsageDal : IEntityRepository<CouponUsage>
    {
    }
}
