using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Shipping;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Kargo/sevkiyat servisi. Admin kargo oluşturur; müşteri kendi kargosunu takip eder.
    public interface IShipmentService
    {
        Task<(HttpStatusCode, Result)> CreateShipment(ShipmentCreateDto dto);
        // Açıklayıcı yorum: Takip - firma API'sinden güncel durumu çeker, kaydı günceller, döner
        Task<(HttpStatusCode, Result)> TrackByOrder(int orderId, int customerId);
        Task<(HttpStatusCode, Result)> GetByOrderForAdmin(int orderId);
    }
}
