using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfWishlistItemDal : EfEntityRepositoryBase<WishlistItem, DivisimaDbContext>, IWishlistItemDal
    {
        public EfWishlistItemDal(DivisimaDbContext context) : base(context) { }
    }
}
