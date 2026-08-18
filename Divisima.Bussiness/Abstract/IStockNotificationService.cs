using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.StockNotification;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: "Stok gelince haber ver" servisi.
    public interface IStockNotificationService
    {
        // Açıklayıcı yorum: Müşteri stoksuz ürün+beden için e-posta bırakır (abonelik)
        Task<(HttpStatusCode, Result)> Subscribe(StockNotificationSubscribeRequestDto dto);
        // Açıklayıcı yorum: Stok geldiğinde bekleyen abonelere bildirim gönderir (StockManager tetikler)
        Task NotifyBackInStock(int productId, string size);
    }
}
