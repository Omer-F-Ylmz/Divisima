using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Alt kategori (frontend sub). Düz yapı.
    public class SubCategory : IEntity
    {
        public int id { get; set; }
        public int category_id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
