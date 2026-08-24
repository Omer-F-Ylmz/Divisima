using Divisima.Core.Security.RateLimiting;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Yol bazlı dağıtık rate limit middleware (auth/ödeme sıkı, genel gevşek).
    //
    // DALGA D / D5 - IKI DEGISIKLIK:
    //  (1) Kova tanimlari artik `RateLimitPolitikasi`den geliyor; auth limiti KAYNAKTA SABIT 5
    //      DEGIL, yapilandirmadan okunuyor ve YERLESIK yolla AYNI degeri goruyor.
    //  (2) Middleware artik HER ZAMAN pipeline'da (eskiden yalniz `Redis:Enabled=true` iken).
    //      Gerekce: `IDistributedRateLimiter` her iki dalda da kayitli (Redis ya da in-memory),
    //      yani middleware'in Redis'e bagimliligi YOK - yalnizca ARKA DEPOSU degisiyor.
    //      Boylece dev/test ve URETIM AYNI BORU HATTINI kosuyor; onceden uretimin gercek
    //      rate limit yolu hicbir testte kosmuyordu (olculdu) ve ayrisma bu yuzden gorunmedi.
    public class RedisRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedRateLimiter _limiter;
        private readonly RateLimitPolitikasi _politika;

        public RedisRateLimitMiddleware(RequestDelegate next, IDistributedRateLimiter limiter, RateLimitPolitikasi politika)
        {
            _next = next;
            _limiter = limiter;
            _politika = politika;
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

            // Yol -> kova secimi TEK KAYNAKTAN (RateLimitPolitikasi). Kultursuz eslesme ve
            // limit degerleri orada; burada kopyasi TUTULMAZ - ayrisma tam da oyle olusmustu.
            var (scope, limit) = _politika.KapsamSec(path);
            var window = _politika.PencereSaniye;

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
