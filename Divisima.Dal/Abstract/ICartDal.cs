using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Sepet DAL. Ortak CRUD yeterli (kalemler serviste ayrı DAL ile - kompozisyon).
    public interface ICartDal : IEntityRepository<Cart>
    {
            // ATOMIK claim (H45b): reminder_sent_at'i YALNIZ hala NULL ise damgala; kazanan (affected=1) doner.
        // Eszamanli iki terk-sepet job'i (veya crash-retry) ayni sepete CIFT hatirlatma maili atmasin
        // (H42'de StockNotification/PriceDrop icin uygulanan desenin ayni - bu manager o turda gozden kacmisti).
        Task<bool> TryClaimReminderAsync(int cartId);
        Task ResetReminderClaimAsync(int cartId);
}
}
