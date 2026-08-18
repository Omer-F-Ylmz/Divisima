using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Koleksiyon-ürün bağlantısı (many-to-many). Düz yapı.
    public class CollectionItem : IEntity
    {
        public int id { get; set; }
        public int collection_id { get; set; }
        public int product_id { get; set; }
        public int display_order { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
    }
}
