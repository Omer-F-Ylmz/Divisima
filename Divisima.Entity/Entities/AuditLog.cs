using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Denetim kaydı - hangi tabloda, hangi kayıtta, ne değişti, kim yaptı.
    // EF SaveChanges interceptor tarafından otomatik doldurulur (kod değişikliği gerekmez).
    public class AuditLog : IEntity
    {
        public int id { get; set; }
        public string table_name { get; set; }
        public string entity_id { get; set; }
        public string action { get; set; }         // Added / Modified / Deleted
        public string? changes { get; set; }        // JSON: değişen alanlar (eski->yeni)
        public string? user_id { get; set; }        // işlemi yapan (JWT claim)
        public DateTime created_at { get; set; }
    }
}
