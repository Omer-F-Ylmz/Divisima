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
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Açıklayıcı yorum: Yol bazlı limit - brute-force hassas uçlar sıkı
            int limit; int window = 60; string scope;
            if (path.Contains("/auth/login") || path.Contains("/auth/register") || path.Contains("/auth/forgot"))
            { limit = 5; scope = "auth"; }
            else if (path.Contains("/payment/"))
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
