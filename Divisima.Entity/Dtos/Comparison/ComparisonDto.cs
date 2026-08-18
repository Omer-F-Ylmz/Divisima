using System.Collections.Generic;
using Divisima.Core.Utilities.Dtos;
using Divisima.Entity.Entities;
using ProductEntity = Divisima.Entity.Entities.Product;
namespace Divisima.Entity.Dtos.Comparison
{
    public class CompareRequestDto : IDto { public List<int> product_ids { get; set; } }

    // Açıklayıcı yorum: Karşılaştırma sonucu - ürünler + her ürünün özellik sözlüğü + tüm anahtar birleşimi
    public class ComparisonResultDto : IDto
    {
        public List<ProductEntity> products { get; set; }
        public Dictionary<int, Dictionary<string, string>> attributes { get; set; } // product_id -> (key -> value)
        public List<string> attribute_keys { get; set; } // tablo satırları için tüm anahtarlar
    }
}
