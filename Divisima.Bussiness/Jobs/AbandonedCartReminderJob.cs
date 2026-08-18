using Divisima.Bussiness.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Jobs
{
    // Açıklayıcı yorum: Terk edilmiş sepet hatırlatma job'u (Hangfire, saatlik). Atıl dolu sepetlere e-posta.
    public class AbandonedCartReminderJob
    {
        private readonly IAbandonedCartService _abandonedCartService;
        private readonly ILogger<AbandonedCartReminderJob> _logger;

        public AbandonedCartReminderJob(IAbandonedCartService abandonedCartService, ILogger<AbandonedCartReminderJob> logger)
        {
            _abandonedCartService = abandonedCartService;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var sent = await _abandonedCartService.SendReminders();
            if (sent > 0)
                _logger.LogInformation("Terk edilmiş sepet hatırlatması: {Count} e-posta gönderildi", sent);
        }
    }
}
