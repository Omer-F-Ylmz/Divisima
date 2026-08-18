namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Fatura tipi
    public enum InvoiceTypeEnum : byte
    {
        Individual = 0,   // Bireysel (TCKN)
        Corporate = 1     // Kurumsal (VKN)
    }

    // Açıklayıcı yorum: Fatura durumu
    public enum InvoiceStatusEnum : byte
    {
        Draft = 0,        // Taslak
        Sent = 1,         // e-Fatura sağlayıcıya gönderildi
        Approved = 2,     // GİB onayladı
        Cancelled = 3     // İptal
    }
}
