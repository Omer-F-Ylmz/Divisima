using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Stok bildirim talebi DAL (minimal - base CRUD yeterli).
    public interface IStockNotificationRequestDal : IEntityRepository<StockNotificationRequest>
    {
        // Açıklayıcı yorum: ATOMİK claim - is_notified'ı YALNIZ hâlâ false ise true yap; kazanan (affected=1) döner.
        // Eşzamanlı iki NotifyBackInStock çağrısı aynı talebi ÇİFT bildirmesin (outbox TryClaimAsync deseni).
        Task<bool> TryClaimForNotificationAsync(int id);
        // Açıklayıcı yorum: Mail gönderimi başarısızsa geri al (tekrar denenebilsin).
        Task ResetNotificationClaimAsync(int id);
    }
}
