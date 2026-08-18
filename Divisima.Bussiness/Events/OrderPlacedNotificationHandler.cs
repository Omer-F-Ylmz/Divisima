using Divisima.Core.Utilities.Notifications;

namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Canlı bildirim handler'ı (SignalR ile admin paneline/müşteriye anlık bildirim).
    public class OrderPlacedNotificationHandler : IOrderPlacedEventHandler
    {
        private readonly INotificationService _notificationService;

        public OrderPlacedNotificationHandler(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task HandleAsync(OrderPlacedEvent @event)
        {
            // Açıklayıcı yorum: Admin'e yeni sipariş bildirimi
            await _notificationService.NotifyAdminsAsync($"Yeni sipariş: #{@event.order_number} ({@event.total} TL)");
        }
    }
}
