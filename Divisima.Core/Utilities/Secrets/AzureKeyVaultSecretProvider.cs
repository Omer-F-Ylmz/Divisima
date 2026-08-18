using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Utilities.Secrets
{
    // Açıklayıcı yorum: GERÇEK Azure Key Vault sağlayıcı. Sırlar kasada; uygulama yalnız okur, saklamaz.
    // DefaultAzureCredential: managed identity (production) / az login (dev) / env - kimlik bilgisi kodda yok.
    // Sırlar kısa süre cache'lenir (Key Vault çağrı maliyeti + rate limit). "Vault:Enabled" false ise config'e düşer.
    public class AzureKeyVaultSecretProvider : ISecretProvider
    {
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AzureKeyVaultSecretProvider> _logger;
        private readonly SecretClient? _client;

        public AzureKeyVaultSecretProvider(IConfiguration config, IMemoryCache cache, ILogger<AzureKeyVaultSecretProvider> logger)
        {
            _config = config;
            _cache = cache;
            _logger = logger;

            var vaultUri = config["Vault:Uri"];
            var enabled = bool.TryParse(config["Vault:Enabled"], out var v) && v;
            if (enabled && !string.IsNullOrWhiteSpace(vaultUri))
            {
                // Açıklayıcı yorum: Managed identity / az login üzerinden kimliklen (secret'sız)
                _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
            }
        }

        public async Task<string> GetSecretAsync(string name)
        {
            // Açıklayıcı yorum: Kasa kapalıysa config/env'den (kademeli geçiş)
            if (_client == null)
                return _config[name] ?? "";

            // Açıklayıcı yorum: 5 dk cache (kasa çağrı maliyeti). Key Vault adı ":" içermez -> "--" dönüşümü
            var cacheKey = $"vault-secret:{name}";
            if (_cache.TryGetValue(cacheKey, out string? cached) && cached != null)
                return cached;

            try
            {
                var secretName = name.Replace(":", "--");
                var secret = await _client.GetSecretAsync(secretName);
                var value = secret.Value.Value;
                _cache.Set(cacheKey, value, TimeSpan.FromMinutes(5));
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Key Vault secret okunamadı: {Name}", name);
                // Açıklayıcı yorum: Kasadan okunamazsa config fallback (kesinti direnci)
                return _config[name] ?? "";
            }
        }
    }
}
