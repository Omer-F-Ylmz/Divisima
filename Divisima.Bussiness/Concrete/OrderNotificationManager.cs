using System.Threading.Tasks;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Notifications;
using Divisima.Core.Utilities.Enums;
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

        public OrderNotificationManager(INotificationService notificationService, ICustomerDeviceService customerDeviceService,
            ISmsService smsService, ICustomerDal customerDal)
        {
            _notificationService = notificationService;
            _customerDeviceService = customerDeviceService;
            _smsService = smsService;
            _customerDal = customerDal;
        }

        public async Task NotifyStatusChangeAsync(Order order, OrderStatusEnum newStatus)
        {
            // Yalniz kargoya-verildi / teslim-edildi bildirilir (transactional - opt-out muaf; kritik guncelleme).
            if (newStatus != OrderStatusEnum.Shipped && newStatus != OrderStatusEnum.Delivered) return;

            var statusText = newStatus == OrderStatusEnum.Shipped
                ? "Siparişiniz kargoya verildi"
                : "Siparişiniz teslim edildi";
            var message = $"{statusText}. Sipariş no: {order.order_number}";

            try
            {
                // In-app (SignalR)
                await _notificationService.NotifyCustomerAsync(order.customer_id, message);
                // Push (FCM) - musterinin kayitli cihazlarina
                await _customerDeviceService.NotifyCustomerAsync(order.customer_id, "Divisima", message);
                // SMS - musteri telefonuna
                var customer = await _customerDal.GetAsync(c => c.id == order.customer_id);
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
