using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: "Stok gelince haber ver" talebi. Ürün+beden stoğu 0 iken müşteri e-posta bırakır;
    // stok geldiğinde bildirim gönderilir. Nav property yok (product_id skaler FK).
    public class StockNotificationRequest : IEntity
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; } // hangi beden (aksesuarda boş)
        public string email { get; set; } // bildirim gönderilecek e-posta
        public bool is_notified { get; set; } // bildirim gönderildi mi (tekrar gönderilmez)
        public DateTime created_at { get; set; }
        public DateTime? notified_at { get; set; } // ne zaman haber verildi
    }
}
