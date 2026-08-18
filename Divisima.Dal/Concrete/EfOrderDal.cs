using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Sipariş DAL implementasyonu. Cafixo minimal tarzı (kalemler serviste GetListAsync ile).
    public class EfOrderDal : EfEntityRepositoryBase<Order, DivisimaDbContext>, IOrderDal
    {
        public EfOrderDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
