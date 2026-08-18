using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Cart
{
    // Açıklayıcı yorum: Sepet dönüşü (kalemler + toplam).
    public class CartResponseDto : IDto
    {
        public int cart_id { get; set; }
        public List<CartLineDto> items { get; set; } = new();
        public decimal subtotal { get; set; }
    }
    public class CartLineDto : IDto
    {
        public int product_id { get; set; }
        public string product_name { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal line_total { get; set; }
    }
}
