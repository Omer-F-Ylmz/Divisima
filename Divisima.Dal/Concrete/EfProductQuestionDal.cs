using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfProductQuestionDal : EfEntityRepositoryBase<ProductQuestion, DivisimaDbContext>, IProductQuestionDal
    {
        public EfProductQuestionDal(DivisimaDbContext context) : base(context) { }
    }
}
