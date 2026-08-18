namespace Divisima.Core.Utilities.Notifications
{
    // Açıklayıcı yorum: Anlık bildirim soyutlaması (SignalR implementasyonu API katmanında).
    public interface INotificationService
    {
        Task NotifyAdminsAsync(string message);
        Task NotifyCustomerAsync(int customerId, string message);
    }
}
