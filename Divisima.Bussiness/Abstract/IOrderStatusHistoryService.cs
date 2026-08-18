using System.Net;
using Divisima.Core.Utilities.Results;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Sipariş durum geçmişi. RecordAsync (durum değişince kayıt) + GetTimeline (müşteri takibi).
    public interface IOrderStatusHistoryService
    {
        // Açıklayıcı yorum: Durum değişimini kaydet (OrderManager/PaymentManager tetikler). Best-effort.
        Task RecordAsync(int orderId, byte status, string note);
        // Açıklayıcı yorum: Siparişin zaman çizelgesi (IDOR korumalı - sadece sahibi).
        Task<(HttpStatusCode, Result)> GetTimeline(int orderId, int customerId);
    }
}
