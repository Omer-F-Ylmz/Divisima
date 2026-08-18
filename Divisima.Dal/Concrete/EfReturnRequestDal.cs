using Microsoft.EntityFrameworkCore;
using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfReturnRequestDal : EfEntityRepositoryBase<ReturnRequest, DivisimaDbContext>, IReturnRequestDal
    {
        public EfReturnRequestDal(DivisimaDbContext context) : base(context) { }

        // Aciklayici yorum: ATOMIK durum gecisi (WHERE status=from) - eszamanli cift-refund engeli.
        public async Task<int> TryTransitionAsync(int returnId, byte fromStatus, byte toStatus)
        {
            var now = DateTime.Now;
            return await Context.Set<ReturnRequest>()
                .Where(r => r.id == returnId && r.status == fromStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.status, toStatus)
                    .SetProperty(r => r.processed_at, now));
        }
    }
}
