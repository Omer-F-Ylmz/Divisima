using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Category
{
    // Açıklayıcı yorum: Alt kategori dönüşü (menü/filtre).
    public class SubCategoryResponseDto : IDto
    {
        public int id { get; set; }
        public int category_id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
    }
}
