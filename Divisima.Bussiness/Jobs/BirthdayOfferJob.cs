using Divisima.Bussiness.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Jobs
{
    // Açıklayıcı yorum: BirthdayOffer kampanya job'u (Hangfire, günlük).
    public class BirthdayOfferJob
    {
        private readonly IEngagementService _engagementService;
        private readonly ILogger<BirthdayOfferJob> _logger;

        public BirthdayOfferJob(IEngagementService engagementService, ILogger<BirthdayOfferJob> logger)
        {
            _engagementService = engagementService;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var sent = await _engagementService.SendBirthdayOffers();
            if (sent > 0)
                _logger.LogInformation("BirthdayOffer: {Count} e-posta gönderildi", sent);
        }
    }
}
