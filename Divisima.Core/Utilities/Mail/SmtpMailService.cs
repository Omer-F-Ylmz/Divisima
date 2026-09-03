using Divisima.Core.Utilities.Text;   // GF-3/K1+K4 - KanitMaskesi (maske + satir temizligi)
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Divisima.Core.Utilities.Mail
{
    // Açıklayıcı yorum: SMTP mail gönderimi (MailKit). "MailSettings" bölümünden okur.
    //
    // ÖNCEKİ HALİ: bu sınıf yalnızca log basıp Task.CompletedTask dönüyordu - hiçbir e-posta
    // gitmiyordu ve HER çağıran BAŞARI görüyordu. Bu, sessiz bir kayıptan daha kötüydü:
    // OutboxProcessor mesajı "işlendi" diye işaretliyor, StockNotificationManager claim'i geri
    // almıyordu; yani gönderilmemiş bildirimler kalıcı olarak "gönderildi" sayılıyordu.
    //
    // SÖZLEŞME (üç durum, açıkça ayrılmış):
    //  1) MailSettings:Host DOLU  -> gerçek gönderim yapılır; BAŞARISIZLIKTA İSTİSNA FIRLATILIR.
    //     Fırlatmak zorunlu: çağıranların telafi mantığı (outbox retry, claim geri alma) yalnızca
    //     istisna görürse çalışır. Hata yutulursa mesaj sessizce kaybolur.
    //  2) MailSettings:Host BOŞ   -> gönderim YOK, LogWarning. Bu duruma yalnızca Development'ta
    //     düşülebilir; çünkü (3) gereği prod'da uygulama zaten açılmaz. Böylece bu sınıfın
    //     ortamı bilmesine gerek kalmıyor (IHostEnvironment bağımlılığı eklenmedi).
    //  3) MailSettings:Host BOŞ + non-Development -> Program.cs'in fail-fast bloğu açılışı
    //     engeller (bkz. Program.cs config doğrulaması).
    public class SmtpMailService : IMailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpMailService> _logger;

        public SmtpMailService(IConfiguration config, ILogger<SmtpMailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(MailMessageDto message)
        {
            var host = _config["MailSettings:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                // Yalnızca Development'ta ulaşılabilir bir dal (bkz. sözleşme 3).
                // GF-3/K1+K4: {To} musteri e-postasidir (KVKK) -> maskeden; {Subject} log'u
                // PARCALAYABILIR (CRLF) -> satir temizliginden gecer. Ikisi de URETIM
                // NOKTASINDA, cagirana birakilmadan.
                _logger.LogWarning("MAIL GONDERILMEDI (Host tanimsiz) -> {To} | {Subject}",
                    KanitMaskesi.Maskele(message.To), KanitMaskesi.SatirGuvenli(message.Subject));
                return;
            }

            var port = int.TryParse(_config["MailSettings:Port"], out var p) ? p : 587;
            var user = _config["MailSettings:User"];
            var password = _config["MailSettings:Password"];
            var from = _config["MailSettings:From"];
            if (string.IsNullOrWhiteSpace(from)) from = user;

            var mime = new MimeMessage();
            // Açıklayıcı yorum: "Divisima <no-reply@divisima.com>" biçimi de düz adres de kabul edilir.
            mime.From.Add(MailboxAddress.Parse(from));
            mime.To.Add(MailboxAddress.Parse(message.To));
            // GF-3/K4 (AV-1/A-3): Subject'te CRLF -> posta basligi enjeksiyonu (MimeKit
            // kodladigi icin SUPHE) ve log satiri parcalanmasi (OLCULEBILIR). Ikisi de AYNI
            // yardimcidan kapatilir; basliga da log'a da ayni temiz deger gider.
            mime.Subject = KanitMaskesi.SatirGuvenli(message.Subject) ?? "";
            mime.Body = new BodyBuilder
            {
                HtmlBody = message.IsHtml ? message.Body : null,
                TextBody = message.IsHtml ? null : message.Body
            }.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Açıklayıcı yorum: 465 -> örtük SSL, diğer portlar -> STARTTLS. Şifresiz bağlantı
                // KABUL EDILMEZ: kimlik bilgileri ve müşteri e-postaları düz metin gitmemeli.
                var secure = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await client.ConnectAsync(host, port, secure);

                if (!string.IsNullOrWhiteSpace(user))
                    await client.AuthenticateAsync(user, password);

                await client.SendAsync(mime);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // LOGLA VE FIRLAT - yutma. Çağıranın telafisi (outbox yeniden deneme, bildirim
                // claim'inin geri alınması) buna bağlı.
                // GF-3/K1+K2: (a) {To} maskeden, {Subject} satir temizliginden gecer;
                // (b) ISTISNA NESNESI ARTIK GECILMIYOR - `LogError(ex, ...)` Serilog'un
                // {Exception} alanina ex.ToString()'i HAM yazardi ve MailKit'in ayrisma
                // istisnalari ALICI ADRESINI mesajlarinda tasir. Yigin izi KAYBOLMAZ:
                // ex.ToString() maskeden gecirilip metne konuyor.
                _logger.LogError("MAIL GONDERILEMEDI -> {To} | {Subject} | {Hata}",
                    KanitMaskesi.Maskele(message.To), KanitMaskesi.SatirGuvenli(message.Subject),
                    KanitMaskesi.Maskele(ex.ToString()));
                throw;
            }
        }
    }
}
