using Microsoft.Extensions.Configuration;

namespace Divisima.Core.Utilities.Secrets
{
    // Açıklayıcı yorum: IConfiguration (env değişkenleri + user-secrets) tabanlı sır sağlayıcı.
    // Production'da AzureKeyVaultSecretProvider ile değiştirilir - arayüz aynı, kod dokunulmaz.
    // Sır önceliği: environment > user-secrets > appsettings (en güvensiz sonda).
    public class ConfigurationSecretProvider : ISecretProvider
    {
        private readonly IConfiguration _config;
        public ConfigurationSecretProvider(IConfiguration config) => _config = config;

        public Task<string> GetSecretAsync(string name)
        {
            // Açıklayıcı yorum: "Iyzico:SecretKey" gibi anahtar; env'de DIVISIMA_Iyzico__SecretKey olarak da okunur
            return Task.FromResult(_config[name] ?? "");
        }
    }
}
