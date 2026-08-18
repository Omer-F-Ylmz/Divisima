namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: İade talebi durumu
    public enum ReturnStatusEnum : byte
    {
        Pending = 0,      // Beklemede
        Approved = 1,     // Onaylandı
        Rejected = 2,     // Reddedildi
        Completed = 3     // Tamamlandı (refund + stok iade yapıldı)
    }

    // Açıklayıcı yorum: İade nedeni
    public enum ReturnReasonEnum : byte
    {
        NotLiked = 0,     // Beğenmedim
        SizeIssue = 1,    // Beden uymadı
        Defective = 2,    // Kusurlu ürün
        WrongItem = 3     // Yanlış ürün geldi
    }

    // Açıklayıcı yorum: İade tipi
    public enum ReturnTypeEnum : byte
    {
        Refund = 0,       // İade (para iadesi)
        Exchange = 1      // Değişim
    }
}
