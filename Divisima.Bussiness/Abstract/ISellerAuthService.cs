using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Seller;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Satıcı kimlik servisi (müşteri auth'undan ayrı). Kayıt -> Pending, giriş -> JWT (user_type=Seller).
    public interface ISellerAuthService
    {
        Task<(HttpStatusCode, Result)> Register(SellerRegisterRequestDto dto);
        Task<(HttpStatusCode, Result)> Login(SellerLoginRequestDto dto);
    }
}
