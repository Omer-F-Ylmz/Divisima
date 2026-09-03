using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Divisima.Core.Entities.Abstract;
using Divisima.Core.Security.Tokens;
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
        public AccessToken CreateToken(IUser user, DateTime? authTime = null)
        {
            // ══ GF-3 / K11 - JWT ZAMAN EKSENI UTC ══════════════════════════════════════════
            // `exp` ve `nbf` JWT'de SANIYE cinsinden UTC epoch olarak tasinir; kutuphane
            // `DateTime`i ONCE UTC'ye cevirir. Yani teldeki deger BUGUN DE dogruydu -
            // degisen sey KAYNAK Kind'i.
            //
            // BILINCLI KABUL (merkez karari, DUR-6): `AccessToken.Expiration` bu degerden
            // turer ve `CustomerLoginResponseDto.expiration` olarak YANIT GOVDESINE cikar.
            // Ozel bir `DateTimeConverter` olmadigi icin System.Text.Json `Kind`e gore yazar:
            // eskiden "...+03:00", artik "...Z". Yani LOGIN YANIT GOVDESI DEGISTI.
            // Olculdu: frontend bu alani TUKETMIYOR (0 gecis) ve hicbir pin OLCMUYORDU (0).
            // `SellerLoginResponseDto` de ayni helper'dan beslendigi icin DOLAYLI etkilenir -
            // Seller KODUNA dokunulmadi, yalnizca degerin BICIMI degisti (muhurde ayri satir).
            var expiration = DateTime.UtcNow.AddMinutes(_tokenOptions.AccessTokenExpiration);
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
                // ══ GF-1 / K3 (C-2) - auth_time ARTIK ZINCIR BOYUNCA TASINIYOR ═════════════
                //
                // Bu satirin ustundeki eski not sorunu ZATEN TARIF EDIYORDU: "su an login+refresh
                // ayni token uretimini kullandigindan refresh'te de yenilenir; gercek step-up icin
                // refresh'te orijinal auth_time TASINMALI". Olculen sonuc: `RequireRecentAuth(10)`
                // step-up'i, calinmis bir refresh cerezi ile SURESIZ uzatilabiliyordu - her refresh
                // saati sifirliyordu.
                //
                // ARTIK: cagiran oturumun GIRIS anini verir (`user_sessions.auth_time`). Login ve
                // 2FA tamamlanmasi `null` gecer -> SIMDI (ikisi de kimlik dogrulamadir, step-up
                // HAKLI olarak acilir). Refresh rotasyonu ESKI degeri gecer -> saat SIFIRLANMAZ.
                // `null` ayrica GF-1 ONCESI oturumlarin (auth_time kolonu NULL) statuko davranisidir.
                //
                // KIND SABITLENIR - YOKSA PATLAR: `new DateTimeOffset(dt, TimeSpan.Zero)`
                // `dt.Kind == Local` ise ArgumentException firlatir; EF Core `datetime2`
                // kolonunu `Unspecified` olarak dondurur. Kolona UTC yazildigi icin (bkz.
                // AuthManager) burada Kind ACIKCA Utc'ye sabitlenir - donusum YAPILMAZ.
                new Claim("auth_time",
                    new DateTimeOffset(
                        authTime.HasValue
                            ? DateTime.SpecifyKind(authTime.Value, DateTimeKind.Utc)
                            : DateTime.UtcNow,
                        TimeSpan.Zero).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var jwt = new JwtSecurityToken(
                issuer: _tokenOptions.Issuer,
                audience: _tokenOptions.Audience,
                expires: expiration,
                notBefore: DateTime.UtcNow,   // GF-3/K11 - expiration ile AYNI eksende
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

