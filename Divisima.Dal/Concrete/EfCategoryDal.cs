using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Kategori DAL implementasyonu. Cafixo minimal tarzı (kompozisyon serviste).
    public class EfCategoryDal : EfEntityRepositoryBase<Category, DivisimaDbContext>, ICategoryDal
    {
        public EfCategoryDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
