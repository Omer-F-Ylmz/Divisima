using System.Collections.Generic;
using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.ProductAttribute
{
    public class AttributePairDto { public string key { get; set; } public string value { get; set; } }
    public class SetAttributesDto : IDto
    {
        public int product_id { get; set; }
        public List<AttributePairDto> attributes { get; set; }
    }
    // Açıklayıcı yorum: Faceted filtre isteği - anahtar/değer çiftleri (VE mantığı)
    public class FacetFilterDto : IDto
    {
        public List<AttributePairDto> filters { get; set; }
    }
}
