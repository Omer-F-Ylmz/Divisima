using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Son görüntülenen ürünler (kişiselleştirme).
    public interface IRecentlyViewedService
    {
        // Açıklayıcı yorum: Ürün görüntülemeyi kaydet (upsert - varsa viewed_at güncelle)
        Task<(HttpStatusCode, Result)> RecordView(int customerId, int productId);
        // Açıklayıcı yorum: Müşterinin son görüntülediği ürünler (en yeniden eskiye)
        Task<(HttpStatusCode, Result)> GetRecentlyViewed(int customerId, int limit = 10);
    }
}
