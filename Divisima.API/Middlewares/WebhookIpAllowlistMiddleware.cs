using Microsoft.Extensions.Configuration;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Webhook endpoint'ini yalnız sağlayıcı (Iyzico) IP aralıklarına açar (imzaya EK katman).
    // appsettings "Webhook:AllowedIps" listesi boşsa atlanır (dev). Bilinmeyen IP -> 403.
    public class WebhookIpAllowlistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HashSet<string> _allowed;

        public WebhookIpAllowlistMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _allowed = (config.GetSection("Webhook:AllowedIps").Get<string[]>() ?? Array.Empty<string>()).ToHashSet();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Açıklayıcı yorum: Sadece webhook yolunu ve allowlist doluysa denetle
            if (context.Request.Path.StartsWithSegments("/api/payment/webhook") && _allowed.Count > 0)
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";
                if (!_allowed.Contains(ip))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { Success = false, Message = "Erişim reddedildi." });
                    return;
                }
            }
            await _next(context);
        }
    }
}
