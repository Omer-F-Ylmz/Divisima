namespace Divisima.Core.Integrations.Iyzico
{
    // Açıklayıcı yorum: Iyzico provider soyutlaması (güvenli akış).
    // KRİTİK: callback'in gövdesine ASLA güvenilmez; sonuç her zaman sunucu-sunucu (token ile) yeniden sorgulanır.
    public interface IIyzicoClient
    {
        // Açıklayıcı yorum: Checkout Form başlat - kart bilgisi Iyzico'nun iframe'ine gider, sunucu görmez (PCI-DSS).
        Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request);

        // Açıklayıcı yorum: Callback sonrası GERÇEK sonucu Iyzico'dan token ile çek (güven kaynağı budur).
        Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token);

        // Açıklayıcı yorum: Callback imzasını doğrula (Iyzico'nun gönderdiği HMAC) - sahte callback engeli.
        bool VerifyCallbackSignature(string token, string signature);

        // Açıklayıcı yorum: İade (refund) - ödemeyi kısmen/tamamen geri öder. paymentTransactionId Iyzico'dan gelir.
        Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount);
    }

    // Açıklayıcı yorum: Checkout Form başlatma isteği - KART BİLGİSİ YOK (Iyzico iframe toplar).
    public class IyzicoCheckoutInitRequest
    {
        public string ConversationId { get; set; }
        public decimal Price { get; set; }
        public string CallbackUrl { get; set; }
        public int CustomerId { get; set; }
        public string BuyerName { get; set; }
        public string BuyerEmail { get; set; }
    }
    public class IyzicoCheckoutInitResult
    {
        public bool Success { get; set; }
        public string CheckoutFormContent { get; set; }  // Iyzico iframe HTML/script (frontend gösterir)
        public string Token { get; set; }                 // sonucu sorgulamak için
        public string ErrorMessage { get; set; }
    }
    // Açıklayıcı yorum: İade sonucu.
    public class IyzicoRefundResult
    {
        public bool Success { get; set; }
        public string? RefundId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Açıklayıcı yorum: Iyzico'dan çekilen gerçek ödeme sonucu (güven kaynağı).
    public class IyzicoPaymentResult
    {
        public bool Success { get; set; }
        public string PaymentId { get; set; }
        public string ConversationId { get; set; }
        public decimal PaidPrice { get; set; }            // GERÇEKTEN ödenen tutar (tutar doğrulaması için)
        public int Installment { get; set; } = 1;         // secilen taksit sayisi (Iyzico sonucu)
        public string Currency { get; set; }
        public string FraudStatus { get; set; }           // Iyzico fraud skoru (1=onay, 0=inceleme, -1=red)
        public string ErrorMessage { get; set; }
    }
}
