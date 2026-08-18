using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: OrderSnapshot DAL implementasyonu.
    public class EfOrderSnapshotDal : EfEntityRepositoryBase<OrderSnapshot, DivisimaDbContext>, IOrderSnapshotDal
    {
        public EfOrderSnapshotDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
