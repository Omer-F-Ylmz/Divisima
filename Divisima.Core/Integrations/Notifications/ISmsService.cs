namespace Divisima.Core.Integrations.Notifications
{
    // Açıklayıcı yorum: SMS soyutlaması. Sipariş onayı, kargo kodu, doğrulama kodu (opsiyonel 2FA) için.
    // Türk sağlayıcılar (Netgsm, İletimerkezi, Twilio) implementasyonu ile değiştirilir.
    public interface ISmsService
    {
        Task<bool> SendAsync(string phoneNumber, string message);
    }
}
