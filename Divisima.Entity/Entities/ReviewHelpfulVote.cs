using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: "Bu yorum faydalı" oyu. (review_id, customer_id) tekil - çift oy engeli.
    public class ReviewHelpfulVote : IEntity
    {
        public int id { get; set; }
        public int review_id { get; set; }
        public int customer_id { get; set; }
        public DateTime created_at { get; set; }
    }
}
