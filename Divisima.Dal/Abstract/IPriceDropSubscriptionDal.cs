using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Abstract
{
    public interface IPriceDropSubscriptionDal : IEntityRepository<PriceDropSubscription> {         // Açıklayıcı yorum: ATOMİK claim - eşzamanlı çift fiyat-düşüş bildirimini engeller (is_notified false->true, kazanan döner).
        Task<bool> TryClaimForNotificationAsync(int id);
        Task ResetNotificationClaimAsync(int id);
}
}
