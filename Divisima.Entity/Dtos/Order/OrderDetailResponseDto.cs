using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş detay dönüşü (kalemler + tutar dökümü).
    public class OrderDetailResponseDto : IDto
    {
        public int id { get; set; }
        public string order_number { get; set; }
        public string order_status { get; set; }
        public decimal subtotal { get; set; }
        public decimal discount_amount { get; set; }
        public decimal shipping_cost { get; set; }
        public decimal total { get; set; }
        public string coupon_code { get; set; }
        public DateTime created_at { get; set; }
        public List<OrderItemResponseDto> items { get; set; }
    }
}
