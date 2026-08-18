namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: Üretilen JWT token bilgisi (Cafixo AccessToken kalıbı).
    public class AccessToken
    {
        public string Token { get; set; }
        public DateTime Expiration { get; set; }
        public string RefreshToken { get; set; }
        public DateTime RefreshTokenExpiration { get; set; }
    }
}
