using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Cart
{
    // Açıklayıcı yorum: Sepet kalemi dönüşü.
    public class CartItemResponseDto : IDto
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string product_name { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal line_total { get; set; }
    }
}
