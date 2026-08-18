using Divisima.Core.Utilities.Enums;

namespace Divisima.Core.Utilities.Orders
{
    // Açıklayıcı yorum: Sipariş DURUM MAKİNESİ (saf, test edilebilir). Hangi durumdan hangisine geçilebilir.
    // Pending->Confirmed/Cancelled, Confirmed->Preparing/Cancelled, Preparing->Shipped/Cancelled,
    // Shipped->Delivered, Delivered/Cancelled terminal. Aynı duruma geçiş (idempotent) serbest.
    public static class OrderStatusMachine
    {
        public static bool IsValidTransition(OrderStatusEnum from, OrderStatusEnum to)
        {
            if (from == to) return true; // no-op (idempotent güncelleme)
            return from switch
            {
                OrderStatusEnum.Pending   => to == OrderStatusEnum.Confirmed || to == OrderStatusEnum.Cancelled,
                OrderStatusEnum.Confirmed => to == OrderStatusEnum.Preparing || to == OrderStatusEnum.Cancelled,
                OrderStatusEnum.Preparing => to == OrderStatusEnum.Shipped   || to == OrderStatusEnum.Cancelled,
                OrderStatusEnum.Shipped   => to == OrderStatusEnum.Delivered,
                OrderStatusEnum.Delivered => false, // terminal
                OrderStatusEnum.Cancelled => false, // terminal
                _ => false
            };
        }
    }
}
