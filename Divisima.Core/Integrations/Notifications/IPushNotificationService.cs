namespace Divisima.Core.Integrations.Notifications
{
    // Açıklayıcı yorum: Push bildirim soyutlaması (Firebase Cloud Messaging). Mobil/masaüstü cihaz token'ına gönderir.
    // Sipariş durumu değişiminde ("Kargoya verildi", "Teslim edildi") müşteriye anlık bildirim.
    public interface IPushNotificationService
    {
        Task<bool> SendAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null);
    }
}
