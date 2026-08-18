namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: JWT ayarları (appsettings.json "TokenOptions" bölümünden bağlanır).
    public class TokenOptions
    {
        public string Audience { get; set; }
        public string Issuer { get; set; }
        public int AccessTokenExpiration { get; set; }   // dakika
        public string SecurityKey { get; set; }
    }
}
