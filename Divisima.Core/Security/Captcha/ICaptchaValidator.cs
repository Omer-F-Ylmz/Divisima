namespace Divisima.Core.Security.Captcha
{
    // Açıklayıcı yorum: Bot koruması soyutlaması. Register/forgot-password/riskli login'de challenge doğrular.
    // reCAPTCHA / hCaptcha / Cloudflare Turnstile implementasyonu ile değiştirilir.
    public interface ICaptchaValidator
    {
        Task<bool> ValidateAsync(string token, string? remoteIp);
    }
}
