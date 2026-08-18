using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Outbox DAL implementasyonu.
    public class EfOutboxMessageDal : EfEntityRepositoryBase<OutboxMessage, DivisimaDbContext>, IOutboxMessageDal
    {
        public EfOutboxMessageDal(DivisimaDbContext context) : base(context) { }

        // Açıklayıcı yorum: Bekleyen mesajlar (status=0), eskiden yeniye
        public async Task<List<OutboxMessage>> GetPendingAsync(int take)
        {
            return await Context.Set<OutboxMessage>()
                .Where(m => m.status == 0 && m.retry_count < 5)
                .OrderBy(m => m.created_at)
                .Take(take)
                .ToListAsync();
        }

        // Aciklayici yorum: ATOMIK claim - Pending(0)->Processing(3), processed_at=claim zamani. rowcount=1 ise bu instance aldi.
        public async Task<int> TryClaimAsync(int id)
        {
            return await Context.Set<OutboxMessage>()
                .Where(m => m.id == id && m.status == 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.status, (byte)3)
                    .SetProperty(m => m.processed_at, System.DateTime.Now));
        }

        // Aciklayici yorum: Yarida kalan Processing mesajlari (processed_at < cutoff) yeniden Pending yap (crash kurtarma).
        public async Task<int> ReclaimStaleAsync(System.DateTime cutoff)
        {
            return await Context.Set<OutboxMessage>()
                .Where(m => m.status == 3 && m.processed_at != null && m.processed_at < cutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.status, (byte)0)
                    .SetProperty(m => m.processed_at, (System.DateTime?)null));
        }
    }
}
