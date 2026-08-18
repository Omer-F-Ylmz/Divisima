using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Auth
{
    // Açıklayıcı yorum: Müşteri giriş isteği.
    public class CustomerLoginRequestDto : IDto
    {
        public string email { get; set; }
        public string password { get; set; }
    }
}
