using Divisima.API.Hubs;
using Divisima.Core.Utilities.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Divisima.API.Services
{
    // Açıklayıcı yorum: INotificationService'in SignalR implementasyonu.
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRNotificationService(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public async Task NotifyAdminsAsync(string message)
        {
            await _hub.Clients.Group("admins").SendAsync("ReceiveNotification", message);
        }

        public async Task NotifyCustomerAsync(int customerId, string message)
        {
            await _hub.Clients.User(customerId.ToString()).SendAsync("ReceiveNotification", message);
        }
    }
}
