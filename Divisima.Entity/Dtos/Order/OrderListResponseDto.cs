using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş liste dönüşü (hesabım - siparişlerim).
    public class OrderListResponseDto : IDto
    {
        public int id { get; set; }
        public string order_number { get; set; }
        public string order_status { get; set; }
        public decimal total { get; set; }
        public DateTime created_at { get; set; }
    }
}
