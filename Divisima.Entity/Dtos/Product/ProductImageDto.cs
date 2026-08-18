using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün görseli görüntüleme.
    public class ProductImageDto : IDto
    {
        public int id { get; set; }
        public int product_id { get; set; }
        public string image_url { get; set; }
        public int sort_order { get; set; }
        public bool is_primary { get; set; }
    }
}
