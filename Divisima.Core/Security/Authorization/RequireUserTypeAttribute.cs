using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Divisima.Core.Security.Authorization
{
    // Açıklayıcı yorum: Kullanıcı tipi bazlı yetkilendirme (Cafixo RequireUserType kalıbı).
    // Kullanım: [RequireUserType(UserTypeEnum.Admin)] veya [RequireUserType(UserTypeEnum.Customer)].
    public class RequireUserTypeAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "RequireUserType_";

        public RequireUserTypeAttribute(UserTypeEnum userType)
        {
            UserType = userType;
        }

        public UserTypeEnum UserType
        {
            get
            {
                if (Policy != null && Policy.StartsWith(PolicyPrefix)
                    && Enum.TryParse<UserTypeEnum>(Policy.Substring(PolicyPrefix.Length), out var t))
                    return t;
                return default;
            }
            set => Policy = $"{PolicyPrefix}{value}";
        }
    }
}
