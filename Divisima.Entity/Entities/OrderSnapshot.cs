using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sipariş anı fotoğrafı (Cafixo OrderSnapshot kalıbı) - sipariş anını dondurur.
    public class OrderSnapshot : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; } // Snapshot'ı oluşturan siparişin ID'si
        public int customer_id { get; set; }
        public string customer_full_name { get; set; }
        public string? shipping_address { get; set; }
        public byte status { get; set; }
        public decimal subtotal { get; set; }
        public decimal discount_amount { get; set; }
        public decimal shipping_cost { get; set; }
        public decimal total_price { get; set; }
        public string? coupon_code { get; set; }
        public DateTime snapshot_created_at { get; set; }
        public DateTime order_created_at { get; set; }
    }
}
