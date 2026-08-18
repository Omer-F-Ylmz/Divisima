using Divisima.Core.Entities.Abstract;

namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: JWT üretim arayüzü (Cafixo ITokenHelper kalıbı). JwtHelper implemente eder.
    public interface ITokenHelper
    {
        AccessToken CreateToken(IUser user);
    }
}
