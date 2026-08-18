namespace Divisima.Core.Security.Encryption
{
    // Açıklayıcı yorum: Alan-seviyesi şifreleme soyutlaması. Hassas DB alanları (2FA secret, telefon) şifreli tutulur.
    // DB sızsa bile bu alanlar anahtarsız okunamaz.
    public interface IEncryptionProvider
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}
