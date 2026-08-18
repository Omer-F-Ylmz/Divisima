using System.Threading.Tasks;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.RateLimiting;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Fraud/hız kontrolü. Müşteri başına 10 dk içinde max 5 ödeme denemesi (kart testi/kaba kuvvet limiti).
    // ATOMİK dağıtık rate limiter (Redis Lua INCR+EXPIRE / in-memory lock) kullanır -> ESZAMANLI denemeler sayacı DOĞRU
    // artırır. Önceki cache-based check-then-act (oku-sil-yaz) LOST-UPDATE race'liydi: paralel istekler limiti aşabiliyordu.
    public class FraudCheckManager : IFraudCheckService
    {
        private readonly IDistributedRateLimiter _rateLimiter;
        private const int MaxAttempts = 5;
        private const int WindowSeconds = 600; // 10 dk

        public FraudCheckManager(IDistributedRateLimiter rateLimiter)
        {
            _rateLimiter = rateLimiter;
        }

        // Açıklayıcı yorum: TEK ATOMİK çağrı - sayacı artırır VE limit aşıldı mı döner. Ayrı check+record (TOCTOU)
        // yerine tek işlem: eşzamanlı denemeler arasında "önce kontrol sonra kaydet" boşluğu YOK.
        public async Task<bool> CanAttemptPaymentAsync(int customerId)
        {
            var result = await _rateLimiter.CheckAsync($"payment-attempts:{customerId}", MaxAttempts, WindowSeconds);
            return result.Allowed;
        }
    }
}
