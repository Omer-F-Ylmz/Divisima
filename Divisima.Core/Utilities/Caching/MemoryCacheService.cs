using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Divisima.Core.Utilities.Caching
{
    // Açıklayıcı yorum: IMemoryCache tabanlı cache (tek sunucu). Prefix invalidation için key takibi tutar.
    // Çok sunuculu ortamda IDistributedCache/Redis implementasyonu ile değiştirilir (arayüz aynı kalır).
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, byte> _keys = new();
        private static readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(10);
        private static readonly object _addLock = new();
        // STAMPEDE KORUMASI (H49): anahtar basina kapi. Cache bosaldigi anda gelen N es zamanli istek
        // AYNI agir hesabi N kez calistiriyordu (ComputeBestSellers tum order_items'i belleğe cekiyor).
        // Ozellikle H47'de eklenen invalidation ile her admin urun duzenlemesinde tetiklenir hale gelmisti.
        // Anahtar uzayi kucuk ve sinirli (merch:*:{take}, take 1..50) -> sozluk kontrolsuz buyumez.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
        // KILITLENME KORUMASI (H50): kapida SINIRSIZ beklemek, factory takilirsa (DB donmasi) o anahtara gelen
        // TUM istekleri sonsuza kadar bloke eder -> thread havuzu tukenir -> yavas uc yerine TAM KESINTI.
        // Sinirli bekleme: sure dolarsa cagri kendi hesabini yapar (nadir fazladan hesap) - erisilebilirlik korunur.
        private static readonly TimeSpan GateWaitSeconds = TimeSpan.FromSeconds(5);

        // BELLEK SIZINTISI FIX (H49): _keys'e eklenen anahtarlar TTL ile suresi dolunca SILINMIYORDU -
        // yalniz acik Remove/RemoveByPrefix temizliyordu. IdempotencyFilter her istekte BENZERSIZ anahtar
        // uretir (idem:{method}:{path}:{client-key}) -> static sozluk sinirsiz buyur (uzun sureli process'te
        // bellek sizintisi) + RemoveByPrefix her cagrida TUM gecmisi tarar (giderek yavaslar).
        // Cozum: tahliye geri-cagrisi - girdi cache'ten dustugu anda anahtar da takipten dusulur.
        private static MemoryCacheEntryOptions TrackedOptions(TimeSpan ttl) =>
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }
                .RegisterPostEvictionCallback((k, v, reason, state) =>
                {
                    if (k is string sk) _keys.TryRemove(sk, out _);
                });

        public MemoryCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        // Açıklayıcı yorum: Cache-aside - varsa cache'ten, yoksa factory'den üret + cache'le
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
        {
            if (_cache.TryGetValue(key, out T cached))
                return cached;

            // STAMPEDE KORUMASI: yalniz BIR cagri hesaplar, digerleri bekleyip hazir sonucu alir.
            var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            var entered = await gate.WaitAsync(GateWaitSeconds);
            if (!entered)
            {
                // Kapi zamaninda acilmadi -> bloke olmak yerine kendi hesabimizi yap (erisilebilirlik > tek-hesap).
                var fallback = await factory();
                _cache.Set(key, fallback, TrackedOptions(ttl ?? _defaultTtl));
                _keys.TryAdd(key, 0);
                return fallback;
            }
            try
            {
                // CIFT KONTROL: biz beklerken baska bir cagri doldurmus olabilir.
                if (_cache.TryGetValue(key, out T filled))
                    return filled;

                var value = await factory();
                _cache.Set(key, value, TrackedOptions(ttl ?? _defaultTtl));
                _keys.TryAdd(key, 0);
                return value;
            }
            finally
            {
                gate.Release();
                // Bekleyen kalmadiysa kapiyi birak (bellek sizintisi olmasin). En kotu ihtimalle
                // nadir bir yaris sonucu iki kapi olusur = fazladan TEK bir hesap; dogruluk bozulmaz.
                if (gate.CurrentCount == 1) _gates.TryRemove(key, out _);
            }
        }

        // Aciklayici yorum: ATOMIK set-if-not-exists (lock ile tek-process). check-then-set bolunmez -> race yok.
        public Task<bool> TryAddAsync(string key, TimeSpan ttl)
        {
            lock (_addLock)
            {
                if (_cache.TryGetValue(key, out _))
                    return Task.FromResult(false);   // zaten var (baska istek ekledi)
                _cache.Set(key, true, TrackedOptions(ttl));
                _keys.TryAdd(key, 0);
                return Task.FromResult(true);         // BU cagri ekledi
            }
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        // Açıklayıcı yorum: Yazma işleminde ilgili tüm cache'i temizle (ör. ürün eklenince "product:*")
        public void RemoveByPrefix(string prefix)
        {
            foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix)).ToList())
            {
                _cache.Remove(key);
                _keys.TryRemove(key, out _);
            }
        }
    }
}
