using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // Best-effort: yan etki hatası ana akışı bozmaz ama SESSİZ kalmaz (H53/H54 dersi).
    public class OrderConfirmationManager : IOrderConfirmationService
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<OrderConfirmationManager> _logger;
        // DALGA-2-FIX: iptal yan etkisi basarisiz olursa OPERATORUN GOREBILECEGI bir yere de yazilir.
        private readonly IOrderStatusHistoryService _statusHistory;

        public OrderConfirmationManager(IInvoiceService invoiceService, ILogger<OrderConfirmationManager> logger,
            IOrderStatusHistoryService statusHistory)
        {
            _invoiceService = invoiceService;
            _logger = logger;
            _statusHistory = statusHistory;
        }

        public async Task ApplyConfirmedSideEffectsAsync(int orderId)
        {
            try
            {
                // InvoiceManager idempotent - aynı sipariş için ikinci fatura üretmez.
                var (_, result) = await _invoiceService.GenerateForOrder(orderId);
                if (result != null && !result.Success)
                    _logger.LogError("Fatura uretilemedi. orderId={OrderId} mesaj={Mesaj}", orderId, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Siparis onay yan etkileri basarisiz. orderId={OrderId}", orderId);
            }
        }

        // ══ DALGA-2-FIX - "IPTAL EDILMIS SIPARISIN FATURASI Sent KALAMAZ" INVARIANTI ══════════
        //
        // Uc iptal yolunun UCU DE bu metodu cagiriyor (Iyzico basarisiz dali, admin durum
        // degisikligi, son kalemin iptali) - yani fatura iptali kod duzeyinde KAPSANMIS durumda.
        // AMA metot BEST-EFFORT: `CancelForOrder` basarisiz donerse (en gercekci sebep:
        // e-fatura saglayicisi GIB iptalini reddediyor -> BadGateway) siparis Cancelled olur,
        // fatura Sent KALIR ve bunu goren tek yer bir LOG SATIRIDIR. Yeniden deneyen de yok.
        //
        // OLCULEN ARTIK (Dalga 2, dev veritabani): iptal edilmis YEDI siparisin faturasi hala
        // `status=1 (Sent)`, her biri 949,80 TL. Bu satirlar bugunku kodun URETEBILECEGI bir sey
        // DEGIL - 22-23 Temmuz tarihli, iptal-yan-etkisi baglanmadan onceki artiklar (olculdu:
        // fatura 8-15 temiz). Ama YUKARIDAKI bosluk, ayni tabloyu URETIMDE yeniden yaratabilir.
        //
        // GUARD: hata artik yalniz loglanmiyor, SIPARIS ZAMAN CIZELGESINE de "KRITIK" notu
        // dusuluyor - H53'teki "para iadesi BASARISIZ" kalibinin aynisi. Operasyon iptal edilmis
        // ama faturasi acikta kalmis siparisi GOREBILIR. Notu yazmak ANA AKISI bozamaz
        // (ayri try/catch): iptal ana akistir, not ikincildir.
        public async Task ApplyCancelledSideEffectsAsync(int orderId)
        {
            try
            {
                // InvoiceManager idempotent - fatura yoksa veya zaten iptalse başarı döner.
                var (_, result) = await _invoiceService.CancelForOrder(orderId);
                if (result != null && !result.Success)
                {
                    _logger.LogError("Fatura iptal edilemedi. orderId={OrderId} mesaj={Mesaj}", orderId, result.Message);
                    await KritikNotDusAsync(orderId,
                        $"KRİTİK: sipariş iptal edildi fakat FATURASI İPTAL EDİLEMEDİ ({result.Message}) - fatura hâlâ geçerli görünüyor, manuel müdahale gerekli");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Siparis iptal yan etkileri basarisiz. orderId={OrderId}", orderId);
                await KritikNotDusAsync(orderId,
                    "KRİTİK: sipariş iptal edildi fakat FATURA İPTALİ HATA VERDİ - fatura hâlâ geçerli görünüyor, manuel müdahale gerekli");
            }
        }

        // Not yazimi BEST-EFFORT: burada patlamak, zaten basarisiz olmus bir yan etkinin uzerine
        // ana akisi da devirmek olurdu. Log birinci kanal, zaman cizelgesi ikinci kanal.
        private async Task KritikNotDusAsync(int orderId, string not)
        {
            try
            {
                await _statusHistory.RecordAsync(orderId, (byte)OrderStatusEnum.Cancelled, not);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura iptal uyarisi zaman cizelgesine yazilamadi. orderId={OrderId}", orderId);
            }
        }
    }
}
