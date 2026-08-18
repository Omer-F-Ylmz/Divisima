namespace Divisima.Core.Utilities.Mail
{
    // Açıklayıcı yorum: Mail gönderim soyutlaması (SMTP/SendGrid implementasyonu ile değiştirilir).
    public interface IMailService
    {
        Task SendAsync(MailMessageDto message);
    }
}
