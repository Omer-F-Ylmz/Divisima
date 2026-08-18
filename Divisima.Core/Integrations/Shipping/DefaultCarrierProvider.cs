using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Integrations.Shipping
{
    // Açıklayıcı yorum: Kargo takip sağlayıcı iskeleti. "Shipping:Enabled" false ise no-op (dev - durum manuel).
    // Gerçekte her carrier için ayrı HTTP entegrasyonu (firma API'si) buraya bağlanır. HttpClient DI'dan.
    public class DefaultCarrierProvider : ICarrierProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<DefaultCarrierProvider> _logger;

        public DefaultCarrierProvider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<DefaultCarrierProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        private bool Enabled => bool.TryParse(_config["Shipping:Enabled"], out var v) && v;

        public async Task<CarrierTrackingResult> TrackAsync(byte carrier, string trackingNumber)
        {
            // Açıklayıcı yorum: Kapalıysa (dev) sorgu yapmadan "yolda" varsay
            if (!Enabled || string.IsNullOrEmpty(trackingNumber))
                return new CarrierTrackingResult { Success = true, NormalizedStatus = 1, RawStatusText = "Takip devre dışı (dev)" };

            try
            {
                // Açıklayıcı yorum: Gerçek: carrier'a göre ilgili firma API'sine sorgu
                // var client = _httpClientFactory.CreateClient("shipping");
                // switch(carrier) { case (byte)CarrierEnum.Yurtici: ... }
                // SAHTE DURUM FIX (H53): Enabled=true iken hicbir sorgu yapmadan "Yolda" donuluyordu ->
                // gercek olmayan takip durumu kaydediliyor, musteriye yanlis bilgi gosteriliyordu.
                // Entegrasyon yazilana kadar BASARISIZ doner; cagiran (ShipmentManager) tracking.Success
                // false ise kaydi GUNCELLEMEZ - yani hicbir sahte veri yazilmaz (guvenli varsayilan).
                await Task.CompletedTask;
                _logger.LogError("Kargo takip entegrasyonu HENUZ YAZILMADI: carrier={Carrier} tracking={Tracking}", carrier, trackingNumber);
                return new CarrierTrackingResult { Success = false, ErrorMessage = "Kargo takip entegrasyonu henüz uygulanmadı." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kargo takip sorgusu başarısız: {Tracking}", trackingNumber);
                return new CarrierTrackingResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
