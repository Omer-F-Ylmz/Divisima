using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Kargo/sevkiyat. Sipariş kargoya verilince oluşturulur (admin takip no girer).
    // Kargo firması API'sinden durum sorgulanır. Düz yapı (Cafixo) - byte carrier/status, nullable tarihler.
    public class Shipment : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public byte carrier { get; set; }             // Yurtiçi (0), Aras (1), MNG (2), PTT (3), Sürat (4)
        public string tracking_number { get; set; }   // kargo takip numarası
        public byte status { get; set; }              // Hazırlanıyor (0), Yolda (1), Dağıtımda (2), TeslimEdildi (3), İadeDe (4)
        public string? last_status_text { get; set; } // firma API'sinden gelen ham durum metni
        public DateTime? shipped_at { get; set; }
        public DateTime? estimated_delivery { get; set; }
        public DateTime? delivered_at { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? last_checked_at { get; set; } // en son ne zaman firma API'sinden sorgulandı
    }
}
