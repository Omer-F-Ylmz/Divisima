using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: İade/değişim talebi. Müşteri teslim edilmiş sipariş için açar; admin onaylar/reddeder.
    // Onayda Iyzico refund + stok iade. Düz yapı (Cafixo) - navigation yok, FK id'ler + byte durum.
    public class ReturnRequest : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public int customer_id { get; set; }         // sahiplik (IDOR kontrolü)
        public int product_id { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public byte reason { get; set; }              // Beğenmedim (0), Beden uymadı (1), Kusurlu (2), Yanlış ürün (3)
        public string? description { get; set; }      // müşteri açıklaması (sanitize edilir)
        public byte return_type { get; set; }         // İade (0), Değişim (1)
        public byte status { get; set; }              // Beklemede (0), Onaylandı (1), Reddedildi (2), Tamamlandı (3)
        public decimal refund_amount { get; set; }    // iade tutarı (onayda hesaplanır)
        public string? refund_id { get; set; }        // Iyzico refund id
        public string? admin_note { get; set; }       // admin ret/onay notu
        public DateTime created_at { get; set; }
        public DateTime? processed_at { get; set; }
    }
}
