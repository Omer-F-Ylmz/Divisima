namespace Divisima.Core.Integrations.EInvoice
{
    // Açıklayıcı yorum: e-Fatura/e-Arşiv sağlayıcı soyutlaması (GİB entegratörü: Foriba, Logo, Uyumsoft, Paraşüt).
    // Uygulama faturayı oluşturur, sağlayıcı GİB'e iletir. Sağlayıcı değişse de iş mantığı sabit kalır.
    public interface IEInvoiceProvider
    {
        Task<EInvoiceResult> SendInvoiceAsync(EInvoiceRequest request);

        // Açıklayıcı yorum: e-Fatura İPTALİ. Bu metot YOKTU: sipariş iptal edilince fatura yalnız
        // YEREL olarak Cancelled işaretleniyor, GİB tarafına hiçbir bildirim gitmiyordu. Sonuç:
        // mağazanın kayıtlarında iptal, vergi idaresinde GEÇERLİ fatura - sessiz uyumsuzluk.
        // providerInvoiceId, gönderim sırasında sağlayıcının döndürdüğü referanstır; yalnız
        // GERÇEKTEN gönderilmiş faturalar iptal edilebilir.
        Task<EInvoiceResult> CancelInvoiceAsync(string providerInvoiceId, string reason);
    }

    // Açıklayıcı yorum: e-Fatura gönderim isteği (sağlayıcıdan bağımsız model)
    public class EInvoiceRequest
    {
        public string InvoiceNumber { get; set; }
        public byte InvoiceType { get; set; }              // bireysel/kurumsal
        public string? TaxNumber { get; set; }
        public string? CompanyName { get; set; }
        public string BuyerName { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public List<EInvoiceLine> Lines { get; set; } = new();
    }

    public class EInvoiceLine
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class EInvoiceResult
    {
        public bool Success { get; set; }
        public string? ProviderInvoiceId { get; set; }
        public string? PdfUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
