using Microsoft.Extensions.Configuration;

namespace Divisima.Core.Utilities.Secrets
{
    // Açıklayıcı yorum: IConfiguration (env değişkenleri + user-secrets) tabanlı sır sağlayıcı.
    // BUGÜN ÜRETİMDE DE KULLANILAN TEK SAĞLAYICI BUDUR (LF-1/K5'te ölçüldü): `Program.cs`
    // KOŞULSUZ bunu kaydeder, `AzureKeyVaultSecretProvider` HİÇBİR YERDE kayıtlı değildir ve
    // `ISecretProvider`ın tüketicisi 0'dır. Bu satır eskiden "Production'da
    // AzureKeyVaultSecretProvider ile değiştirilir - arayüz aynı, kod dokunulmaz" diyordu;
    // YANLIŞTI - kasaya yazılan bir değer, okuyucu yazılana kadar uygulamaya ULAŞMAZ.
    // (Aynı yanlış SECURITY.md'de ve secret-rotation.yml'de de vardı; ikisi de düzeltildi.)
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
