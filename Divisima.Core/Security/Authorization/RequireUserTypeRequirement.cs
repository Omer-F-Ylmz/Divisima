using Microsoft.AspNetCore.Authorization;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Core.Security.Authorization
{
    // Açıklayıcı yorum: Yetkilendirme gereksinimi (policy handler bunu değerlendirir).
    public class RequireUserTypeRequirement : IAuthorizationRequirement
    {
        public UserTypeEnum UserType { get; }
        public RequireUserTypeRequirement(UserTypeEnum userType) => UserType = userType;
    }
}
