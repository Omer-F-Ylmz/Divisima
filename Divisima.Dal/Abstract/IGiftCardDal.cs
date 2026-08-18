using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Hediye kartı DAL. Atomik bozdurma çift-kullanımı (aynı kartı iki kez bozdurma) önler.
    public interface IGiftCardDal : IEntityRepository<GiftCard>
    {
        // Açıklayıcı yorum: ATOMİK bozdurma (compare-and-swap) - balance=0, is_active=false, redeemed_by/at TEK UPDATE'te,
        // WHERE id=X AND balance=expected AND balance>0. Eşzamanlı ikinci istek beklenen bakiyeyi bulamaz -> 0 döner (çift kredi yok).
        Task<int> TryRedeemAsync(int cardId, decimal expectedBalance, int redeemedBy, System.DateTime redeemedAt);
    }
}
