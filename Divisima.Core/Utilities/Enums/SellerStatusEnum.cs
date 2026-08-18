namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Satıcı hesap durumu. Pending = admin onayı bekliyor (giriş yapabilir ama satış yapamaz),
    // Approved = aktif satıcı, Suspended = askıya alınmış (giriş engelli).
    public enum SellerStatusEnum : byte
    {
        Pending = 0,
        Approved = 1,
        Suspended = 2
    }
}
