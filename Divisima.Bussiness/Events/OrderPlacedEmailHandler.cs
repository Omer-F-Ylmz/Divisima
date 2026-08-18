using Divisima.Core.Utilities.Mail;

namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Sipariş onay maili handler'ı. IMailService ile gönderir (SMTP/SendGrid).
    public class OrderPlacedEmailHandler : IOrderPlacedEventHandler
    {
        private readonly IMailService _mailService;

        public OrderPlacedEmailHandler(IMailService mailService)
        {
            _mailService = mailService;
        }

        public async Task HandleAsync(OrderPlacedEvent @event)
        {
            // Açıklayıcı yorum: Sipariş onay maili (gerçek adres customer'dan çekilebilir)
            await _mailService.SendAsync(new MailMessageDto
            {
                Subject = $"Siparişiniz alındı - #{@event.order_number}",
                Body = $"Merhaba, #{@event.order_number} numaralı siparişiniz başarıyla oluşturuldu. Tutar: {@event.total} TL.",
                To = $"customer-{@event.customer_id}@divisima.local"
            });
        }
    }
}
