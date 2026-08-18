using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Product
{
    // Açıklayıcı yorum: Ürün liste/grid dönüşü (frontend cardHTML). Toplam stok özet olarak döner.
    public class ProductListResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string brand { get; set; }
        public int category_id { get; set; }
        public string category_name { get; set; }
        public decimal price { get; set; }
        public decimal? old_price { get; set; }
        public string color_hex { get; set; }
        public int total_stock { get; set; }
        public string? image_url { get; set; }
        public List<string> sizes { get; set; } = new(); // frontend sizes[] - müsait bedenler
        public decimal average_rating { get; set; }
        public int review_count { get; set; }
        public bool is_active { get; set; }
    }
}
