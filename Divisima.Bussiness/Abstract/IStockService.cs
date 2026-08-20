using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Stok iş servisi (Cafixo IStockService kalıbı). Beden bazlı stok yönetimi.
    // OrderManager sipariş verilince bunu çağırır (stok düş + hareket kaydı).
    public interface IStockService
    {
        // Açıklayıcı yorum: Belirli ürün+bedende yeterli stok var mı (frontend sizeStockOf kontrolü)
        Task<bool> CheckStock(int productId, string size, int quantity);

        // Açıklayıcı yorum: Stok düş + Out hareketi kaydet (sipariş anında)
        Task<(HttpStatusCode, Result)> DecreaseStock(int productId, string size, int quantity, int? referenceId);
        // Açıklayıcı yorum: Rezervasyon akışı (oversell + terk edilen sepet koruması)
        Task<(HttpStatusCode, Result)> ReserveStock(int productId, string size, int quantity, int orderId);
        Task<(HttpStatusCode, Result)> ConfirmReservation(int orderId);
        Task<(HttpStatusCode, Result)> ReleaseReservation(int orderId);
        Task<int> ReleaseExpiredReservations();
        // Açıklayıcı yorum: Admin stok düzeltme (mutlak yeni değer + denetim notu)
        Task<(HttpStatusCode, Result)> AdjustStock(int productId, string size, int newQuantity, string note);

        // Aciklayici yorum: ADMIN stok detayi - beden basina fiziksel/rezerve/satilabilir.
        // Operator duzeltme yapmadan once mevcut durumu gormeli; rezerve gorunmeden "10 stok var
        // ama 3 satamiyorum" farki ekranda anlasilmaz.
        Task<(HttpStatusCode, Result)> GetStockDetail(int productId);

        // Açıklayıcı yorum: Stok artır + In hareketi kaydet (iade/iptal anında)
        Task<(HttpStatusCode, Result)> IncreaseStock(int productId, string size, int quantity, int? referenceId);
    }
}
