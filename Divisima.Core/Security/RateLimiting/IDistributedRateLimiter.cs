namespace Divisima.Core.Security.RateLimiting
{
    // Açıklayıcı yorum: Dağıtık rate limiter. Çok sunuculu ortamda merkezi sayaç (Redis) - limit gerçekten paylaşılır.
    public interface IDistributedRateLimiter
    {
        // Açıklayıcı yorum: key (IP+kapsam) için windowSeconds içinde limit aşıldı mı; kalan izin döner
        Task<RateLimitResult> CheckAsync(string key, int limit, int windowSeconds);
    }

    public class RateLimitResult
    {
        public bool Allowed { get; set; }
        public int Remaining { get; set; }
        public int RetryAfterSeconds { get; set; }
    }
}
