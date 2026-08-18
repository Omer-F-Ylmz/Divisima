using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Payment
{
    // Açıklayıcı yorum: Ödeme başlatma - SADECE sipariş id. Kart bilgisi Iyzico Checkout Form iframe'inde
    // toplanır; sunucumuza HİÇ gelmez. Böylece PCI-DSS kapsamı ciddi şekilde daralır (kart verisi taşımayız).
    public class PaymentInitRequestDto : IDto
    {
        public int order_id { get; set; }
        public string? callback_url { get; set; }
    }
}
