using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: İçerik/legal sayfa (frontend 10 legal sayfa). Çok dilli, düz yapı.
    public class Content : IEntity
    {
        public int id { get; set; }
        public string slug { get; set; }
        public string title_tr { get; set; }
        public string? title_en { get; set; }
        public string body_tr { get; set; }
        public string? body_en { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
