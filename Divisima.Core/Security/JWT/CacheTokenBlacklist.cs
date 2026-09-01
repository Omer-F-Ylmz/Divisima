using Divisima.Core.Utilities.Caching;

namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: ICacheService tabanlı blacklist. TTL = token'ın kalan ömrü (süre dolunca otomatik silinir).
    // Tek sunucuda IMemoryCache, çok sunucuda Redis - aynı arayüz.
    public class CacheTokenBlacklist : ITokenBlacklist
    {
        private readonly ICacheService _cache;
        public CacheTokenBlacklist(ICacheService cache) => _cache = cache;

        private static string Key(string jti) => $"revoked-jti:{jti}";

        // ══ GF-1 / K2 - KARA LISTE KENDINI ZEHIRLIYORDU (OLCULDU) ═════════════════════════
        //
        // ESKI HAL: iki metot da `GetOrSetAsync` cagiriyordu.
        //   `IsRevokedAsync` -> anahtar yoksa factory (`false`) kosar ve SONUCU CACHE'E YAZAR.
        //   `RevokeAsync`    -> ayni `GetOrSetAsync`; anahtar DOLU oldugu icin cached `false`
        //                       doner, factory HIC KOSMAZ, `true` HIC YAZILMAZ.
        // Middleware her kimlikli istekte `IsRevokedAsync` cagirdigi icin AKTIF bir jetonun
        // anahtari pratikte SUREKLI `false` ile zehirliydi: yazma tarafi baglansa BILE iptal
        // 60 saniyeye kadar SESSIZ NO-OP olurdu.
        //
        // URETIM DALI DA AYNIYDI: kusur `MemoryCacheService`e ozgu degil - `RedisCacheService`
        // de ayni cache-aside desenini uyguluyor, yani Redis acikken de gecerliydi.
        //
        // YENI HAL: okuma `ExistsAsync` (SALT-OKUMA, anahtar olusturmaz), yazma `TryAddAsync`
        // (atomik set-if-not-exists). Okuma artik yazma URETMEDIGI icin anahtar YALNIZCA
        // gercekten iptal edildiginde var olur ve `TryAddAsync`in "zaten vardi" dali da
        // DOGRU anlama gelir (ayni jeton iki kez iptal edilmis).
        public async Task RevokeAsync(string jti, DateTime expiresAt)
        {
            if (string.IsNullOrEmpty(jti)) return;
            var ttl = expiresAt - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero) return; // zaten süresi dolmuş
            await _cache.TryAddAsync(Key(jti), ttl);
        }

        public async Task<bool> IsRevokedAsync(string jti)
        {
            if (string.IsNullOrEmpty(jti)) return false;
            // Anahtarin VARLIGI iptal demektir. Hicbir sey YAZILMAZ.
            return await _cache.ExistsAsync(Key(jti));
        }
    }
}
