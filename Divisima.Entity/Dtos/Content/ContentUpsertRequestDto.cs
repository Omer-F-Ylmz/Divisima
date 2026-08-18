using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Content
{
    // Açıklayıcı yorum: İçerik ekle/güncelle isteği (admin).
    public class ContentUpsertRequestDto : IDto
    {
        public int? id { get; set; }
        public string slug { get; set; }
        public string title_tr { get; set; }
        public string title_en { get; set; }
        public string body_tr { get; set; }
        public string body_en { get; set; }
    }
}
