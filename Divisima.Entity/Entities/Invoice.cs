using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Fatura. Sipariş onaylandığında/ödendiğinde oluşturulur. KDV dahil tutar.
    // Düz yapı (Cafixo) - navigation yok. e-fatura sağlayıcıya gönderilince provider_invoice_id dolar.
    public class Invoice : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public int customer_id { get; set; }
        public string invoice_number { get; set; }        // DIV-2025-000001 (benzersiz)
        public byte invoice_type { get; set; }            // Bireysel (0), Kurumsal (1)
        public string? tax_number { get; set; }           // kurumsal: VKN; bireysel: TCKN (şifreli saklanabilir)
        public string? company_name { get; set; }         // kurumsal fatura ünvanı
        public decimal subtotal { get; set; }             // KDV hariç
        public decimal tax_rate { get; set; }             // KDV oranı (0.20 = %20)
        public decimal tax_amount { get; set; }           // KDV tutarı
        public decimal total { get; set; }                // KDV dahil toplam
        public byte status { get; set; }                  // Taslak (0), Gönderildi (1), Onaylandı (2), İptal (3)
        public string? provider_invoice_id { get; set; }  // e-fatura sağlayıcı referansı
        public string? pdf_url { get; set; }              // fatura PDF bağlantısı
        public DateTime created_at { get; set; }
    }
}
