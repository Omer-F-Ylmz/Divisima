using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.StockNotification
{
    // Açıklayıcı yorum: "Gelince haber ver" abonelik isteği (frontend ürün detay).
    public class StockNotificationSubscribeRequestDto : IDto
    {
        public int product_id { get; set; }
        public string size { get; set; }
        public string email { get; set; }
    }
}
