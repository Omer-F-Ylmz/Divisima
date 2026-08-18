using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Return
{
    // Açıklayıcı yorum: İade görüntüleme.
    public class ReturnResponseDto : IDto
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public byte reason { get; set; }
        public byte return_type { get; set; }
        public byte status { get; set; }
        public string status_name { get; set; }
        public decimal refund_amount { get; set; }
        public string? admin_note { get; set; }
        public DateTime created_at { get; set; }
    }
}
