using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sepet (müşteriye bağlı kalıcı sepet). Düz yapı.
    public class Cart : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public DateTime? reminder_sent_at { get; set; } // sepet terk hatırlatması gönderildi mi (spam önleme)
    }
}
