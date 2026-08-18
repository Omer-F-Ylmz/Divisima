using System.Text;
using System.Security.Cryptography;

namespace Divisima.Core.Security.Hashing
{
    // Açıklayıcı yorum: Şifre hash/doğrulama (Cafixo HashingHelper birebir). HMAC-SHA512.
    public static class HashingHelper
    {
        // Açıklayıcı yorum: Şifreden hash + salt üret (kayıt anında)
        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512();
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        // Açıklayıcı yorum: Girilen şifre kayıtlı hash+salt ile eşleşiyor mu (login anında)
        public static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512(passwordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            // TIMING-SAFE + LENGTH-SAFE karşılaştırma: eski byte-byte erken-dönüş (a) timing side-channel idi,
            // (b) passwordHash.Length != computedHash.Length ise passwordHash[i] IndexOutOfRange -> login 500 crash yapardı.
            // CryptographicOperations.FixedTimeEquals sabit-zamanlı çalışır VE uzunluk farkında güvenle false döner.
            return CryptographicOperations.FixedTimeEquals(computedHash, passwordHash);
        }
    }
}
