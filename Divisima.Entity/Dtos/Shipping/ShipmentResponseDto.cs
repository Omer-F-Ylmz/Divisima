using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Shipping
{
    // Açıklayıcı yorum: Kargo görüntüleme (müşteri takip eder).
    public class ShipmentResponseDto : IDto
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public byte carrier { get; set; }
        public string carrier_name { get; set; }
        public string tracking_number { get; set; }
        public byte status { get; set; }
        public string status_name { get; set; }
        public string? last_status_text { get; set; }
        public DateTime? shipped_at { get; set; }
        public DateTime? estimated_delivery { get; set; }
        public DateTime? delivered_at { get; set; }
    }
}
