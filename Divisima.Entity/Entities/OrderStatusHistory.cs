using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sipariş durum geçmişi (zaman çizelgesi). Her durum değişimi bir satır - müşteri
    // siparişin yolculuğunu görür (Beklemede -> Onaylandı -> Kargoda -> Teslim). Nav property yok.
    public class OrderStatusHistory : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public byte status { get; set; } // OrderStatusEnum değeri
        public string note { get; set; } // opsiyonel açıklama (örn. "Ödeme onaylandı")
        public DateTime created_at { get; set; }
    }
}
