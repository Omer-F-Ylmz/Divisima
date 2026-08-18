using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Return;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: İade/değişim servisi. Müşteri talep açar (sahiplik), admin işler (Iyzico refund).
    public interface IReturnService
    {
        Task<(HttpStatusCode, Result)> CreateReturn(ReturnCreateRequestDto dto);
        Task<(HttpStatusCode, Result)> ProcessReturn(ReturnProcessRequestDto dto);
        Task<(HttpStatusCode, Result)> GetMyReturns(int customerId);
        Task<(HttpStatusCode, Result)> GetPendingReturns();
    }
}
