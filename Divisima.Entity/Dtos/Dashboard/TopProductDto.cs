using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Dashboard
{
    // Açıklayıcı yorum: En çok satan ürün - ürün id/ad + satılan adet + getirdiği ciro.
    public class TopProductDto : IDto
    {
        public int product_id { get; set; }
        public string product_name { get; set; }
        public int total_quantity { get; set; }
        public decimal total_revenue { get; set; }
    }
}
