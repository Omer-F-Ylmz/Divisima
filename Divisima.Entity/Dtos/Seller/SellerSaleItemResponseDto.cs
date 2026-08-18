using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Seller
{
    // Açıklayıcı yorum: Satıcı satış kalemi - satıcının ürünlerini içeren sipariş kalemleri (kendi kalemleri).
    public class SellerSaleItemResponseDto : IDto
    {
        public int order_id { get; set; }
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal line_total { get; set; }
        public bool is_cancelled { get; set; }
        public DateTime created_at { get; set; }
    }
}
