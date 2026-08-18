using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.ProductAttribute;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün özellikleri + faceted search. Admin özellik atar; müşteri facet'lerle filtreler.
    public interface IProductAttributeService
    {
        Task<(HttpStatusCode, Result)> SetAttributes(SetAttributesDto dto);   // admin
        Task<(HttpStatusCode, Result)> GetAttributes(int productId);
        Task<(HttpStatusCode, Result)> GetFacets();                            // tüm anahtar/değer + sayaç
        Task<(HttpStatusCode, Result)> FilterByAttributes(FacetFilterDto dto); // eşleşen ürünler + güncel facet
    }
}
