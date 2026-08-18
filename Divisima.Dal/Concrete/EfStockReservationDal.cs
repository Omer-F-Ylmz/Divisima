using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Microsoft.EntityFrameworkCore;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfStockReservationDal : EfEntityRepositoryBase<StockReservation, DivisimaDbContext>, IStockReservationDal
    {
        public EfStockReservationDal(DivisimaDbContext context) : base(context) { }

        // Aciklayici yorum: ATOMIK durum gecisi - tek UPDATE (WHERE status=fromStatus). Cift-onay/cift-release engeli.
        public async Task<int> TryTransitionAsync(int reservationId, byte fromStatus, byte toStatus)
        {
            var now = DateTime.Now;
            return await Context.Set<StockReservation>()
                .Where(r => r.id == reservationId && r.status == fromStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.status, toStatus)
                    .SetProperty(r => r.closed_at, now));
        }
    }
}
