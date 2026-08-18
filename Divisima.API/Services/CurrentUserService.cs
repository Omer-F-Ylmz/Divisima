using System.Security.Claims;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;

namespace Divisima.API.Services
{
    // Açıklayıcı yorum: ICurrentUserService implementasyonu - HttpContext claim'lerinden okur.
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User;

        public int? UserId
        {
            get
            {
                var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(claim, out var id) ? id : null;
            }
        }

        public int? UserType
        {
            get
            {
                var claim = User?.FindFirst("user_type")?.Value;
                return int.TryParse(claim, out var t) ? t : null;
            }
        }

        public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
        public bool IsAdmin => UserType == (int)UserTypeEnum.Admin;

        // Açıklayıcı yorum: Kimlik yoksa UnauthorizedAccessException - global exception middleware 401 çevirir
        public int GetRequiredUserId()
        {
            return UserId ?? throw new UnauthorizedAccessException("Kimlik doğrulanamadı.");
        }
    }
}
