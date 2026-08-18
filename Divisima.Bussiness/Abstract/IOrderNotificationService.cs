using System.Threading.Tasks;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: MERKEZİ sipariş-durumu bildirimi (DRY). Sipariş Shipped/Delivered olunca müşteriye
    // in-app + push + SMS bildirimi. Hem ChangeOrderStatus (admin) hem ShipmentManager (kargo) buradan cagirir
    // -> kargo-kaynakli gecislerde bildirim ATLANMAZ (onceden ShipmentManager bildirimi hic tetiklemiyordu).
    public interface IOrderNotificationService
    {
        Task NotifyStatusChangeAsync(Order order, OrderStatusEnum newStatus);
    }
}
