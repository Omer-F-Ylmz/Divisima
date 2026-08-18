using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Storage
{
    // Açıklayıcı yorum: Yerel disk depolama (dev/tek sunucu). wwwroot/uploads altına yazar, /uploads/.. URL'i döner.
    // Production'da bulut (Azure Blob / S3) implementasyonu ile değiştirilir - iş mantığı değişmez.
    public class LocalImageStorage : IImageStorage
    {
        private readonly IConfiguration _config;
        private readonly ILogger<LocalImageStorage> _logger;

        public LocalImageStorage(IConfiguration config, ILogger<LocalImageStorage> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Açıklayıcı yorum: Fiziksel kök (wwwroot/uploads) ve genel URL öneki
        private string PhysicalRoot => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
        private string UrlPrefix => (_config["Storage:PublicBaseUrl"] ?? "").TrimEnd('/') + "/uploads/products";

        public async Task<string> SaveAsync(byte[] content, string fileName, string contentType)
        {
            Directory.CreateDirectory(PhysicalRoot);
            // GÜVENLİK: uzantıyı DOĞRULANMIŞ content-type'tan türet, client dosya-adından DEĞİL.
            // Önceden Path.GetExtension(fileName) ile client uzantısı ("x.html"/"x.aspx") kaydediliyordu ->
            // statik sunumda text/html servis edilip stored-XSS olabilirdi. Artık her zaman güvenli görsel uzantısı.
            var ext = (contentType?.ToLower()) switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".img"   // manager zaten whitelist doğruluyor; güvenli son çare
            };
            var safeName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(PhysicalRoot, safeName);
            await File.WriteAllBytesAsync(fullPath, content);
            return $"{UrlPrefix}/{safeName}";
        }

        public Task DeleteAsync(string url)
        {
            try
            {
                var fileName = Path.GetFileName(new Uri(url, UriKind.RelativeOrAbsolute).ToString());
                var fullPath = Path.Combine(PhysicalRoot, fileName);
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Görsel silinemedi: {Url}", url); }
            return Task.CompletedTask;
        }
    }
}
