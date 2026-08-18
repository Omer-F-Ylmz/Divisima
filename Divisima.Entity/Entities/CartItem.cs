using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sepet kalemi (frontend CartItem: ürün + beden + adet). Düz yapı.
    public class CartItem : IEntity
    {
        public int id { get; set; }
        public int cart_id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
