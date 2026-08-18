using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfStoreCreditTransactionDal : EfEntityRepositoryBase<StoreCreditTransaction, DivisimaDbContext>, IStoreCreditTransactionDal
    {
        // Açıklayıcı yorum: DbContext'i base'e ilet (EfEntityRepositoryBase parametresiz ctor'a sahip DEĞİL -> zorunlu).
        public EfStoreCreditTransactionDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
