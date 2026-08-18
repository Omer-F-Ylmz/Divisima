using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Hesaplanan ürün listeleri (sipariş verisinden). Vitrin/keşif için.
    public interface IMerchandisingService
    {
        Task<(HttpStatusCode, Result)> GetBestSellers(int take);   // en çok satılan (adet)
        Task<(HttpStatusCode, Result)> GetTrending(int take);      // son 30 günde en çok sipariş edilen
        Task<(HttpStatusCode, Result)> GetNewArrivals(int take);   // en yeni ürünler
    }
}
