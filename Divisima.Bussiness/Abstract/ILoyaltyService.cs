using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Sadakat puanı - siparişte kazan, krediye çevir. Bakiye Customer.loyalty_points.
    public interface ILoyaltyService
    {
        Task<(HttpStatusCode, Result)> EarnPoints(int customerId, int points, string reason, int? orderId);
        Task<(HttpStatusCode, Result)> EarnFromOrder(int customerId, decimal orderTotal, int orderId);
        // Aciklayici yorum: Siparis iptalinde kazanilan puani geri al (farming engeli) - order_id ledger'dan bulunur.
        Task<(HttpStatusCode, Result)> ReverseForOrder(int customerId, int orderId);
        Task<(HttpStatusCode, Result)> RedeemForCredit(int customerId, int points);
        Task<(HttpStatusCode, Result)> GetBalance(int customerId);
        Task<(HttpStatusCode, Result)> GetTier(int customerId);
        Task<(HttpStatusCode, Result)> GetHistory(int customerId);
    }
}
