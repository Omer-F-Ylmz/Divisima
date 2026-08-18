using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Fatura servisi. Sipariş onayında fatura üretir; müşteri kendi faturasını görür.
    public interface IInvoiceService
    {
        // Açıklayıcı yorum: Sipariş için fatura oluştur (idempotent - varsa tekrar üretmez)
        Task<(HttpStatusCode, Result)> GenerateForOrder(int orderId);
        Task<(HttpStatusCode, Result)> GetMyInvoices(int customerId);
        Task<(HttpStatusCode, Result)> GetByOrder(int orderId, int customerId);
    }
}
