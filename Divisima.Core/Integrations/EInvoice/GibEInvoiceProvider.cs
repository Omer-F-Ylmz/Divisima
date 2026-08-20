using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Integrations.EInvoice
{
    // Açıklayıcı yorum: e-Fatura sağlayıcı implementasyonu iskeleti. "EInvoice:Enabled" false ise no-op (dev).
    // Gerçek entegratör API'si (SOAP/REST) buraya bağlanır. Sağlayıcı seçimi config ile.
    public class GibEInvoiceProvider : IEInvoiceProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<GibEInvoiceProvider> _logger;

        public GibEInvoiceProvider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<GibEInvoiceProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        private bool Enabled => bool.TryParse(_config["EInvoice:Enabled"], out var v) && v;

        public async Task<EInvoiceResult> SendInvoiceAsync(EInvoiceRequest request)
        {
            // Açıklayıcı yorum: e-Fatura kapalıysa (dev) taslak referans döndür
            if (!Enabled)
                return new EInvoiceResult { Success = true, ProviderInvoiceId = $"DRAFT-{request.InvoiceNumber}" };

            // YALAN SOYLEYEN STUB FIX (H53): Enabled=true (uretim niyeti) oldugunda bu metot HICBIR SEY
            // GONDERMEDEN Success=true donuyordu. InvoiceManager de "if (result.Success)" ile faturayi
            // InvoiceStatusEnum.Sent (GIB'e gonderildi) olarak isaretliyordu -> magaza, HIC YAPILMAMIS
            // vergi bildirimlerini yapilmis saniyordu (yasal/muhasebe riski, piyasaya cikista kritik).
            // Artik: entegrasyon yapilandirilmamissa (ApiUrl yok) YUKSEK SESLE basarisiz doner; fatura
            // "Sent" isaretlenmez, hata loglanir. Gercek entegratör baglaninca burasi doldurulur.
            var apiUrl = _config["EInvoice:ApiUrl"];
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("e-Fatura ETKIN ama entegrasyon YAPILANDIRILMAMIS (EInvoice:ApiUrl bos). " +
                                 "Fatura {Number} GONDERILMEDI ve 'Sent' isaretlenmeyecek.", request.InvoiceNumber);
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = "e-Fatura entegrasyonu yapılandırılmamış (EInvoice:ApiUrl). Fatura gönderilmedi."
                };
            }
            try
            {
                // Açıklayıcı yorum: Gerçek entegratör çağrısı (Foriba/Logo/Uyumsoft REST) buraya.
                // ÖNEMLİ: gerçek çağrı eklenene kadar burası da başarısız döner - sahte başarı ÜRETİLMEZ.
                var client = _httpClientFactory.CreateClient("einvoice");
                // var res = await client.PostAsJsonAsync(apiUrl, request);
                await Task.CompletedTask;
                _logger.LogError("e-Fatura gonderim kodu HENUZ YAZILMADI: {Number} - fatura gonderilmedi.", request.InvoiceNumber);
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = "e-Fatura gönderim entegrasyonu henüz uygulanmadı; fatura gönderilmedi."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "e-Fatura gönderilemedi: {Number}", request.InvoiceNumber);
                return new EInvoiceResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        // Açıklayıcı yorum: e-Fatura iptali. SendInvoiceAsync ile AYNI H53 deseni izlenir -
        // "yalan söyleyen stub" üretilmez.
        public async Task<EInvoiceResult> CancelInvoiceAsync(string providerInvoiceId, string reason)
        {
            // e-Fatura kapalıysa (dev): gönderim de yapılmamıştı, iptal edilecek bir şey yok.
            if (!Enabled)
                return new EInvoiceResult { Success = true, ProviderInvoiceId = providerInvoiceId };

            if (string.IsNullOrWhiteSpace(providerInvoiceId))
            {
                // Sağlayıcıya hiç gitmemiş bir fatura - iptal edilecek bir dış kayıt yok.
                // Bu bir HATA değil; çağıran zaten yerel iptali yapacak.
                return new EInvoiceResult { Success = true };
            }

            var apiUrl = _config["EInvoice:ApiUrl"];
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("e-Fatura ETKIN ama entegrasyon YAPILANDIRILMAMIS (EInvoice:ApiUrl bos). " +
                                 "Fatura {Id} GIB tarafinda IPTAL EDILMEDI.", providerInvoiceId);
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = "e-Fatura entegrasyonu yapılandırılmamış (EInvoice:ApiUrl). İptal gönderilmedi."
                };
            }

            try
            {
                // Açıklayıcı yorum: Gerçek entegratör iptal çağrısı buraya (Foriba/Logo/Uyumsoft REST).
                // ÖNEMLİ: gerçek çağrı eklenene kadar burası da BAŞARISIZ döner - sahte başarı ÜRETİLMEZ.
                // Sahte başarı, GİB'de geçerli duran faturayı yerelde "iptal" göstermek demekti.
                var client = _httpClientFactory.CreateClient("einvoice");
                // var res = await client.PostAsJsonAsync(apiUrl + "/cancel", new { providerInvoiceId, reason });
                await Task.CompletedTask;
                _logger.LogError("e-Fatura IPTAL kodu HENUZ YAZILMADI: {Id} - iptal gonderilmedi.", providerInvoiceId);
                return new EInvoiceResult
                {
                    Success = false,
                    ErrorMessage = "e-Fatura iptal entegrasyonu henüz uygulanmadı; iptal gönderilmedi."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "e-Fatura iptal edilemedi: {Id}", providerInvoiceId);
                return new EInvoiceResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
