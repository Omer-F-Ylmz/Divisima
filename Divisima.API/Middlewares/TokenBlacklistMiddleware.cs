using Divisima.Core.Security.JWT;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Her kimlikli istekte token'ın jti'sini kara listeye karşı kontrol eder.
    // Logout edilmiş / iptal edilmiş access token süresi dolmasa bile burada reddedilir (401).
    public class TokenBlacklistMiddleware
    {
        private readonly RequestDelegate _next;
        public TokenBlacklistMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, ITokenBlacklist blacklist)
        {
            var jti = context.User?.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti) && await blacklist.IsRevokedAsync(jti))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { Success = false, Message = "Oturum sonlandırılmış. Lütfen tekrar giriş yapın." });
                return;
            }
            await _next(context);
        }
    }
}
