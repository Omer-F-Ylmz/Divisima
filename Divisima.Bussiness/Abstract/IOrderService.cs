using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Order;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Sipariş iş servisi. PlaceOrder (sepet->sipariş) + durum + listeleme.
    public interface IOrderService
    {
        // Açıklayıcı yorum: Sipariş oluştur (stok kontrol -> düş -> sipariş -> snapshot -> event)
        Task<(HttpStatusCode, Result)> PlaceOrder(OrderCreateRequestDto dto);

        // Açıklayıcı yorum: Sipariş durumunu değiştir (admin)
        Task<(HttpStatusCode, Result)> ChangeOrderStatus(OrderStatusChangeRequestDto dto);
        Task<(HttpStatusCode, Result)> ConfirmManualPayment(int orderId);
        Task<(HttpStatusCode, Result)> GetInvoiceHtml(int orderId, int customerId);

        Task<(HttpStatusCode, Result)> GetById(int id, int customerId);
        Task<(HttpStatusCode, Result)> GetByCustomer(int customerId);
        // Açıklayıcı yorum: Admin - tüm siparişler (filtre + sayfalama)
        Task<(HttpStatusCode, Result)> GetAllForAdmin(Divisima.Entity.Dtos.Order.AdminOrderFilterDto filter);
        Task<(HttpStatusCode, Result)> CancelItem(int orderId, int orderItemId, int customerId);
        Task<(HttpStatusCode, Result)> GetEstimatedDelivery(int orderId, int customerId);
    }
}
