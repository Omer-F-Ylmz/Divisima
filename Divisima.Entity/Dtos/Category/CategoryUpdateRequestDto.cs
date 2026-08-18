using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Category
{
    // Açıklayıcı yorum: Kategori güncelleme isteği (admin).
    public class CategoryUpdateRequestDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public int display_order { get; set; }
    }
}
