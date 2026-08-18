using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Divisima.Core.Security.RateLimiting
{
    // Açıklayıcı yorum: Redis tabanlı dağıtık rate limiter. Atomik sabit-pencere sayacı (Lua: INCR + ilk artışta EXPIRE).
    // Tüm sunucular aynı Redis sayacını kullanır → limit gerçekten global. Redis erişilemezse fail-open (servis kesilmez).
    public class RedisRateLimiter : IDistributedRateLimiter
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisRateLimiter> _logger;

        // Açıklayıcı yorum: Atomik betik - yarış koşulu yok (INCR + koşullu EXPIRE tek işlem)
        private const string LuaScript = @"
local current = redis.call('INCR', KEYS[1])
if current == 1 then
  redis.call('EXPIRE', KEYS[1], ARGV[1])
end
local ttl = redis.call('TTL', KEYS[1])
return {current, ttl}";

        public RedisRateLimiter(IConnectionMultiplexer redis, ILogger<RedisRateLimiter> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<RateLimitResult> CheckAsync(string key, int limit, int windowSeconds)
        {
            try
            {
                var db = _redis.GetDatabase();
                var result = (RedisValue[])await db.ScriptEvaluateAsync(LuaScript,
                    new RedisKey[] { $"ratelimit:{key}" },
                    new RedisValue[] { windowSeconds });

                var current = (int)result[0];
                var ttl = (int)result[1];
                var allowed = current <= limit;

                return new RateLimitResult
                {
                    Allowed = allowed,
                    Remaining = Math.Max(0, limit - current),
                    RetryAfterSeconds = allowed ? 0 : (ttl > 0 ? ttl : windowSeconds)
                };
            }
            catch (Exception ex)
            {
                // Açıklayıcı yorum: Redis kesintisinde fail-open - rate limit yerine servis erişilebilirliğini koru
                _logger.LogWarning(ex, "Redis rate limiter erişilemedi, istek geçirildi (fail-open)");
                return new RateLimitResult { Allowed = true, Remaining = limit };
            }
        }
    }
}
