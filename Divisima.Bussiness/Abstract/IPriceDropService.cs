using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.PriceDrop;
namespace Divisima.Bussiness.Abstract
{
    public interface IPriceDropService
    {
        Task<(HttpStatusCode, Result)> Subscribe(PriceDropSubscribeDto dto);
        Task NotifyPriceDrop(int productId, decimal newPrice);
    }
}
