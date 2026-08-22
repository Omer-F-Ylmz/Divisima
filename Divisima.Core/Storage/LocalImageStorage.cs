using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _env;

        public LocalImageStorage(IConfiguration config, ILogger<LocalImageStorage> logger, IWebHostEnvironment env)
        {
            _config = config;
            _logger = logger;
            _env = env;
        }

        // SPRINT 8 MADDE 4 - YAZMA VE SUNUM AYNI DIZINE BAKAR.
        //
        // ONCEKI HALI: `Directory.GetCurrentDirectory()/wwwroot/uploads/products`.
        // `UseStaticFiles` ise dosyalari `IWebHostEnvironment.WebRootPath`ten (varsayilan olarak
        // ContentRoot/wwwroot) SUNUYOR. Bu ikisi yalnizca CALISMA DIZINI content root ile AYNI
        // oldugunda ortusur. `dotnet run --project` ve normal yayinlarda ortusuyor; ama calisma
        // dizini farkli baslatilan bir serviste (systemd'de WorkingDirectory verilmemis, Windows
        // Service) yukleme HIC SUNULMAYAN bir dizine yazilir: uc "basarili" doner, gorsel
        // SONSUZA KADAR 404 verir.
        //
        // BU TEORIK DEGIL - E2b'DE CANLI ORTAMDA GERCEKLESTI (olculdu): `product_images`
        // tablosunda 3 satir vardi (is_primary=1 dahil), ama `Divisima.API/wwwroot/uploads/products`
        // BOSTU ve dosyalar yalnizca `Divisima.IntegrationTests/bin/Release/net8.0/wwwroot/...`
        // altinda bulundu. Veritabani "gorsel var" diyordu, vitrin 404 aliyordu.
        //
        // WebRootPath dogru kaynaktir: sunum hangi dizinden yapiliyorsa yazma da oraya yapilir.
        // WebRootPath'in BOS olabilecegi tek durum wwwroot dizininin hic olmamasidir; o zaman
        // ContentRoot/wwwroot'a duseriz - yine sunumun bakacagi yer.
        private string PhysicalRoot => Path.Combine(
            string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath,
            "uploads", "products");
        private string UrlPrefix => (_config["Storage:PublicBaseUrl"] ?? "").TrimEnd('/') + "/uploads/products";

        public async Task<string> SaveAsync(byte[] content, string fileName, string contentType)
        {
            Directory.CreateDirectory(PhysicalRoot);
            // GÜVENLİK: uzantıyı DOĞRULANMIŞ content-type'tan türet, client dosya-adından DEĞİL.
            // Önceden Path.GetExtension(fileName) ile client uzantısı ("x.html"/"x.aspx") kaydediliyordu ->
            // statik sunumda text/html servis edilip stored-XSS olabilirdi. Artık her zaman güvenli görsel uzantısı.
            var ext = (contentType?.ToLowerInvariant()) switch   // MIME: makine dizgesi
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
