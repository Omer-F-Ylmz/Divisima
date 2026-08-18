namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: CSRF koruması (double-submit cookie deseni). Cookie tabanlı auth (refresh_token) olduğu için,
    // durum değiştiren isteklerde X-CSRF-Token header'ı ile csrf cookie eşleşmeli. Bearer JWT ile gelen
    // API istekleri (SPA) header taşıdığından zaten CSRF'e kapalı; bu, cookie taşıyan tarayıcı istekleri için ek kalkan.
    public class AntiforgeryMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly HashSet<string> SafeMethods = new() { "GET", "HEAD", "OPTIONS", "TRACE" };

        public AntiforgeryMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            // Açıklayıcı yorum: Yalnız durum değiştiren + cookie taşıyan istekleri denetle
            var method = context.Request.Method;
            var hasAuthCookie = context.Request.Cookies.ContainsKey("refresh_token");
            var hasBearer = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ");

            if (!SafeMethods.Contains(method) && hasAuthCookie && !hasBearer)
            {
                var headerToken = context.Request.Headers["X-CSRF-Token"].ToString();
                var cookieToken = context.Request.Cookies["csrf_token"];
                if (string.IsNullOrEmpty(headerToken) || headerToken != cookieToken)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { Success = false, Message = "CSRF doğrulaması başarısız." });
                    return;
                }
            }
            await _next(context);
        }
    }
}
