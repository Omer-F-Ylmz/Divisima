using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Coupon
{
    // Açıklayıcı yorum: Kupon doğrulama isteği (frontend applyCoupon: kod + sepet tutarı).
    public class CouponValidateRequestDto : IDto
    {
        public string code { get; set; }
        public decimal cart_total { get; set; }
        public int customer_id { get; set; } // token'dan (ilk-sipariş kontrolü için)
    }
}
