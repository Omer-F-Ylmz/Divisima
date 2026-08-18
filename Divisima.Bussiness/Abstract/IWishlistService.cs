using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Favori listesi iş servisi (toggle + listeleme).
    public interface IWishlistService
    {
        Task<(HttpStatusCode, Result)> Toggle(int customerId, int productId);
        Task<(HttpStatusCode, Result)> MoveToCart(int customerId, int productId, string size, int quantity);   // varsa çıkar, yoksa ekle
        Task<(HttpStatusCode, Result)> GetByCustomer(int customerId);
    }
}
