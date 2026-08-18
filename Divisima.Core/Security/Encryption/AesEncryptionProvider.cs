using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Divisima.Core.Security.Encryption
{
    // Açıklayıcı yorum: AES-256-GCM alan şifreleme. Anahtar appsettings/secrets "Encryption:Key" (32 byte base64).
    // GCM: hem gizlilik hem bütünlük (tamper tespiti). Her şifreleme benzersiz nonce üretir.
    public class AesEncryptionProvider : IEncryptionProvider
    {
        private readonly byte[] _key;
        private const int NonceSize = 12;   // GCM standart
        private const int TagSize = 16;

        public AesEncryptionProvider(IConfiguration config)
        {
            var b64 = config["Encryption:Key"] ?? "";
            // Açıklayıcı yorum: Anahtar 32 byte (AES-256) olmalı; yoksa güvenli türet (dev). Production'da zorunlu.
            _key = b64.Length > 0 ? Convert.FromBase64String(b64) : SHA256.HashData(Encoding.UTF8.GetBytes("DIVISIMA_DEV_ENCRYPTION_KEY"));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            var plain = Encoding.UTF8.GetBytes(plainText);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plain, cipher, tag);
            // Açıklayıcı yorum: nonce + tag + cipher birleşik base64 (çözerken ayrıştırılır)
            var result = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);
            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            try
            {
                var data = Convert.FromBase64String(cipherText);
                var nonce = data[..NonceSize];
                var tag = data[NonceSize..(NonceSize + TagSize)];
                var cipher = data[(NonceSize + TagSize)..];
                var plain = new byte[cipher.Length];
                using var aes = new AesGcm(_key, TagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                // Açıklayıcı yorum: Çözülemezse (eski düz veri / bozuk) olduğu gibi dön - kademeli geçiş
                return cipherText;
            }
        }
    }
}
