using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfRecentlyViewedProductDal : EfEntityRepositoryBase<RecentlyViewedProduct, DivisimaDbContext>, IRecentlyViewedProductDal
    {
        // Açıklayıcı yorum: DbContext'i base'e ilet (EfEntityRepositoryBase parametresiz ctor'a sahip DEĞİL -> zorunlu).
        public EfRecentlyViewedProductDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
