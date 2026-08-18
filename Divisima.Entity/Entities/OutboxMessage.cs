using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Outbox mesajı - event, sipariş ile AYNI transaction'da buraya yazılır.
    // Arka plan işleyici sonradan güvenilir şekilde işler (at-least-once). Event kaybı olmaz.
    public class OutboxMessage : IEntity
    {
        public int id { get; set; }
        public string event_type { get; set; }        // ör. "OrderPlaced"
        public string payload { get; set; }            // JSON serialize edilmiş event
        public byte status { get; set; }               // Beklemede (0), İşlendi (1), Hatalı (2)
        public int retry_count { get; set; }
        public string? error { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? processed_at { get; set; }
    }
}
