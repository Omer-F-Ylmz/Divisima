namespace Divisima.Core.Storage
{
    // Açıklayıcı yorum: Görsel depolama soyutlaması. Yerel disk (dev) veya bulut (Azure Blob/S3 - prod).
    // Sağlayıcı config ile değişir; iş mantığı depolamadan bağımsız.
    public interface IImageStorage
    {
        // Açıklayıcı yorum: Baytları kaydeder, erişilebilir URL döner
        Task<string> SaveAsync(byte[] content, string fileName, string contentType);
        Task DeleteAsync(string url);
    }
}
