using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Divisima.Bussiness.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // Best-effort: yan etki hatası ana akışı bozmaz ama SESSİZ kalmaz (H53/H54 dersi).
    public class OrderConfirmationManager : IOrderConfirmationService
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<OrderConfirmationManager> _logger;

        public OrderConfirmationManager(IInvoiceService invoiceService, ILogger<OrderConfirmationManager> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
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
    }
}
