using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Sipariş (Cafixo Order kalıbı). byte status/payment_type, düz yapı.
    public class Order : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public string order_number { get; set; }
        public string? request_id { get; set; } // idempotency anahtarı (WebOrder kalıbı) - çift sipariş engeli
        public byte status { get; set; } // Pending (0), Confirmed (1), Preparing (2), Shipped (3), Delivered (4), Cancelled (5)
        public decimal subtotal { get; set; }
        public decimal discount_amount { get; set; }
        public decimal shipping_cost { get; set; }
        public decimal total_price { get; set; }
        public string currency { get; set; } = "TRY"; // para birimi (ödeme doğrulamasında kullanılır)
        public string? coupon_code { get; set; }
        public int? address_id { get; set; }
        public byte payment_type { get; set; } // Online Ödeme (0), Kapıda Ödeme (1)
        public decimal store_credit_used { get; set; } = 0m; // checkout'ta uygulanan cuzdan/mağaza kredisi
        // KUMULATIF IADE SAYACI: bu siparis icin bugune kadar iade edilen TOPLAM tutar.
        // RefundToSourceAsync her iadede atomik olarak artirir ve toplam total_price'i ASLA asamaz.
        // Onceden yalnizca TEK cagri icinde kirpma vardi -> ardisik iki tam iade siparis tutarinin
        // iki katini geri odeyebiliyordu (Iyzico'ya da iki kez gidiyordu).
        public decimal refunded_amount { get; set; } = 0m;
        public byte installment_count { get; set; } = 1;   // taksit sayisi (1 = tek cekim)
        public bool is_online_payment_done { get; set; }
        public string? payment_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? delivered_at { get; set; } // teslim zamani (iade penceresi bundan sayilir)
        public DateTime? review_invite_sent_at { get; set; } // yorum daveti gönderildi mi
    }
}
