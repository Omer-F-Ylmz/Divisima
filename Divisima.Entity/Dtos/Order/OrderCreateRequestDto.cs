using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Order
{
    // Açıklayıcı yorum: Sipariş oluşturma isteği (frontend checkout). Kalemler + adres + opsiyonel kupon.
    public class OrderCreateRequestDto : IDto
    {
        public int customer_id { get; set; }
        public string? request_id { get; set; } // idempotency (çift sipariş engeli)
        public int? address_id { get; set; }
        public string coupon_code { get; set; }
        public decimal use_store_credit { get; set; } // checkout'ta kullanilacak magaza kredisi (0 = kullanma)
        public byte payment_method { get; set; } // 0 = Online (Iyzico), 1 = Kapida Odeme (COD)
        public List<OrderItemRequestDto> items { get; set; }
    }
}
