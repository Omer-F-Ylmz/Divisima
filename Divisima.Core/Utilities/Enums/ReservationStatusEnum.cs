namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Stok rezervasyon durumu
    public enum ReservationStatusEnum : byte
    {
        Active = 0,      // Aktif (ödeme bekliyor)
        Confirmed = 1,   // Onaylandı (ödeme başarılı, stok düştü)
        Released = 2,    // Serbest bırakıldı (ödeme başarısız/iptal)
        Expired = 3      // Süresi doldu (terk edildi, otomatik serbest)
    }
}
