using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Category
{
    // Açıklayıcı yorum: Kategori ekleme isteği (admin).
    public class CategoryAddRequestDto : IDto
    {
        public string name { get; set; }
        public string slug { get; set; }
        public int display_order { get; set; }
    }
}
