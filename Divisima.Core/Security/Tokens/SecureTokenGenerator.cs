using System.Security.Cryptography;

namespace Divisima.Core.Security.Tokens
{
    // Açıklayıcı yorum: Kriptografik güvenli token üreteci. Guid yerine (Guid tahmin edilebilirlik açısından token için ideal değil).
    // RandomNumberGenerator ile 256-bit entropi -> URL-güvenli base64. Şifre sıfırlama / e-posta doğrulama / refresh token için.
    public static class SecureTokenGenerator
    {
        public static string Generate(int byteLength = 32)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            // URL-güvenli base64 (+/= karakterleri temizlenir - token URL'de/e-postada taşınır)
            return Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        // Aciklayici yorum: Kriptografik guvenli N-haneli sayisal kod (2FA/OTP icin - Random yerine RNG).
        public static string GenerateNumericCode(int digits)
        {
            if (digits < 1) digits = 6;
            var bytes = new byte[4];
            var sb = new System.Text.StringBuilder();
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            for (int i = 0; i < digits; i++)
            {
                rng.GetBytes(bytes);
                uint val = System.BitConverter.ToUInt32(bytes, 0);
                sb.Append((val % 10).ToString());
            }
            return sb.ToString();
        }

    }
}
