using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: OrderItem DAL implementasyonu.
    public class EfOrderItemDal : EfEntityRepositoryBase<OrderItem, DivisimaDbContext>, IOrderItemDal
    {
        public EfOrderItemDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
