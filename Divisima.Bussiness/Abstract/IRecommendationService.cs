using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün öneri motoru. Sipariş geçmişinden birliktelik + kategori benzerliği.
    public interface IRecommendationService
    {
        // Açıklayıcı yorum: "Bunu alanlar şunu da aldı" - aynı siparişlerde geçen diğer ürünler (sıklığa göre)
        Task<(HttpStatusCode, Result)> GetFrequentlyBoughtTogether(int productId, int limit = 8);
        // Açıklayıcı yorum: "Benzer ürünler" - aynı kategorideki diğer aktif ürünler
        Task<(HttpStatusCode, Result)> GetSimilarProducts(int productId, int limit = 8);
    }
}
