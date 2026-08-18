using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Auth
{
    // Açıklayıcı yorum: Giriş sonucu - JWT token + kullanıcı bilgisi (Cafixo AccessToken kalıbı).
    public class CustomerLoginResponseDto : IDto
    {
        public int customer_id { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string token { get; set; }
        public DateTime expiration { get; set; }
        public string refresh_token { get; set; }
    }
}
