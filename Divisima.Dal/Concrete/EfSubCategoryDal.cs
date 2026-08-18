using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Alt kategori DAL implementasyonu. Generic CRUD yeterli.
    public class EfSubCategoryDal : EfEntityRepositoryBase<SubCategory, DivisimaDbContext>, ISubCategoryDal
    {
        public EfSubCategoryDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
