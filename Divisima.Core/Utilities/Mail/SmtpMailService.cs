using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Utilities.Mail
{
    // Açıklayıcı yorum: SMTP mail gönderimi (basit). Production'da SendGrid/MailKit önerilir.
    // appsettings "MailSettings" bölümünden okur. Şimdilik loglar (gerçek SMTP bağlanınca aktif olur).
    public class SmtpMailService : IMailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpMailService> _logger;

        public SmtpMailService(IConfiguration config, ILogger<SmtpMailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public Task SendAsync(MailMessageDto message)
        {
            // Açıklayıcı yorum: Gerçek SMTP gönderimi buraya (System.Net.Mail.SmtpClient veya MailKit).
            // Şimdilik log - SMTP kimlik bilgileri appsettings/secrets'ten bağlanınca aktif edilir.
            _logger.LogInformation("MAIL -> {To} | {Subject}", message.To, message.Subject);
            return Task.CompletedTask;
        }
    }
}
