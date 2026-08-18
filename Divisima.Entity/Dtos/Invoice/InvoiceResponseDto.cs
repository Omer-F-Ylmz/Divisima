using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Invoice
{
    // Açıklayıcı yorum: Fatura görüntüleme.
    public class InvoiceResponseDto : IDto
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public string invoice_number { get; set; }
        public byte invoice_type { get; set; }
        public string? company_name { get; set; }
        public decimal subtotal { get; set; }
        public decimal tax_amount { get; set; }
        public decimal total { get; set; }
        public byte status { get; set; }
        public string? pdf_url { get; set; }
        public DateTime created_at { get; set; }
    }
}
