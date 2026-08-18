using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: StockMovement DAL implementasyonu.
    public class EfStockMovementDal : EfEntityRepositoryBase<StockMovement, DivisimaDbContext>, IStockMovementDal
    {
        public EfStockMovementDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
