using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Müşteri cihazı (push token). Bir müşterinin birden çok cihazı olabilir (telefon/tablet/masaüstü).
    // Push bildirimi bu tablodaki aktif token'lara gönderilir. Token yenilenince güncellenir (upsert).
    public class CustomerDevice : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public string device_token { get; set; }     // FCM registration token
        public byte platform { get; set; }           // Web (0), Android (1), iOS (2)
        public bool is_active { get; set; }           // token geçersizleşince false
        public DateTime created_at { get; set; }
        public DateTime? last_used_at { get; set; }
    }
}
