using System.Security.Cryptography;
using System.Text;

namespace Divisima.Core.Security.TwoFactor
{
    // Açıklayıcı yorum: RFC 6238 TOTP implementasyonu (Google Authenticator uyumlu, 30 sn pencere, 6 hane).
    // Gizli anahtar Base32; login'de two_factor_enabled ise 6 haneli kod doğrulanır (±1 pencere toleransı).
    public class TotpService : ITwoFactorService
    {
        private const int Digits = 6;
        private const int PeriodSeconds = 30;

        public string GenerateSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(20);
            return Base32Encode(bytes);
        }

        public string GenerateQrCodeUri(string email, string secret)
        {
            // Açıklayıcı yorum: otpauth:// URI - authenticator uygulaması QR'dan okur
            return $"otpauth://totp/Divisima:{Uri.EscapeDataString(email)}?secret={secret}&issuer=Divisima&digits={Digits}&period={PeriodSeconds}";
        }

        public bool ValidateCode(string secret, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds;
            // Açıklayıcı yorum: ±1 zaman penceresi toleransı (saat kayması / gecikme)
            for (long i = -1; i <= 1; i++)
            {
                if (ComputeTotp(secret, counter + i) == code.Trim())
                    return true;
            }
            return false;
        }

        private static string ComputeTotp(string base32Secret, long counter)
        {
            var key = Base32Decode(base32Secret);
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);
            var offset = hash[^1] & 0x0F;
            var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
            var otp = binary % (int)Math.Pow(10, Digits);
            return otp.ToString().PadLeft(Digits, '0');
        }

        // Açıklayıcı yorum: Base32 kodlama/çözme (TOTP standardı)
        private static readonly string B32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private static string Base32Encode(byte[] data)
        {
            var sb = new StringBuilder();
            int bits = 0, value = 0;
            foreach (var b in data)
            {
                value = (value << 8) | b; bits += 8;
                while (bits >= 5) { sb.Append(B32[(value >> (bits - 5)) & 31]); bits -= 5; }
            }
            if (bits > 0) sb.Append(B32[(value << (5 - bits)) & 31]);
            return sb.ToString();
        }
        private static byte[] Base32Decode(string input)
        {
            input = input.TrimEnd('=').ToUpperInvariant();
            int bits = 0, value = 0; var output = new List<byte>();
            foreach (var c in input)
            {
                value = (value << 5) | B32.IndexOf(c); bits += 5;
                if (bits >= 8) { output.Add((byte)((value >> (bits - 8)) & 0xFF)); bits -= 8; }
            }
            return output.ToArray();
        }
    }
}
