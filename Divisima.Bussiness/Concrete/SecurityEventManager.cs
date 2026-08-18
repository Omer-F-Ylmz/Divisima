using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Notifications;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Güvenlik olay yöneticisi. DB'ye yazar, structured log'a düşer, Critical ise admin'e bildirir.
    public class SecurityEventManager : ISecurityEventService
    {
        private readonly ISecurityEventDal _dal;
        private readonly INotificationService _notification;
        private readonly ILogger<SecurityEventManager> _logger;

        public SecurityEventManager(ISecurityEventDal dal, INotificationService notification, ILogger<SecurityEventManager> logger)
        {
            _dal = dal;
            _notification = notification;
            _logger = logger;
        }

        public async Task LogAsync(string eventType, string severity, int? customerId, string? ip, string? userAgent, string? detail)
        {
            await _dal.AddAsync(new SecurityEvent
            {
                event_type = eventType,
                severity = severity,
                customer_id = customerId,
                ip_address = ip,
                user_agent = userAgent,
                detail = detail,
                created_at = DateTime.Now
            });
            // Açıklayıcı yorum: Structured log (Serilog -> SIEM'e akıtılabilir)
            _logger.LogWarning("SECURITY {EventType} {Severity} customer={CustomerId} ip={Ip} {Detail}",
                eventType, severity, customerId, ip, detail);
            // Açıklayıcı yorum: Kritik olayda admin'e anlık bildirim
            if (severity == "Critical")
                await _notification.NotifyAdminsAsync($"[GÜVENLİK] {eventType}: {detail} (IP: {ip})");
        }
    }
}
