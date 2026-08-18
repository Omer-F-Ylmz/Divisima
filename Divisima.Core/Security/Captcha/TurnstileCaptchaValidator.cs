using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Security.Captcha
{
    // Açıklayıcı yorum: GERÇEK Cloudflare Turnstile doğrulaması. siteverify endpoint'ine HttpClient POST eder.
    // "Captcha:Enabled" false ise (dev) atlanır. HttpClient DI'dan (IHttpClientFactory) gelir - socket exhaustion yok.
    public class TurnstileCaptchaValidator : ICaptchaValidator
    {
        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<TurnstileCaptchaValidator> _logger;

        public TurnstileCaptchaValidator(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<TurnstileCaptchaValidator> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> ValidateAsync(string token, string? remoteIp)
        {
            // Açıklayıcı yorum: Captcha kapalıysa (dev) her zaman geç
            if (!bool.TryParse(_config["Captcha:Enabled"], out var enabled) || !enabled)
                return true;

            if (string.IsNullOrWhiteSpace(token))
                return false;

            var secret = _config["Captcha:SecretKey"] ?? "";
            var client = _httpClientFactory.CreateClient("turnstile");

            var form = new List<KeyValuePair<string, string>>
            {
                new("secret", secret),
                new("response", token)
            };
            if (!string.IsNullOrEmpty(remoteIp))
                form.Add(new("remoteip", remoteIp));

            try
            {
                using var response = await client.PostAsync(VerifyUrl, new FormUrlEncodedContent(form));
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                // Açıklayıcı yorum: {"success":true/false,"error-codes":[...]}
                return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            }
            catch (Exception ex)
            {
                // Açıklayıcı yorum: Doğrulama servisine ulaşılamazsa GÜVENLİ TARAFTA KAL - reddet (fail-closed)
                _logger.LogWarning(ex, "Turnstile siteverify erişilemedi");
                return false;
            }
        }
    }
}
