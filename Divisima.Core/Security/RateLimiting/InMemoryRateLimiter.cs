using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Divisima.Core.Security.RateLimiting
{
    // Açıklayıcı yorum: In-memory ATOMİK rate limiter (dev/tek-sunucu fallback). Redis yoksa IDistributedRateLimiter
    // aynı arayüzle çalışır -> ona bağımlı servisler (FraudCheckManager) dev'de de resolve olur (DI kırılmaz).
    // lock ile THREAD-SAFE: eşzamanlı istekler sayacı DOĞRU artırır (cache-based check-then-act'teki lost-update YOK).
    public class InMemoryRateLimiter : IDistributedRateLimiter
    {
        private sealed class Counter { public int Count; public DateTime WindowEnd; }
        private readonly ConcurrentDictionary<string, Counter> _counters = new();
        private readonly object _lock = new();

        public Task<RateLimitResult> CheckAsync(string key, int limit, int windowSeconds)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                if (!_counters.TryGetValue(key, out var c) || now >= c.WindowEnd)
                {
                    // Yeni anahtar veya pencere doldu -> sabit-pencere sıfırla
                    c = new Counter { Count = 0, WindowEnd = now.AddSeconds(windowSeconds) };
                    _counters[key] = c;
                }
                c.Count++;   // ATOMİK artış (lock altında) - eşzamanlı çağrılar kaybolmaz
                var allowed = c.Count <= limit;
                var ttl = (int)Math.Ceiling((c.WindowEnd - now).TotalSeconds);
                return Task.FromResult(new RateLimitResult
                {
                    Allowed = allowed,
                    Remaining = Math.Max(0, limit - c.Count),
                    RetryAfterSeconds = allowed ? 0 : (ttl > 0 ? ttl : windowSeconds)
                });
            }
        }
    }
}
