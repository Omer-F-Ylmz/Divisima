using System.Net;
using Divisima.Core.Utilities.Results;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Referans programı - kod üret, kayıtta bağla, ilk siparişte iki tarafa kredi.
    public interface IReferralService
    {
        Task<(HttpStatusCode, Result)> GetOrCreateMyCode(int customerId);
        Task<int?> ResolveReferrer(string code); // kod -> referrer customer id (kayıt sırasında)
        Task RewardOnFirstOrder(int customerId, int orderId); // ilk sipariş tamamlanınca ödül (post-commit)
    }
}
