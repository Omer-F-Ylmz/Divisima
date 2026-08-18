using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Favori/istek listesi kalemi (frontend favorites Set karşılığı).
    public class WishlistItem : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public int product_id { get; set; }
        public DateTime created_at { get; set; }
    }
}
