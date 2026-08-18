using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Auth
{
    // Açıklayıcı yorum: Token yenileme isteği. Access token süresi dolunca refresh_token ile yeni token alınır.
    public class RefreshTokenRequestDto : IDto
    {
        public string refresh_token { get; set; }
    }
}
