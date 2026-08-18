namespace Divisima.Core.Security.TwoFactor
{
    // Açıklayıcı yorum: İki faktörlü doğrulama (TOTP - Google Authenticator uyumlu) soyutlaması.
    // Login'de şifre doğrulandıktan sonra, two_factor_enabled ise kod istenir.
    public interface ITwoFactorService
    {
        string GenerateSecret();
        string GenerateQrCodeUri(string email, string secret);
        bool ValidateCode(string secret, string code);
    }
}
