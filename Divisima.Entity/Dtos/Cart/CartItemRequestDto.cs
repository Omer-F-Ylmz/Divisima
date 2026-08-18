using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Cart
{
    // Açıklayıcı yorum: Sepete ekle/güncelle (ürün + beden + adet).
    public class CartItemRequestDto : IDto
    {
        public int customer_id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
    }
}
