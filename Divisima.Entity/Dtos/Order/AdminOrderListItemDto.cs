using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Admin sipariş listesi kalemi (özet - detay ayrı uçtan).
    public class AdminOrderListItemDto : IDto
    {
        public int id { get; set; }
        public string order_number { get; set; }
        public int customer_id { get; set; }
        public byte status { get; set; }
        public string status_name { get; set; }
        public decimal total_price { get; set; }
        public byte payment_type { get; set; }
        public string? coupon_code { get; set; }
        public DateTime created_at { get; set; }
    }
}
