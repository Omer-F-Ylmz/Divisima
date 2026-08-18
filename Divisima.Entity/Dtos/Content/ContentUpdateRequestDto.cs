using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Content
{
    // Açıklayıcı yorum: İçerik güncelleme isteği (admin).
    public class ContentUpdateRequestDto : IDto
    {
        public int id { get; set; }
        public string title_tr { get; set; }
        public string title_en { get; set; }
        public string body_tr { get; set; }
        public string body_en { get; set; }
    }
}
