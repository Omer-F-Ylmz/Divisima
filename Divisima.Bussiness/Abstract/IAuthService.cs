using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Auth;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Kimlik doğrulama servisi (Cafixo IAuthService kalıbı - Customer tipi için).
    public interface IAuthService
    {
        Task<(HttpStatusCode, Result)> Register(CustomerRegisterRequestDto dto);
        Task<(HttpStatusCode, Result)> Login(CustomerLoginRequestDto dto);

        // Açıklayıcı yorum: Refresh token ile yeni access token üret (oturum süresi uzatma)
        Task<(HttpStatusCode, Result)> RefreshToken(RefreshTokenRequestDto dto);
        Task<(HttpStatusCode, Result)> VerifyTwoFactor(string email, string code);

        // Açıklayıcı yorum: E-posta doğrulama
        Task<(HttpStatusCode, Result)> VerifyEmail(string token);
        Task<(HttpStatusCode, Result)> ResendVerification(string email);

        // Açıklayıcı yorum: Şifre sıfırlama + çıkış (oturum iptali)
        Task<(HttpStatusCode, Result)> ForgotPassword(ForgotPasswordRequestDto dto);
        Task<(HttpStatusCode, Result)> ResetPassword(ResetPasswordRequestDto dto);
        Task<(HttpStatusCode, Result)> Logout(int customerId, string? refreshToken);

        // Açıklayıcı yorum: GDPR/KVKK - hesap silme (unutulma hakkı) + veri dışa aktarma (taşınabilirlik)
        Task<(HttpStatusCode, Result)> ExportMyData(int customerId);

    }
}
