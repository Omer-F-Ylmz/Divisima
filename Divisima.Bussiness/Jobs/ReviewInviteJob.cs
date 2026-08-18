using Divisima.Bussiness.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Jobs
{
    // Açıklayıcı yorum: ReviewInvite kampanya job'u (Hangfire, günlük).
    public class ReviewInviteJob
    {
        private readonly IEngagementService _engagementService;
        private readonly ILogger<ReviewInviteJob> _logger;

        public ReviewInviteJob(IEngagementService engagementService, ILogger<ReviewInviteJob> logger)
        {
            _engagementService = engagementService;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var sent = await _engagementService.SendReviewInvites();
            if (sent > 0)
                _logger.LogInformation("ReviewInvite: {Count} e-posta gönderildi", sent);
        }
    }
}
