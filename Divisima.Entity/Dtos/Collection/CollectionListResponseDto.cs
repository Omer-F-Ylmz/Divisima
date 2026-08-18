using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Collection
{
    // Açıklayıcı yorum: Koleksiyon liste dönüşü (ana sayfa kartları + elçiler).
    public class CollectionListResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public string collection_type { get; set; }
        public string curator_name { get; set; }
        public string subtitle { get; set; }
        public string gradient { get; set; }
        public int product_count { get; set; }
        public bool is_active { get; set; }
    }
}
