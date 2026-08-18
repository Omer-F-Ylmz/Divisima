using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Hediye kartı - admin üretir, müşteri bozdurur (bakiye mağaza kredisine).
    public interface IGiftCardService
    {
        Task<(HttpStatusCode, Result)> Create(decimal amount);
        Task<(HttpStatusCode, Result)> CheckBalance(string code);
        Task<(HttpStatusCode, Result)> Redeem(int customerId, string code);
    }
}
