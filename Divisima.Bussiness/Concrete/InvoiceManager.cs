using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.EInvoice;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Invoice;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Configuration;

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
        private readonly IInvoiceItemDal _invoiceItemDal;
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IProductDal _productDal;
        private readonly ICategoryDal _categoryDal;
        private readonly IEInvoiceProvider _eInvoiceProvider;
        private readonly IConfiguration _config;

        public InvoiceManager(IInvoiceDal invoiceDal, IOrderDal orderDal, IOrderItemDal orderItemDal,
            IProductDal productDal, IEInvoiceProvider eInvoiceProvider, IConfiguration config,
            IInvoiceItemDal invoiceItemDal, ICategoryDal categoryDal)
        {
            _config = config;
            _invoiceDal = invoiceDal;
            _invoiceItemDal = invoiceItemDal;
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _productDal = productDal;
            _categoryDal = categoryDal;
            _eInvoiceProvider = eInvoiceProvider;
        }

        public async Task<(HttpStatusCode, Result)> GenerateForOrder(int orderId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // SPRINT 8 MADDE 2 - SIPARIS DURUMU GUARD'I.
            // Bu uc, var olan HERHANGI bir siparis id'si icin fatura kesiyordu; tek koruma
            // "cagiran dogru yerden cagirsin" varsayimiydi. Sprint 7'de odeme akisindaki cagri
            // onay dalina tasindi ve o yol duzeldi, ama UCUN KENDISI korumasiz kaldi - baska
            // bir cagri yolu (admin ekrani, toplu is, ileride outbox yeniden denemesi) IPTAL
            // EDILMIS ya da HENUZ ODENMEMIS bir siparise fatura kesebilirdi.
            // Fatura mali bir beyandir: iptal edilmis siparise kesilen fatura ciroyu sisirir ve
            // musteriye odenmemis bir borc gonderir.
            //
            // KURAL: yalnizca ONAYLANMIS VE SONRASI durumlar faturalanabilir.
            //   Pending(0)   -> HAYIR (para henuz alinmadi)
            //   Cancelled(5) -> HAYIR (siparis yok hukmunde)
            //   Confirmed(1) / Preparing(2) / Shipped(3) / Delivered(4) -> EVET
            if (order.status == (byte)OrderStatusEnum.Pending || order.status == (byte)OrderStatusEnum.Cancelled)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvoiceOrderNotBillable));

            // Açıklayıcı yorum: Idempotent - bu sipariş için fatura zaten varsa tekrar üretme
            var existing = await _invoiceDal.GetAsync(i => i.order_id == orderId);
            if (existing != null)
                return (HttpStatusCode.OK, new SuccessResult(Messages.InvoiceAlreadyExists));

            var total = order.total_price;
            var invoiceNumber = $"DIV-{DateTime.Now:yyyy}-{order.id:D6}";

            // KALEM BAZLI KDV.
            // Onceden KDV BASLIK duzeyinde tek oranla ayristiriliyordu (subtotal = total / 1.20).
            // Karisik sepette (giyim %10 + aksesuar %20) bu matematiksel olarak YANLIS bir
            // beyandi. Artik her kalem KENDI efektif orani ile hesaplanir:
            //     efektif oran = Product.vat_rate ?? Category.vat_rate ?? EInvoice:KdvRate
            // ve bu oran faturaya KOPYALANIR (snapshot) - kategori orani sonradan degisse bile
            // kesilmis fatura DEGISMEZ.
            var items = await _orderItemDal.GetListAsync(i => i.order_id == order.id);
            var productIds = items.Select(i => i.product_id).Distinct().ToList();
            var products = (await _productDal.GetListAsync(p => productIds.Contains(p.id)))
                .ToDictionary(p => p.id, p => p);
            var categoryIds = products.Values.Select(p => p.category_id).Distinct().ToList();
            var categoryRates = (await _categoryDal.GetListAsync(c => categoryIds.Contains(c.id)))
                .ToDictionary(c => c.id, c => c.vat_rate);

            // Kalem brut tutarlari siparis indirimiyle ORANTILI dusulur - aksi halde kalemler
            // toplami order.total_price'i asardi (ReturnManager'daki refund_amount ile ayni kural).
            decimal indirimOrani = order.subtotal > 0m
                ? (order.subtotal - order.discount_amount) / order.subtotal
                : 1m;

            var invoiceItems = new List<InvoiceItem>();
            var lines = new List<EInvoiceLine>();
            decimal toplananBrut = 0m;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                products.TryGetValue(item.product_id, out var product);
                var productName = product?.name ?? "Ürün";

                decimal? categoryRate = null;
                if (product != null) categoryRates.TryGetValue(product.category_id, out categoryRate);
                var effectiveRate = product?.vat_rate ?? categoryRate ?? TaxRate;

                var brut = MoneyHelper.Round(item.unit_price * item.quantity * indirimOrani);

                // KURUS KACAGI ENGELI: yuvarlama artiklari SON kaleme yazilir; boylece
                // kalem toplamlari order.total_price'a BIREBIR esitlenir.
                if (i == items.Count - 1) brut = total - toplananBrut;
                toplananBrut += brut;

                var lineSubtotal = MoneyHelper.Round(brut / (1 + effectiveRate));
                var vatAmount = brut - lineSubtotal;

                invoiceItems.Add(new InvoiceItem
                {
                    product_id = item.product_id,
                    product_name = productName,
                    quantity = item.quantity,
                    unit_price = item.unit_price,
                    line_subtotal = lineSubtotal,
                    vat_rate = effectiveRate,
                    vat_amount = vatAmount,
                    line_total = brut,
                    created_at = DateTime.Now
                });

                lines.Add(new EInvoiceLine
                {
                    ProductName = productName,
                    Quantity = item.quantity,
                    UnitPrice = item.unit_price,
                    LineTotal = brut,
                    VatRate = effectiveRate,
                    VatAmount = vatAmount
                });
            }

            var subtotal = invoiceItems.Sum(x => x.line_subtotal);
            var taxAmount = invoiceItems.Sum(x => x.vat_amount);

            // Baslik tax_rate'in ANLAMI DEGISTI: artik kalemlerin AGIRLIKLI ORTALAMASI.
            // Tek oranli sepette eski davranisla ayni degeri verir (regresyon uyumu).
            var weightedRate = subtotal > 0m ? MoneyHelper.RoundRate(taxAmount / subtotal) : TaxRate;

            var invoice = new Invoice
            {
                order_id = order.id,
                customer_id = order.customer_id,
                invoice_number = invoiceNumber,
                invoice_type = (byte)InvoiceTypeEnum.Individual,
                subtotal = subtotal,
                tax_rate = weightedRate,
                tax_amount = taxAmount,
                total = subtotal + taxAmount,   // = kalem toplamlari (kurus kacagi yok)
                status = (byte)InvoiceStatusEnum.Draft,
                created_at = DateTime.Now
            };
            await _invoiceDal.AddAsync(invoice);

            foreach (var ii in invoiceItems)
            {
                ii.invoice_id = invoice.id;
                await _invoiceItemDal.AddAsync(ii);
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

            // SAGLAYICI IPTALI - yerel iptalden ONCE.
            // Fatura GERCEKTEN gonderilmisse (provider_invoice_id dolu) once GIB tarafinda iptal
            // edilmeli. Saglayici basarisiz donerse fatura Cancelled ISARETLENMEZ: aksi halde
            // magazanin kaydinda "iptal", vergi idaresinde GECERLI fatura kalir ve bu uyumsuzluk
            // sessizce buyur. Cagiran hatayi gorur ve yeniden deneyebilir (metot idempotent).
            if (!string.IsNullOrWhiteSpace(invoice.provider_invoice_id))
            {
                var cancelResult = await _eInvoiceProvider.CancelInvoiceAsync(
                    invoice.provider_invoice_id, "Siparis iptal edildi");
                if (!cancelResult.Success)
                    return (HttpStatusCode.BadGateway,
                        new ErrorResult(Messages.InvoiceProviderCancelFailed + " " + (cancelResult.ErrorMessage ?? "")));
            }

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
