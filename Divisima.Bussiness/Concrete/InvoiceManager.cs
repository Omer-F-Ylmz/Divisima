using System.Net;
using Microsoft.Extensions.Configuration;
using Divisima.Core.Utilities.Pricing;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.EInvoice;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Invoice;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Fatura yöneticisi. Sipariş toplamından KDV'yi ayrıştırır (fiyatlar KDV dahil kabul edilir),
    // fatura üretir ve e-fatura sağlayıcıya iletir. Idempotent: aynı sipariş için ikinci kez üretmez.
    public class InvoiceManager : IInvoiceService
    {
        // Açıklayıcı yorum: KDV oranı YAPILANDIRMADAN (EInvoice:KdvRate) - hardcode yerine. Vergi oranı değişebilir
        // veya kategori bazlı olabilir. Geçersiz/eksikse %20 güvenli varsayılan. Kesir olarak: 0.20 = %20, 0.10 = %10.
        // InvariantCulture: Türkçe locale ondalık ayıracı virgül olduğundan "0.20" doğru parse edilsin.
        private decimal TaxRate =>
            decimal.TryParse(_config["EInvoice:KdvRate"], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var r) && r >= 0m && r < 1m ? r : 0.20m;

        private readonly IInvoiceDal _invoiceDal;
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IProductDal _productDal;
        private readonly IEInvoiceProvider _eInvoiceProvider;
        private readonly IConfiguration _config;

        public InvoiceManager(IInvoiceDal invoiceDal, IOrderDal orderDal, IOrderItemDal orderItemDal,
            IProductDal productDal, IEInvoiceProvider eInvoiceProvider, IConfiguration config)
        {
            _config = config;
            _invoiceDal = invoiceDal;
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _productDal = productDal;
            _eInvoiceProvider = eInvoiceProvider;
        }

        public async Task<(HttpStatusCode, Result)> GenerateForOrder(int orderId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // Açıklayıcı yorum: Idempotent - bu sipariş için fatura zaten varsa tekrar üretme
            var existing = await _invoiceDal.GetAsync(i => i.order_id == orderId);
            if (existing != null)
                return (HttpStatusCode.OK, new SuccessResult(Messages.InvoiceAlreadyExists));

            // Açıklayıcı yorum: KDV ayrıştırma (total KDV dahil) -> subtotal = total / 1.20, tax = total - subtotal
            var total = order.total_price;
            var subtotal = MoneyHelper.Round(total / (1 + TaxRate));
            var taxAmount = total - subtotal;

            var invoiceNumber = $"DIV-{DateTime.Now:yyyy}-{order.id:D6}";

            var invoice = new Invoice
            {
                order_id = order.id,
                customer_id = order.customer_id,
                invoice_number = invoiceNumber,
                invoice_type = (byte)InvoiceTypeEnum.Individual,
                subtotal = subtotal,
                tax_rate = TaxRate,
                tax_amount = taxAmount,
                total = total,
                status = (byte)InvoiceStatusEnum.Draft,
                created_at = DateTime.Now
            };
            await _invoiceDal.AddAsync(invoice);

            // Açıklayıcı yorum: e-Fatura sağlayıcıya ilet (kapalıysa taslak referans döner)
            var items = await _orderItemDal.GetListAsync(i => i.order_id == order.id);
            // N+1 duzeltmesi: tum urun adlarini tek sorguda
            var invIds = items.Select(i => i.product_id).Distinct().ToList();
            var invProducts = (await _productDal.GetListAsync(p => invIds.Contains(p.id))).ToDictionary(p => p.id, p => p.name);
            var lines = new List<EInvoiceLine>();
            foreach (var item in items)
            {
                lines.Add(new EInvoiceLine
                {
                    ProductName = invProducts.TryGetValue(item.product_id, out var pn) ? pn : "Ürün",
                    Quantity = item.quantity,
                    UnitPrice = item.unit_price,
                    LineTotal = item.unit_price * item.quantity
                });
            }

            var result = await _eInvoiceProvider.SendInvoiceAsync(new EInvoiceRequest
            {
                InvoiceNumber = invoiceNumber,
                InvoiceType = invoice.invoice_type,
                Subtotal = subtotal,
                TaxAmount = taxAmount,
                Total = total,
                BuyerName = "Müşteri",
                Lines = lines
            });

            if (result.Success)
            {
                invoice.provider_invoice_id = result.ProviderInvoiceId;
                invoice.pdf_url = result.PdfUrl;
                invoice.status = (byte)InvoiceStatusEnum.Sent;
                await _invoiceDal.UpdateAsync(invoice);
            }

            return (HttpStatusCode.OK, new SuccessResult(Messages.InvoiceGenerated));
        }

        // Açıklayıcı yorum: FATURA İPTALİ - sipariş iptal edilince faturası da iptal edilmeliydi ama hiçbir kod
        // InvoiceStatusEnum.Cancelled yazmıyordu: iptal edilen siparişin faturası Sent/Approved kalıyor, muhasebe
        // raporu iptal edilmiş siparişi ciroda sayıyordu. Idempotent (yan etki tekrar çağrılabilir olmalı).
        public async Task<(HttpStatusCode, Result)> CancelForOrder(int orderId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // Açıklayıcı yorum: GÜVENLİK - yalnız gerçekten iptal edilmiş siparişin faturası iptal edilebilir.
            // (Aktif siparişin faturası yanlışlıkla/kötü niyetle iptal edilemesin.)
            if (order.status != (byte)OrderStatusEnum.Cancelled)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvoiceCancelOrderNotCancelled));

            var invoice = await _invoiceDal.GetAsync(i => i.order_id == orderId);
            // Açıklayıcı yorum: Fatura hiç üretilmemişse (ör. Pending sipariş iptali) yapacak bir şey yok - başarı dön.
            if (invoice == null)
                return (HttpStatusCode.OK, new SuccessResult(Messages.InvoiceCancelNotNeeded));

            // Açıklayıcı yorum: Zaten iptalse tekrar yazma (idempotent - HandleStatusSideEffects + CancelItem
            // yollarının ikisi de aynı siparişte tetiklenebilir).
            if (invoice.status == (byte)InvoiceStatusEnum.Cancelled)
                return (HttpStatusCode.OK, new SuccessResult(Messages.InvoiceAlreadyCancelled));

            // DİKKAT: Invoice entity'sinde updated_at/cancelled_at kolonu YOK -> yalnız status güncellenir
            // (olmayan alana atama CS1061 ile build'i patlatırdı).
            invoice.status = (byte)InvoiceStatusEnum.Cancelled;
            await _invoiceDal.UpdateAsync(invoice);

            return (HttpStatusCode.OK, new SuccessResult(Messages.InvoiceCancelled));
        }

        public async Task<(HttpStatusCode, Result)> GetMyInvoices(int customerId)
        {
            var invoices = await _invoiceDal.GetListAsync(i => i.customer_id == customerId);
            return (HttpStatusCode.OK, new SuccessDataResult<List<InvoiceResponseDto>>(invoices.Select(Map).ToList()));
        }

        public async Task<(HttpStatusCode, Result)> GetByOrder(int orderId, int customerId)
        {
            var invoice = await _invoiceDal.GetAsync(i => i.order_id == orderId);
            if (invoice == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.InvoiceNotFound));
            // Açıklayıcı yorum: SAHİPLİK - yalnız kendi faturası (IDOR engeli)
            if (invoice.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.InvoiceNotFound));   // TEK SOZLESME: sahiplik ihlali de "bulunamadi"
            return (HttpStatusCode.OK, new SuccessDataResult<InvoiceResponseDto>(Map(invoice)));
        }

        private static InvoiceResponseDto Map(Invoice i) => new()
        {
            id = i.id,
            order_id = i.order_id,
            invoice_number = i.invoice_number,
            invoice_type = i.invoice_type,
            company_name = i.company_name,
            subtotal = i.subtotal,
            tax_amount = i.tax_amount,
            total = i.total,
            status = i.status,
            pdf_url = i.pdf_url,
            created_at = i.created_at
        };
    }
}
