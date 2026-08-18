namespace Divisima.Core.Integrations.Shipping
{
    // Açıklayıcı yorum: Kargo firması soyutlaması. Takip numarasıyla durum sorgular. Her firma (Yurtiçi/Aras/MNG/PTT)
    // farklı API sunar; bu soyutlama iş mantığını firmadan bağımsız kılar. Firma seçimi carrier byte + config ile.
    public interface ICarrierProvider
    {
        Task<CarrierTrackingResult> TrackAsync(byte carrier, string trackingNumber);
    }

    // Açıklayıcı yorum: Firma-bağımsız takip sonucu (normalize edilmiş durum)
    public class CarrierTrackingResult
    {
        public bool Success { get; set; }
        public byte NormalizedStatus { get; set; }        // ShipmentStatusEnum
        public string? RawStatusText { get; set; }        // firma ham metni
        public DateTime? EstimatedDelivery { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
