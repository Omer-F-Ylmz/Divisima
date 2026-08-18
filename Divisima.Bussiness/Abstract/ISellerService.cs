using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Satıcı paneli servisi. TÜM metotlar sellerId parametresi alır ve YALNIZCA o satıcının
    // verisini döner (izolasyon iş katmanında zorlanır; sellerId controller'da JWT'den gelir, client'tan DEĞİL).
    public interface ISellerService
    {
        Task<(HttpStatusCode, Result)> GetDashboardAsync(int sellerId);
        Task<(HttpStatusCode, Result)> GetMyProductsAsync(int sellerId);
        Task<(HttpStatusCode, Result)> GetMySalesAsync(int sellerId);
    }
}
