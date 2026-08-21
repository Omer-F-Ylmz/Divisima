using Divisima.Core.Entities.Abstract;
namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: "Fiyat düşünce haber ver" aboneliği. Abone olunan fiyatın altına düşünce e-posta.
    public class PriceDropSubscription : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string email { get; set; }
        public decimal subscribed_price { get; set; } // abone olurkenki fiyat (bunun altına düşünce bildir)
        public bool is_notified { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? notified_at { get; set; }

        // SPRINT 8 MADDE 10 - ABONELIKTEN CIKMA JETONU (StockNotificationRequest ile ayni gerekce).
        public string unsubscribe_token { get; set; }
    }
}
