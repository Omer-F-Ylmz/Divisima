using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfProductImageDal : EfEntityRepositoryBase<ProductImage, DivisimaDbContext>, IProductImageDal
    {
        public EfProductImageDal(DivisimaDbContext context) : base(context) { }
    }
}
