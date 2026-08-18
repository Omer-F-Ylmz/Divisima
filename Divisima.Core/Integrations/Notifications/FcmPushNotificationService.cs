using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Integrations.Notifications
{
    // Açıklayıcı yorum: FCM push implementasyonu (HTTP v1). "Push:Enabled" false ise no-op (dev).
    // Gerçek gönderim için FCM proje kimliği + OAuth2 token (service account) gerekir. HttpClient DI'dan.
    public class FcmPushNotificationService : IPushNotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<FcmPushNotificationService> _logger;

        public FcmPushNotificationService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<FcmPushNotificationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        private bool Enabled => bool.TryParse(_config["Push:Enabled"], out var v) && v;

        public async Task<bool> SendAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
        {
            // Açıklayıcı yorum: Push kapalıysa (dev) sessizce başarılı say
            if (!Enabled || string.IsNullOrEmpty(deviceToken))
                return true;

            var projectId = _config["Push:FcmProjectId"] ?? "";
            var accessToken = _config["Push:AccessToken"] ?? "";   // gerçekte service account'tan OAuth2 ile alınır

            var payload = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new { title, body },
                    data = data ?? new Dictionary<string, string>()
                }
            };

            try
            {
                var client = _httpClientFactory.CreateClient("fcm");
                var url = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Add("Authorization", $"Bearer {accessToken}");
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var res = await client.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                    _logger.LogWarning("FCM push başarısız: {Status}", res.StatusCode);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM push gönderilemedi");
                return false;
            }
        }

    }
}
