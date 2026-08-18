using Divisima.Core.Utilities.Dtos;

namespace Divisima.Entity.Dtos.Payment
{
    // Açıklayıcı yorum: Checkout Form içeriği (frontend iframe'i gösterir) + token.
    public class PaymentInitResponseDto : IDto
    {
        public string conversation_id { get; set; }
        public string checkout_form_content { get; set; }
    }
}
