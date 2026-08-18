using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Cart;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Kalıcı sepet iş servisi (müşteriye bağlı).
    public interface ICartService
    {
        Task<(HttpStatusCode, Result)> AddItem(CartItemRequestDto dto);   // stok kontrollü
        Task<(HttpStatusCode, Result)> RemoveItem(int customerId, int productId, string size);
        Task<(HttpStatusCode, Result)> GetCart(int customerId);
        Task<(HttpStatusCode, Result)> ClearCart(int customerId);
        Task<(HttpStatusCode, Result)> SaveForLater(int customerId, int productId, string size);
        Task<(HttpStatusCode, Result)> MoveToCart(int customerId, int productId, string size);
    }
}
