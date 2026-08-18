using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: OrderSnapshotItem DAL implementasyonu.
    public class EfOrderSnapshotItemDal : EfEntityRepositoryBase<OrderSnapshotItem, DivisimaDbContext>, IOrderSnapshotItemDal
    {
        public EfOrderSnapshotItemDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
