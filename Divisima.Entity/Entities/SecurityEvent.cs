using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Güvenlik olay kaydı - başarısız login, kilitlenme, ödeme reddi, fraud, IDOR denemesi,
    // yeni cihaz login vb. SIEM/alerting için ayrı akış. Denetim (audit) kaydından farklı: güvenlik odaklı.
    public class SecurityEvent : IEntity
    {
        public int id { get; set; }
        public string event_type { get; set; }      // LoginFailed, AccountLocked, PaymentFraud, IdorAttempt, NewDeviceLogin...
        public string severity { get; set; }          // Info, Warning, Critical
        public int? customer_id { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }
        public string? detail { get; set; }
        public DateTime created_at { get; set; }
    }
}
