using Microsoft.AspNetCore.Authorization;

namespace Divisima.Core.Security.Authorization
{
    // Açıklayıcı yorum: Yetkilendirme handler'ı (Cafixo AuthorizationHandler kalıbı).
    // JWT'deki "user_type" claim'i, istenen tiple eşleşiyor mu kontrol eder.
    public class RequireUserTypeHandler : AuthorizationHandler<RequireUserTypeRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, RequireUserTypeRequirement requirement)
        {
            var claim = context.User.FindFirst("user_type");
            if (claim != null && int.TryParse(claim.Value, out var userType)
                && userType == (int)requirement.UserType)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}
