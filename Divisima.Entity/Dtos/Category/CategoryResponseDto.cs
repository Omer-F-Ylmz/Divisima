using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Category
{
    // Açıklayıcı yorum: Kategori dönüşü, alt kategorileriyle (frontend menü + filtre).
    public class CategoryResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public int display_order { get; set; }
        public bool is_active { get; set; }
        public List<SubCategoryResponseDto> sub_categories { get; set; }
    }
}
