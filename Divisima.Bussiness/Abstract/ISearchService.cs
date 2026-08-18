using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Search;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün arama iş servisi (metin + filtre + sıralama + sayfalama).
    public interface ISearchService
    {
        Task<(HttpStatusCode, Result)> SearchProducts(ProductSearchRequestDto dto);
    }
}
