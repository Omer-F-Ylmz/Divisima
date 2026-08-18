using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Shipping
{
    // Açıklayıcı yorum: Kargo oluşturma (admin - sipariş kargoya verilince takip no girer).
    public class ShipmentCreateDto : IDto
    {
        public int order_id { get; set; }
        public byte carrier { get; set; }
        public string tracking_number { get; set; }
        public DateTime? estimated_delivery { get; set; }
    }
}
