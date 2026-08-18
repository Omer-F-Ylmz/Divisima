namespace Divisima.Core.Utilities.Enums
{
    // Açıklayıcı yorum: Kargo firması
    public enum CarrierEnum : byte
    {
        Yurtici = 0,
        Aras = 1,
        Mng = 2,
        Ptt = 3,
        Surat = 4
    }

    // Açıklayıcı yorum: Kargo durumu (firma durumlarından normalize edilir)
    public enum ShipmentStatusEnum : byte
    {
        Preparing = 0,    // Hazırlanıyor
        InTransit = 1,    // Yolda
        OutForDelivery = 2, // Dağıtımda
        Delivered = 3,    // Teslim edildi
        Returned = 4      // İadede
    }
}
