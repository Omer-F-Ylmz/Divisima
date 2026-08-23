using System.Threading.Tasks;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Integrations.Notifications;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Notifications;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Merkezi sipariş-durumu bildirim servisi. Best-effort (bildirim hatasi ana akisi bozmaz).
    public class OrderNotificationManager : IOrderNotificationService
    {
        private readonly INotificationService _notificationService;
        private readonly ICustomerDeviceService _customerDeviceService;
        private readonly ISmsService _smsService;
        private readonly ICustomerDal _customerDal;
        // DALGA B / B4: e-posta kanali + takip numarasini tasiyan baglanti.
        private readonly IOutboxService _outboxService;
        private readonly IMailLinkBuilder _links;

        public OrderNotificationManager(INotificationService notificationService, ICustomerDeviceService customerDeviceService,
            ISmsService smsService, ICustomerDal customerDal, IOutboxService outboxService, IMailLinkBuilder links)
        {
            _notificationService = notificationService;
            _customerDeviceService = customerDeviceService;
            _smsService = smsService;
            _customerDal = customerDal;
            _outboxService = outboxService;
            _links = links;
        }

        public async Task NotifyStatusChangeAsync(Order order, OrderStatusEnum newStatus, string? kargoFirmasi = null, string? takipNo = null)
        {
            // Yalniz kargoya-verildi / teslim-edildi bildirilir (transactional - opt-out muaf; kritik guncelleme).
            if (newStatus != OrderStatusEnum.Shipped && newStatus != OrderStatusEnum.Delivered) return;

            var statusText = newStatus == OrderStatusEnum.Shipped
                ? "Siparişiniz kargoya verildi"
                : "Siparişiniz teslim edildi";
            var message = $"{statusText}. Sipariş no: {order.order_number}";

            var customer = await _customerDal.GetAsync(c => c.id == order.customer_id);

            // ══ DALGA B / B4 - E-POSTA (KALICI KANAL) ══════════════════════════════════════
            // OLCULEN ONCE-DURUM: bu servisin UC kanali da (SignalR / FCM / SMS) yapilandirilmis
            // bir saglayici ister; hicbiri yoksa musteriye HICBIR SEY ulasmiyordu. Ustelik hicbir
            // mesajda TAKIP NUMARASI yoktu - admin numarayi girdikten sonra musterinin onu
            // ogrenebilecegi tek yer kalmiyordu.
            //
            // ASAGIDAKI try/catch'in DISINDA duruyor - BILINCLI: o catch, dis saglayici
            // entegrasyonlarinin (SignalR/FCM/SMS) hatasini yutmak icin var. Outbox yazimi bir
            // DB satiridir; onu da yutmak "mail hic yazilmadi ve kimse bilmiyor" demek olurdu.
            // Outbox'a girdikten SONRA teslimat zaten yeniden denenir ve kalici hata Failed olarak GORUNUR.
            if (customer != null && !string.IsNullOrWhiteSpace(customer.email))
            {
                var baglanti = _links.VitrinBaglantisi("#/hesabim/siparislerim");
                var yonerge = baglanti == null
                    ? "Siparişini Hesabım > Siparişlerim sayfasından takip edebilirsin."
                    : $"Siparişini buradan takip edebilirsin:\n{baglanti}";

                string govde;
                if (newStatus == OrderStatusEnum.Shipped)
                {
                    // TAKIP SATIRI YALNIZ GERCEKTEN BILINIYORSA YAZILIR. ChangeOrderStatus bu bilgiyi
                    // tasimaz (kargo kaydini gormez) ve oradan gelen cagrida null gecer; uydurma bir
                    // numara ya da bos bir "Takip no:" satiri URETILMEZ.
                    var takipSatiri = string.IsNullOrWhiteSpace(takipNo)
                        ? ""
                        : $"\nKargo firması: {kargoFirmasi ?? "-"}\nTakip numarası: {takipNo}\n";
                    govde = $"Merhaba {customer.name},\n\n{order.order_number} numaralı siparişin kargoya verildi.\n{takipSatiri}\n{yonerge}";
                }
                else
                {
                    govde = $"Merhaba {customer.name},\n\n{order.order_number} numaralı siparişin teslim edildi. Afiyet olsun!\n\n{yonerge}";
                }

                await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = customer.email,
                    Subject = $"Divisima - {statusText.Replace("Siparişiniz", "Siparişin")}",
                    Body = govde
                });
            }

            try
            {
                // In-app (SignalR)
                await _notificationService.NotifyCustomerAsync(order.customer_id, message);
                // Push (FCM) - musterinin kayitli cihazlarina
                await _customerDeviceService.NotifyCustomerAsync(order.customer_id, "Divisima", message);
                // SMS - musteri telefonuna
                if (customer != null && !string.IsNullOrEmpty(customer.phone))
                    await _smsService.SendAsync(customer.phone, message);
            }
            catch
            {
                // Bildirim hatasi ana akisi bozmaz (ikincil). Gercekte ILogger ile loglanir.
            }
        }
    }
}
