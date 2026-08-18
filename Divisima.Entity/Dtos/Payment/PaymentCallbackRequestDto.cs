using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Payment
{
    // Açıklayıcı yorum: Iyzico callback - SADECE token + imza. Sonuç/status'a güvenilmez;
    // gerçek sonuç token ile Iyzico'dan yeniden çekilir. Signature sahte callback'i eler.
    public class PaymentCallbackRequestDto : IDto
    {
        public string token { get; set; }
        public string? signature { get; set; }
    }
}
