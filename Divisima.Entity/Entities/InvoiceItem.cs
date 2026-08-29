using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: FATURA KALEMİ. Önceden fatura yalnız BAŞLIK düzeyinde tek bir tax_rate
    // taşıyordu; karışık sepette (giyim %10 + aksesuar %20) fatura matematiksel olarak YANLIŞ
    // çıkıyordu - tek oran tüm sepete uygulanıyordu.
    //
    // ORAN SNAPSHOT'I: vat_rate fatura kesildiği andaki EFEKTİF oranla doldurulur ve bir daha
    // değişmez. Kategori/ürün oranı sonradan güncellenirse ESKİ faturalar etkilenmez -
    // OrderItem.unit_price snapshot mantığının aynısı (fatura yasal bir belge; geçmişe dönük
    // değişmemeli).
    public class InvoiceItem : IEntity
    {
        public int id { get; set; }
        public int invoice_id { get; set; }
        // KARGO SÖZLEŞMESİ (MANTIK-FIX-2R / MIG): product_id NULL ise bu kalem bir ÜRÜN DEĞİL,
        // siparişin KARGO BEDELİDİR. Sahte bir "Kargo" ürünü AÇILMADI - katalog anonim uçlardan
        // görünür ve uydurma bir ürün oraya sızardı. FK KORUNUR: SQL Server NULL değerleri yabancı
        // anahtar kontrolünden muaf tutar, yani ürün kalemleri HÂLÂ products'a bağlıdır.
        // Kargo kalemi HER faturada TAM 1 tanedir (bedava kargoda line_total 0,00) - tek biçim,
        // ekranda dalsız gösterim, pinde tek sözleşme.
        public int? product_id { get; set; }
        public string product_name { get; set; }   // sipariş anındaki ad (ürün adı sonradan değişebilir)
        public int quantity { get; set; }
        public decimal unit_price { get; set; }    // KDV DAHİL birim fiyat (sipariş anı)
        public decimal line_subtotal { get; set; } // KDV hariç kalem tutarı
        public decimal vat_rate { get; set; }      // DONDURULMUŞ efektif oran (0.10 = %10)
        public decimal vat_amount { get; set; }    // kalem KDV tutarı
        public decimal line_total { get; set; }    // KDV dahil kalem tutarı (indirim payı düşülmüş)
        public DateTime created_at { get; set; }
    }
}
