using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Müşterinin son görüntülediği ürünler (kişiselleştirme). Ürün başına tek satır -
    // tekrar görüntülemede viewed_at güncellenir (upsert). Nav property yok.
    public class RecentlyViewedProduct : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public int product_id { get; set; }
        public DateTime viewed_at { get; set; }
    }
}
