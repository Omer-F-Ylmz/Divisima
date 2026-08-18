using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ürün görseli (çoklu). Bir ürünün birden çok görseli olabilir; biri birincil (grid'de gösterilir).
    public class ProductImage : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string image_url { get; set; }
        public int sort_order { get; set; }      // görsel sırası
        public bool is_primary { get; set; }     // birincil görsel (grid/liste)
        public DateTime created_at { get; set; }
    }
}
