using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ürün görsel yönetimi. Yükleme (doğrulama + depolama), listeleme, silme, birincil belirleme.
    public interface IProductImageService
    {
        // Açıklayıcı yorum: content = dosya baytları; tür/boyut serviste doğrulanır
        Task<(HttpStatusCode, Result)> Upload(int productId, byte[] content, string fileName, string contentType, bool isPrimary);
        Task<(HttpStatusCode, Result)> GetByProduct(int productId);
        Task<(HttpStatusCode, Result)> Delete(int imageId);
        Task<(HttpStatusCode, Result)> SetPrimary(int imageId);
    }
}
