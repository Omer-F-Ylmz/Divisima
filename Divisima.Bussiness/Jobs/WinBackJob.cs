using Divisima.Bussiness.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Jobs
{
    // Açıklayıcı yorum: WinBack kampanya job'u (Hangfire, günlük).
    public class WinBackJob
    {
        private readonly IEngagementService _engagementService;
        private readonly ILogger<WinBackJob> _logger;

        public WinBackJob(IEngagementService engagementService, ILogger<WinBackJob> logger)
        {
            _engagementService = engagementService;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var sent = await _engagementService.SendWinBackCampaigns();
            if (sent > 0)
                _logger.LogInformation("WinBack: {Count} e-posta gönderildi", sent);
        }
    }
}
