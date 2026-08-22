using Divisima.Core.Security.RateLimiting;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Redis dağıtık rate limit middleware. Yol bazlı limit (auth/ödeme sıkı, genel gevşek).
    // Yalnız Redis açıkken pipeline'a eklenir; kapalıyken .NET yerleşik limiter devrede kalır.
    public class RedisRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedRateLimiter _limiter;

        public RedisRateLimitMiddleware(RequestDelegate next, IDistributedRateLimiter limiter)
        {
            _next = next;
            _limiter = limiter;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // ══ KALITE SUPURMESI B3 - URL YOLU KIMLIK DIZGESIDIR, KULTURSUZ ESLESIR ═════════
            // ONCEKI HALI: `Path.Value?.ToLower()` + kultur duyarli `Contains`.
            // Uygulama tr-TR'ye pinli oldugu icin 'I' -> 'ı' oluyordu. OLCULDU:
            //   '/API/AUTH/LOGIN'.ToLower()  ->  '/apı/auth/logın'
            //   .Contains("/auth/login")     ->  FALSE     (invariant'ta TRUE)
            // ASP.NET rotalama BUYUK/KUCUK HARF DUYARSIZ oldugundan /API/AUTH/LOGIN gecerli
            // bir istektir; yani saldirgan yalnizca URL'yi buyuk harfle yazarak 5/dk'lik
            // KABA KUVVET savunmasindan kacip 100/dk'lik genel kovaya dusuyordu.
            // Yol bir MAKINE dizgesidir: OrdinalIgnoreCase ile eslenir, ToLower KULLANILMAZ.
            var path = context.Request.Path.Value ?? "";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Açıklayıcı yorum: Yol bazlı limit - brute-force hassas uçlar sıkı
            int limit; int window = 60; string scope;
            if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/auth/register", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/auth/forgot", StringComparison.OrdinalIgnoreCase))
            { limit = 5; scope = "auth"; }
            else if (path.Contains("/payment/", StringComparison.OrdinalIgnoreCase))
            { limit = 10; scope = "payment"; }
            else
            { limit = 100; scope = "global"; }

            var result = await _limiter.CheckAsync($"{scope}:{ip}", limit, window);

            // Açıklayıcı yorum: Standart rate limit başlıkları
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();

            if (!result.Allowed)
            {
                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Çok fazla istek. Lütfen biraz sonra tekrar deneyin." });
                return;
            }

            await _next(context);
        }
    }
}
