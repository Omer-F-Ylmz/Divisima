using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Abstract
{
    public interface IReturnRequestDal : IEntityRepository<ReturnRequest>
    {
        // Aciklayici yorum: ATOMIK durum gecisi (cift-refund guard) - yalnizca status==from ise gecir.
        Task<int> TryTransitionAsync(int returnId, byte fromStatus, byte toStatus);
    }
}
