using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş kalemi dönüşü.
    public class OrderItemResponseDto : IDto
    {
        public int product_id { get; set; }
        public string product_name { get; set; }
        public string size { get; set; }
        public int quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal line_total { get; set; }
        // TUTARSIZLIK FIX (H44): iptal edilmiş kalem sipariş detayında görünüyordu ama İPTAL OLDUĞU BELLİ DEĞİLDİ ->
        // müşterinin gördüğü kalem toplamları order.total_price ile TUTMUYORDU (iptal düşülmüş). Bu bayrak ile
        // arayüz "İptal edildi" gösterebilir ve toplam mutabık kalır (aktif kalemlerin toplamı = sipariş toplamı).
        public bool is_cancelled { get; set; }
    }
}
