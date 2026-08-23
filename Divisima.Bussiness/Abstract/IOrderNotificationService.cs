using System.Threading.Tasks;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: MERKEZİ sipariş-durumu bildirimi (DRY). Sipariş Shipped/Delivered olunca müşteriye
    // in-app + push + SMS bildirimi. Hem ChangeOrderStatus (admin) hem ShipmentManager (kargo) buradan cagirir
    // -> kargo-kaynakli gecislerde bildirim ATLANMAZ (onceden ShipmentManager bildirimi hic tetiklemiyordu).
    //
    // DALGA B / B4: E-POSTA KANALI ve TAKIP NUMARASI eklendi.
    // OLCULEN ONCE-DURUM: bu servis yalnizca in-app (SignalR) + push (FCM) + SMS gonderiyordu; UCU DE
    // yapilandirilmis bir saglayici ister ve mesajda TAKIP NUMARASI YOKTU ("Siparisiniz kargoya verildi.
    // Siparis no: X"). Yani admin takip numarasini girdikten sonra musteriye o numara HICBIR kanaldan
    // ulasmiyordu. E-posta, Dalga A'da kurulan outbox uzerinden gider (SMTP patlarsa akis dusmez).
    public interface IOrderNotificationService
    {
        // kargoFirmasi/takipNo YALNIZ Shipped gecisinde ve YALNIZ kargo kaydini bilen cagirandan
        // (ShipmentManager) gelir. ChangeOrderStatus bunlari bilmez, null gecer - o durumda e-posta
        // takip satiri OLMADAN gider; uydurma bir numara yazilmaz.
        Task NotifyStatusChangeAsync(Order order, OrderStatusEnum newStatus, string? kargoFirmasi = null, string? takipNo = null);
    }
}
