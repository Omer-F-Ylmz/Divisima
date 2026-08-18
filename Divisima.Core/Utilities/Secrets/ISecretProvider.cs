namespace Divisima.Core.Utilities.Secrets
{
    // Açıklayıcı yorum: Sır (secret) kaynağı soyutlaması. Development'ta appsettings/env; production'da
    // Azure Key Vault / AWS Secrets Manager / HashiCorp Vault. Uygulama kodu değişmeden kaynak değişir.
    public interface ISecretProvider
    {
        Task<string> GetSecretAsync(string name);
    }
}
