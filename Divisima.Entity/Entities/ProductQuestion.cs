using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ürün soru-cevap. Müşteri soru sorar, admin yanıtlar; yanıtlananlar herkese görünür.
    public class ProductQuestion : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public int customer_id { get; set; }
        public string question { get; set; }
        public string? answer { get; set; }          // admin yanıtı (null = henüz yanıtlanmadı)
        public int? answered_by { get; set; }         // yanıtlayan admin id
        public bool is_answered { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? answered_at { get; set; }
    }
}
