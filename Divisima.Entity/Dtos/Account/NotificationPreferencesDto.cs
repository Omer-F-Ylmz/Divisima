using Divisima.Core.Utilities.Dtos;
namespace Divisima.Entity.Dtos.Account
{
    public class NotificationPreferencesDto : IDto
    {
        public bool notify_email { get; set; }
        public bool notify_sms { get; set; }
        public bool notify_push { get; set; }
    }
}
