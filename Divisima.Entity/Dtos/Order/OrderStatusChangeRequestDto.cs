using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş durum değiştirme isteği (admin - kargoya ver, teslim et...).
    public class OrderStatusChangeRequestDto : IDto
    {
        public int id { get; set; }
        public OrderStatusEnum order_status { get; set; }
    }
}
