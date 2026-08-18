using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Abstract
{
    public interface IStockReservationDal : IEntityRepository<StockReservation>
    {
        // Aciklayici yorum: ATOMIK durum gecisi - yalnizca status==fromStatus ise gecir (cift-isleme guard).
        // Donen deger 1 ise bu cagri gecisi kazandi (stok islemi yapabilir); 0 ise baskasi zaten yapti.
        Task<int> TryTransitionAsync(int reservationId, byte fromStatus, byte toStatus);
    }
}
