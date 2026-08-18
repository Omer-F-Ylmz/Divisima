using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Divisima.Core.Security.Authorization
{
    // Açıklayıcı yorum: Step-up auth - hassas işlemlerde token'ın "auth_time" claim'i son N dakika içinde olmalı.
    // Değilse 401 + "yeniden giriş gerekli". 2FA kapatma, şifre/e-posta değişimi, hesap silme gibi işlemlerde kullanılır.
    // Çalınan uzun ömürlü oturumla kritik işlem yapılmasını engeller.
    public class RequireRecentAuthAttribute : ActionFilterAttribute
    {
        private readonly int _maxMinutes;
        public RequireRecentAuthAttribute(int maxMinutes = 10) => _maxMinutes = maxMinutes;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var authTimeClaim = context.HttpContext.User.FindFirst("auth_time")?.Value;
            if (authTimeClaim == null || !long.TryParse(authTimeClaim, out var authTimeUnix))
            {
                context.Result = new ObjectResult(new { Success = false, Message = "Bu işlem için yeniden giriş yapmalısınız." })
                { StatusCode = 401 };
                return;
            }
            var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeUnix).UtcDateTime;
            if (DateTime.UtcNow - authTime > TimeSpan.FromMinutes(_maxMinutes))
            {
                context.Result = new ObjectResult(new { Success = false, Message = "Oturumunuz bu işlem için çok eski, yeniden giriş yapın." })
                { StatusCode = 401 };
            }
        }
    }
}
