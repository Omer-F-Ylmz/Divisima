namespace Divisima.Core.Utilities.Caching
{
    // Açıklayıcı yorum: Cache soyutlaması (IMemoryCache -> ileride Redis'e geçiş kolay olsun diye).
    // Cache-aside deseni: GetOrSetAsync ile "önce cache, yoksa üret+yaz".
    public interface ICacheService
    {
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null);
        // Aciklayici yorum: ATOMIK set-if-not-exists (Redis SETNX / in-memory lock). true = BU cagri anahtari ekledi
        // (yoktu); false = zaten vardi. "Yalniz ilk kazanir" senaryolari (idempotency, kilit) icin - check-then-act race YOK.
        Task<bool> TryAddAsync(string key, TimeSpan ttl);
        void Remove(string key);
        void RemoveByPrefix(string prefix);
    }
}
