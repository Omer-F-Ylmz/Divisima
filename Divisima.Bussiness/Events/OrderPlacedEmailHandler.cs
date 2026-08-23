using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Sipariş onay maili handler'ı.
    //
    // ══ LAUNCH-FIX A1(a) - ALICI ADRESI SAHTEYDI ═══════════════════════════════════════════
    // OLCULEN ONCE-DURUM: To = $"customer-{@event.customer_id}@divisima.local".
    // Yani siparis onay maili MUSTERIYE HIC GITMIYORDU. Ustelik ".local" yonlendirilemez bir
    // ust alan adidir: gercek bir SMTP sunucusuyla bu gonderim RCPT TO asamasinda REDDEDILIR ve
    // SmtpMailService (bilincli olarak) ISTISNA FIRLATIR.
    //
    // ADRESIN KAYNAGI OLCULDU, UYDURULMADI: siparisin musterisi HER IKI YOLDA da customers
    // tablosunda GERCEK e-postasiyla duruyor -
    //   uye siparisi    -> customer_id token'dan gelir (OrderController.Place)
    //   misafir siparisi -> GuestCheckoutManager once Customer satirini dto.guest_email ile
    //                       OLUSTURUR, sonra PlaceOrder'a o id ile devreder.
    // Bu yuzden tek dogru kaynak customer_id uzerinden okumaktir; event'e ayri bir e-posta alani
    // EKLENMEDI (snapshot degil, GUNCEL adres istenir - musteri adresini degistirmis olabilir).
    public class OrderPlacedEmailHandler : IOrderPlacedEventHandler
    {
        private readonly IMailService _mailService;
        private readonly ICustomerDal _customerDal;
        private readonly IMailLinkBuilder _links;
        private readonly ILogger<OrderPlacedEmailHandler> _logger;

        public OrderPlacedEmailHandler(IMailService mailService, ICustomerDal customerDal,
            IMailLinkBuilder links, ILogger<OrderPlacedEmailHandler> logger)
        {
            _mailService = mailService;
            _customerDal = customerDal;
            _links = links;
            _logger = logger;
        }

        public async Task HandleAsync(OrderPlacedEvent @event)
        {
            var customer = await _customerDal.GetAsync(c => c.id == @event.customer_id);
            if (customer == null || string.IsNullOrWhiteSpace(customer.email))
            {
                // SESSIZ GECISTIRME YOK: adres yoksa mail atilamaz, ama bu bir VERI sorunudur ve
                // gorunmelidir. Istisna FIRLATILMIYOR - firlatilsa outbox mesaji 5 kez yeniden
                // denenir ve her denemede AYNI sonucu verirdi (musteri satiri kendiliginden
                // dolmaz); gurultulu log dogru kanal.
                _logger.LogError("SIPARIS ONAY MAILI GONDERILEMEDI - musteri/e-posta yok. "
                    + "order_id={OrderId} customer_id={CustomerId}", @event.order_id, @event.customer_id);
                return;
            }

            var link = _links.VitrinBaglantisi("#/hesabim/siparislerim");
            var takip = link == null
                ? "\n\nSiparişinin durumunu Hesabım > Siparişlerim sayfasından takip edebilirsin."
                : $"\n\nSiparişinin durumunu buradan takip edebilirsin:\n{link}";

            await _mailService.SendAsync(new MailMessageDto
            {
                To = customer.email,
                Subject = $"Divisima - Siparişin alındı (#{@event.order_number})",
                Body = $"Merhaba {customer.name},\n\n#{@event.order_number} numaralı siparişin "
                     + $"başarıyla oluşturuldu.\nTutar: {@event.total:N2} TL." + takip + "\n\nDivisima",
                IsHtml = false
            });
        }
    }
}
