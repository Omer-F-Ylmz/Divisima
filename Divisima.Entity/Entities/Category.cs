using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ana kategori (frontend MAINS). Düz yapı.
    public class Category : IEntity
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public int display_order { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
