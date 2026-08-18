using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ürün özelliği (materyal, sezon, stil vb.) - anahtar/değer. Faceted search için.
    public class ProductAttribute : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string attribute_key { get; set; }   // ör. "materyal", "sezon", "stil"
        public string attribute_value { get; set; }  // ör. "pamuk", "yaz", "günlük"
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
    }
}
