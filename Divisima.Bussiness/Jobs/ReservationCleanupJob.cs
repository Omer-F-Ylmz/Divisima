using Divisima.Bussiness.Abstract;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Jobs
{
    // Açıklayıcı yorum: Süresi dolan stok rezervasyonlarını serbest bırakan job (Hangfire, 5 dk).
    // Terk edilen sepetlerde (müşteri ödemeye dönmezse) rezerve edilen stok geri kazanılır - hayalet kayıp önlenir.
    public class ReservationCleanupJob
    {
        private readonly IStockService _stockService;
        private readonly ILogger<ReservationCleanupJob> _logger;

        public ReservationCleanupJob(IStockService stockService, ILogger<ReservationCleanupJob> logger)
        {
            _stockService = stockService;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var released = await _stockService.ReleaseExpiredReservations();
            if (released > 0)
                _logger.LogInformation("Süresi dolan {Count} stok rezervasyonu serbest bırakıldı", released);
        }
    }
}
