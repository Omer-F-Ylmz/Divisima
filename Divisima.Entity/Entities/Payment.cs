using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Ödeme kaydı (Cafixo WebPayment kalıbı). Güvenli akış alanları:
    // paid_price = Iyzico'dan dönen GERÇEK ödenen tutar (tutar doğrulaması), fraud_status = Iyzico skoru, token.
    // KART BİLGİSİ TUTULMAZ (PCI-DSS) - Checkout Form iframe toplar, sunucu görmez.
    public class Payment : IEntity
    {
        public int id { get; set; }
        public int order_id { get; set; }
        public string payment_provider { get; set; }   // "iyzico"
        public byte payment_status { get; set; }        // Beklemede (0), Başarılı (1), Başarısız (2)
        public decimal amount { get; set; }             // beklenen tutar (sipariş toplamı)
        public decimal? paid_price { get; set; }         // Iyzico'dan dönen gerçek ödenen tutar (taksit komisyonu dahil)
        public byte installment_count { get; set; } = 1; // secilen taksit sayisi (1 = tek cekim)
        public decimal? installment_fee { get; set; }    // taksit komisyonu (paid_price - order.total)
        public string? currency { get; set; }
        public string? fraud_status { get; set; }        // Iyzico fraud skoru (1=onay)
        public string? transaction_id { get; set; }      // provider işlem id (paymentId)
        public string? conversation_id { get; set; }     // eşleme
        public string? token { get; set; }               // Checkout Form sonucu sorgulama anahtarı
        public DateTime? paid_at { get; set; }
        public DateTime created_at { get; set; }
    }
}
