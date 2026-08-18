using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Payment;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Ödeme iş servisi. authenticatedCustomerId = JWT'den gelen gerçek kullanıcı
    // (sahiplik kontrolü: kullanıcı yalnızca KENDİ siparişini ödeyebilir).
    public interface IPaymentService
    {
        Task<(HttpStatusCode, Result)> Initialize(PaymentInitRequestDto dto, int authenticatedCustomerId);
        Task<(HttpStatusCode, Result)> HandleCallback(PaymentCallbackRequestDto dto);
    }
}
