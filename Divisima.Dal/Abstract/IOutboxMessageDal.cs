using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Outbox DAL. Bekleyen mesajları getirme.
    public interface IOutboxMessageDal : IEntityRepository<OutboxMessage>
    {
        Task<List<OutboxMessage>> GetPendingAsync(int take);

        // Aciklayici yorum: ATOMIK CLAIM - mesaji Pending->Processing gecir (processed_at=now=claim zamani).
        // Iki processor instance ayni mesaji ISLEYEMEZ: yalniz biri rowcount=1 alir, digeri 0 (skip). Cift teslim ENGELI.
        Task<int> TryClaimAsync(int id);

        // Aciklayici yorum: CRASH KURTARMA - islemi yarida kalan (Processing + processed_at < cutoff) mesajlari
        // yeniden Pending yapar (processor cokerse mesaj sonsuza dek Processing kalmasin).
        Task<int> ReclaimStaleAsync(System.DateTime cutoff);
    }
}
