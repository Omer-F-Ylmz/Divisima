namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Sipariş durumu. Order.status (byte) bu değerlerle yorumlanır.
    public enum OrderStatusEnum
    {
        Pending = 0,      // Onay bekliyor
        Confirmed = 1,    // Onaylandı
        Preparing = 2,    // Hazırlanıyor
        Shipped = 3,      // Kargoda
        Delivered = 4,    // Teslim edildi
        Cancelled = 5     // İptal
    }
}
