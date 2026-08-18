using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Zaman çizelgesi satırı (frontend sipariş takip adımı).
    public class OrderStatusHistoryDto : IDto
    {
        public byte status { get; set; }
        public string status_name { get; set; }
        public string note { get; set; }
        public DateTime created_at { get; set; }
    }
}
