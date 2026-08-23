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
            // DALGA B / B4 - KAPALI DAL DA SAHTE DURUM URETIYORDU (H53'un GORMEDIGI YARISI).
            //
            // ONCEKI HAL: Success=true, NormalizedStatus=1 (InTransit), RawStatusText="Takip devre disi (dev)".
            // Cagiran (ShipmentManager.TrackByOrder) Success=true gorunce kaydi GUNCELLIYOR - yani bu
            // deger VERITABANINA YAZILIYORDU. CANLI OLCULDU (Dalga B):
            //     admin kargoyu olusturdu        -> shipments.status = 0 (Preparing)
            //     musteri BIR KEZ track cagirdi
            //     DB'deki satir                  -> status = 1 (InTransit)
            //                                       last_status_text = "Takip devre disi (dev)"
            // Paketi kimse tasimadi; durum uyduruldu ve bir GELISTIRICI DIZGESI hem musteriye hem
            // admin paneline servis edilir hale geldi.
            //
            // H53 ayni kusuru Enabled=TRUE dali icin duzeltmisti ("hicbir sorgu yapmadan Yolda donuluyordu");
            // FALSE dali atlanmisti - ustelik LAUNCH YAPILANDIRMASI o. Kargo firmasi entegrasyonu
            // yok ve olmayacak (is karari), yani uretimde surekli kosacak dal BU.
            //
            // Success=false donuluyor: cagiran kaydi GUNCELLEMEZ, saklanan gercek durum (adminin
            // girdigi firma + takip no + Preparing) oldugu gibi kalir. Musteriye gosterilecek dogru
            // bilgi zaten budur - "elle girilmis takip numarasi", uydurulmus bir tasima durumu degil.
            if (!Enabled || string.IsNullOrEmpty(trackingNumber))
                return new CarrierTrackingResult
                {
                    Success = false,
                    ErrorMessage = "Kargo takip entegrasyonu kapalı (Shipping:Enabled=false) - saklanan durum korunur."
                };

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
