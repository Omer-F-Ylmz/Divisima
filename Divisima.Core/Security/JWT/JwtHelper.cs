using System.IdentityModel.Tokens.Jwt;
using Divisima.Core.Security.Tokens;
using System.Security.Claims;
using System.Text;
using Divisima.Core.Entities.Abstract;
using Divisima.Core.Utilities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: JWT üretimi (Cafixo JwtHelper kalıbı). ITokenHelper implementasyonu.
    public class JwtHelper : ITokenHelper
    {
        private readonly TokenOptions _tokenOptions;

        public JwtHelper(IConfiguration configuration)
        {
            _tokenOptions = configuration.GetSection("TokenOptions").Get<TokenOptions>();
        }

        // Açıklayıcı yorum: Kullanıcı için imzalı JWT üret (Customer tipi claim'leriyle)
        public AccessToken CreateToken(IUser user)
        {
            var expiration = DateTime.Now.AddMinutes(_tokenOptions.AccessTokenExpiration);
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_tokenOptions.SecurityKey));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.id.ToString()),
                new Claim(ClaimTypes.Email, user.email ?? ""),
                // Açıklayıcı yorum: Kullanıcı tipi claim'i (RequireUserType yetkilendirmesi bunu okur)
                // Aciklayici yorum: user_type entity'den okunur (0/unset -> Customer guvenli varsayilan; Admin = 1)
                new Claim("user_type", ((int)(user.user_type == 0 ? (byte)UserTypeEnum.Customer : user.user_type)).ToString()),
                // Açıklayıcı yorum: jti - benzersiz token id (revocation/izleme; refresh rotation ile birlikte)
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                // Açıklayıcı yorum: auth_time - kimlik doğrulama zamanı (UTC unix saniye). RequireRecentAuth bunu okur
                // (hassas işlemler için "yakın zamanda giriş" zorunlulugu). NOT: su an login+refresh ayni token uretimini
                // kullandigindan refresh'te de yenilenir ("son aktiflik"); gercek step-up icin refresh'te orijinal auth_time tasinmali.
                new Claim("auth_time",
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var jwt = new JwtSecurityToken(
                issuer: _tokenOptions.Issuer,
                audience: _tokenOptions.Audience,
                expires: expiration,
                notBefore: DateTime.Now,
                claims: claims,
                signingCredentials: signingCredentials);

            var token = new JwtSecurityTokenHandler().WriteToken(jwt);

            return new AccessToken
            {
                Token = token,
                Expiration = expiration,
                RefreshToken = SecureTokenGenerator.Generate(),
                RefreshTokenExpiration = expiration.AddDays(7)
            };
        }
    }
}

