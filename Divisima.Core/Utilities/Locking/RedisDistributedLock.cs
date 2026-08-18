using StackExchange.Redis;

namespace Divisima.Core.Utilities.Locking
{
    // Açıklayıcı yorum: Redis tabanlı dağıtık kilit (SET NX PX). Çok sunuculu ortamda gerçek kilit.
    // Değer benzersiz token; bırakırken sadece kendi token'ıysa siler (başkasının kilidini açmaz).
    public class RedisDistributedLock : IDistributedLock
    {
        private readonly IConnectionMultiplexer _redis;
        public RedisDistributedLock(IConnectionMultiplexer redis) => _redis = redis;

        public async Task<IDisposable> AcquireAsync(string key, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            var token = Guid.NewGuid().ToString("N");
            // Açıklayıcı yorum: SET key token NX PX expiry - yalnızca yoksa yazar (atomik kilit)
            if (!await db.StringSetAsync(key, token, expiry, When.NotExists))
                return null;
            return new Releaser(db, key, token);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly IDatabase _db; private readonly string _key; private readonly string _token;
            public Releaser(IDatabase db, string key, string token) { _db = db; _key = key; _token = token; }
            public void Dispose()
            {
                // Açıklayıcı yorum: Lua script - yalnızca kendi token'ımızsa sil (güvenli bırakma)
                const string lua = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
                _db.ScriptEvaluate(lua, new RedisKey[] { _key }, new RedisValue[] { _token });
            }
        }
    }
}
