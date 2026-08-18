using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfCartItemDal : EfEntityRepositoryBase<CartItem, DivisimaDbContext>, ICartItemDal
    {
        public EfCartItemDal(DivisimaDbContext context) : base(context) { }
    }
}
