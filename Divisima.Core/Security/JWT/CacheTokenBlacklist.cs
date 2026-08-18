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

        public async Task RevokeAsync(string jti, DateTime expiresAt)
        {
            if (string.IsNullOrEmpty(jti)) return;
            var ttl = expiresAt - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero) return; // zaten süresi dolmuş
            await _cache.GetOrSetAsync(Key(jti), () => Task.FromResult(true), ttl);
        }

        public async Task<bool> IsRevokedAsync(string jti)
        {
            if (string.IsNullOrEmpty(jti)) return false;
            // Açıklayıcı yorum: Varsa (true) kara listede demektir; yoksa factory false döner
            return await _cache.GetOrSetAsync(Key(jti), () => Task.FromResult(false), TimeSpan.FromMinutes(1));
        }
    }
}
