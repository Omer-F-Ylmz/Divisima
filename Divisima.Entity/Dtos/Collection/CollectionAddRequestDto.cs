using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Entity.Dtos.Collection
{
    // Açıklayıcı yorum: Koleksiyon ekleme isteği (admin). curator_name Ambassador tipinde dolar.
    public class CollectionAddRequestDto : IDto
    {
        public string name { get; set; }
        public string slug { get; set; }
        public CollectionTypeEnum collection_type { get; set; }
        public string curator_name { get; set; }
        public string subtitle { get; set; }
        public string gradient { get; set; }
        public List<int> product_ids { get; set; }   // koleksiyona eklenecek ürünler
    }
}
