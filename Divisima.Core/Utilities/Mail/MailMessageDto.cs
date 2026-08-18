namespace Divisima.Core.Utilities.Mail
{
    // Açıklayıcı yorum: Mail mesajı modeli.
    public class MailMessageDto
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool IsHtml { get; set; } = false;
    }
}
