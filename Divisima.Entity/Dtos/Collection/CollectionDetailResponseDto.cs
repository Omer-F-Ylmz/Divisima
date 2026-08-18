using Divisima.Core.Utilities.Dtos;
using Divisima.Entity.Dtos.Product;

namespace Divisima.Entity.Dtos.Collection
{
    // Açıklayıcı yorum: Koleksiyon detay dönüşü - içindeki ürünlerle (frontend showCollection).
    public class CollectionDetailResponseDto : IDto
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public string collection_type { get; set; }
        public string curator_name { get; set; }
        public string subtitle { get; set; }
        public string gradient { get; set; }
        public List<ProductListResponseDto> products { get; set; }
    }
}
