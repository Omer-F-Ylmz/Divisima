using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Mağaza kredisi - bakiye + defter. İade/hediye kartı kredi ekler; checkout kullanır.
    public interface IStoreCreditService
    {
        Task<(HttpStatusCode, Result)> AddCredit(int customerId, decimal amount, string reason, int? orderId);
        Task<(HttpStatusCode, Result)> UseCredit(int customerId, decimal amount, string reason, int? orderId);
        Task<(HttpStatusCode, Result)> GetBalance(int customerId);
        Task<(HttpStatusCode, Result)> GetHistory(int customerId);
    }
}
