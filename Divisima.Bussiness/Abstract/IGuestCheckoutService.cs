using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Guest;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Misafir checkout - hesapsız sipariş. Var olan e-postaya giriş yönlendirir.
    public interface IGuestCheckoutService
    {
        Task<(HttpStatusCode, Result)> PlaceGuestOrder(GuestCheckoutDto dto);
    }
}
