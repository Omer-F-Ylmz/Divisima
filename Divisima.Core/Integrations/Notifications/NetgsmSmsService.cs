using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Integrations.Notifications
{
    // Açıklayıcı yorum: Netgsm SMS implementasyonu iskeleti (Türkiye'de yaygın). "Sms:Enabled" false ise no-op.
    // Gerçek gönderim: Netgsm HTTP API (usercode/password/msgheader). HttpClient DI'dan.
    public class NetgsmSmsService : ISmsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<NetgsmSmsService> _logger;

        public NetgsmSmsService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<NetgsmSmsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        private bool Enabled => bool.TryParse(_config["Sms:Enabled"], out var v) && v;

        public async Task<bool> SendAsync(string phoneNumber, string message)
        {
            // Açıklayıcı yorum: SMS kapalıysa (dev) sessizce başarılı say
            if (!Enabled || string.IsNullOrEmpty(phoneNumber))
                return true;

            var usercode = _config["Sms:UserCode"] ?? "";
            var password = _config["Sms:Password"] ?? "";
            var header = _config["Sms:MsgHeader"] ?? "DIVISIMA";

            try
            {
                var client = _httpClientFactory.CreateClient("sms");
                // Açıklayıcı yorum: Netgsm bir GET/POST HTTP API'si; gerçek uçtaki parametreler doldurulur
                var url = $"https://api.netgsm.com.tr/sms/send/get?usercode={Uri.EscapeDataString(usercode)}" +
                          $"&password={Uri.EscapeDataString(password)}&gsmno={Uri.EscapeDataString(phoneNumber)}" +
                          $"&message={Uri.EscapeDataString(message)}&msgheader={Uri.EscapeDataString(header)}";
                using var res = await client.GetAsync(url);
                var body = await res.Content.ReadAsStringAsync();
                // Açıklayıcı yorum: Netgsm başarı kodu "00" veya "01/02" ile başlar
                var success = res.IsSuccessStatusCode && (body.StartsWith("00") || body.StartsWith("01") || body.StartsWith("02"));
                if (!success) _logger.LogWarning("SMS gönderilemedi: {Body}", body);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS gönderilemedi");
                return false;
            }
        }
    }
}
