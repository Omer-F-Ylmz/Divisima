using Hangfire.Dashboard;

namespace Divisima.API.Services
{
    // Açıklayıcı yorum: Hangfire panosuna yalnızca oturum açmış Admin erişebilir (herkese açık DEĞİL).
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            // Açıklayıcı yorum: Kimlik doğrulanmış + user_type = Admin (1)
            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated != true) return false;
            var userType = user.FindFirst("user_type")?.Value;
            return userType == "1";
        }
    }
}
