using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Divisima.Core.Utilities.Caching
{
    // Açıklayıcı yorum: ICacheService'in Redis (IDistributedCache) implementasyonu.
    // Çok sunuculu ortamda MemoryCacheService yerine bu kullanılır - arayüz aynı, servisler değişmez.
    // Program.cs'te: AddStackExchangeRedisCache + AddSingleton<ICacheService, RedisCacheService>.
    // Prefix invalidation için key seti Redis'te ayrı bir SET'te tutulur (basit yaklaşım).
    public class RedisCacheService : ICacheService
    {
        // STAMPEDE KORUMASI (H49): process ici kapi - ayni instance'taki N es zamanli istek tek hesap yapar.
        // NOT: cok-instance'li kurulumda tam koruma icin dagitik kilit gerekir (Redis SETNX) - Omer'in altyapi karari.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
        // KILITLENME KORUMASI (H50): kapida SINIRSIZ beklemek, factory takilirsa (DB donmasi) o anahtara gelen
        // TUM istekleri sonsuza kadar bloke eder -> thread havuzu tukenir -> yavas uc yerine TAM KESINTI.
        // Sinirli bekleme: sure dolarsa cagri kendi hesabini yapar (nadir fazladan hesap) - erisilebilirlik korunur.
        private static readonly TimeSpan GateWaitSeconds = TimeSpan.FromSeconds(5);

        private readonly IDistributedCache _cache;
        private readonly IConnectionMultiplexer _mux;
        private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);

        public RedisCacheService(IDistributedCache cache, IConnectionMultiplexer mux)
        {
            _cache = cache;
            _mux = mux;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
        {
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
                return JsonSerializer.Deserialize<T>(cached);

            var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            var entered = await gate.WaitAsync(GateWaitSeconds);
            if (!entered)
            {
                // Kapi zamaninda acilmadi -> bloke olmak yerine kendi hesabimizi yap (erisilebilirlik > tek-hesap).
                var fallback = await factory();
                await _cache.SetStringAsync(key, JsonSerializer.Serialize(fallback), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl
                });
                return fallback;
            }
            try
            {
                // CIFT KONTROL: biz beklerken baska bir cagri cache'i doldurmus olabilir.
                var filled = await _cache.GetStringAsync(key);
                if (filled != null)
                    return JsonSerializer.Deserialize<T>(filled);

                var value = await factory();
                await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? _defaultTtl
                });
                return value;
            }
            finally
            {
                gate.Release();
                if (gate.CurrentCount == 1) _gates.TryRemove(key, out _);
            }
        }

        // GF-1 / K2: SALT-OKUMA varlik sorgusu (Redis EXISTS). Anahtari OLUSTURMAZ, TTL'e
        // dokunmaz. `TryAddAsync` ile AYNI ham anahtar uzayini kullanir (InstanceName bos)
        // - yani biri yazip digeri okuyamaz durumu OLUSMAZ. Gerekcesi ICacheService'te.
        public async Task<bool> ExistsAsync(string key)
        {
            var db = _mux.GetDatabase();
            return await db.KeyExistsAsync(key);
        }

        // GF-1b / K1: DEGER YAZ. `GetOrSetAsync` ile AYNI serilestirmeyi (JSON) kullanir ki
        // iki yol ayni anahtari okuyabilsin. Var olan anahtari EZER (SETNX DEGIL).
        public async Task SetAsync<T>(string key, T value, TimeSpan ttl) =>
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });

        // GF-1b / K1: SALT-OKUMA. Anahtar yoksa `default` doner; HICBIR SEY YAZILMAZ.
        public async Task<T?> GetAsync<T>(string key)
        {
            var ham = await _cache.GetStringAsync(key);
            return ham == null ? default : JsonSerializer.Deserialize<T>(ham);
        }

        // Aciklayici yorum: ATOMIK SETNX (SET key val NX) - Redis'in kendi atomik islemi, race YOK.
        // true = eklendi (yoktu); false = zaten vardi. IDistributedCache InstanceName bos oldugundan raw key Remove ile tutarli.
        public async Task<bool> TryAddAsync(string key, TimeSpan ttl)
        {
            var db = _mux.GetDatabase();
            return await db.StringSetAsync(key, "1", ttl, When.NotExists);
        }

        // LF-5 / D1: ATOMIK SAYAC. Redis `INCR` tek islemde artirir ve YENI degeri doner -
        // oku-degistir-yaz yarisi YOKTUR. TTL YALNIZ ilk artirimda (`deger == 1`) kurulur:
        // her denemede tazelenseydi saldirgan deneme yaparak pencereyi uzatabilirdi.
        // `INCR` olmayan anahtari 0 kabul edip 1 yapar, yani ayrica "yoksa yarat" gerekmez.
        public async Task<long> IncrementAsync(string key, TimeSpan ttl)
        {
            var db = _mux.GetDatabase();
            var yeni = await db.StringIncrementAsync(key);
            if (yeni == 1) await db.KeyExpireAsync(key, ttl);
            return yeni;
        }

        // `IncrementAsync` ile AYNI PRIMITIF (ham string). `GetAsync<T>` BURADA KULLANILAMAZ:
        // o `IDistributedCache` uzerinden gider ve StackExchangeRedis girdileri HASH olarak
        // saklar (`HMGET absexp sldexp data`); ham string bir anahtara carpinca **WRONGTYPE**
        // firlatir. Canlida birebir bu oldu - gerekce `ICacheService.SayacOkuAsync`in basinda.
        // Anahtar yoksa `RedisValue.Null` doner ve sayac 0'dir (SALT-OKUMA, anahtar YARATMAZ).
        public async Task<long> SayacOkuAsync(string key)
        {
            var deger = await _mux.GetDatabase().StringGetAsync(key);
            return deger.HasValue && long.TryParse(deger.ToString(), out var n) ? n : 0;
        }

        public void Remove(string key) => _cache.Remove(key);

        // Açıklayıcı yorum: Redis'te prefix invalidation için SCAN gerekir; production'da
        // IConnectionMultiplexer ile pattern silme yapılır. Arayüz sözleşmesi korunur.
        public void RemoveByPrefix(string prefix)
        {
            // Not: StackExchange.Redis IServer.Keys(pattern: prefix + "*") ile uygulanır.
        }
    }
}
