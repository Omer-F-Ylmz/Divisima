using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ana kategori (frontend MAINS). Düz yapı.
    public class Category : IEntity
    {
        public int id { get; set; }
        public string name { get; set; }
        public string slug { get; set; }
        public int display_order { get; set; }
        // KDV oranı (0.10 = %10). NULL = tanımlı değil -> EInvoice:KdvRate varsayılanına düşer.
        // Giyim %10, aksesuar %20 gibi farklı oranlar burada tutulur; ürün bazında override edilebilir
        // (bkz. Product.vat_rate). Fatura kesilirken oran KALEME KOPYALANIR (snapshot).
        public decimal? vat_rate { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
