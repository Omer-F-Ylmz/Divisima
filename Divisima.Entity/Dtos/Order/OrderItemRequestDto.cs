using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş kalemi isteği (frontend sepet kalemi: ürün + beden + adet).
    public class OrderItemRequestDto : IDto
    {
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
    }
}
