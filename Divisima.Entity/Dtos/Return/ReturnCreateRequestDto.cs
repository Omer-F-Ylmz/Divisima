using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Return
{
    // Açıklayıcı yorum: İade talebi oluşturma. customer_id JWT'den set edilir (client göndermez).
    public class ReturnCreateRequestDto : IDto
    {
        public int order_id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public byte reason { get; set; }
        public string? description { get; set; }
        public byte return_type { get; set; }        // 0=İade, 1=Değişim
        public int customer_id { get; set; }          // JWT'den override
    }
}
