using System.Collections.Concurrent;

namespace Divisima.Core.Utilities.Locking
{
    // Açıklayıcı yorum: Tek-sunucu kilit (SemaphoreSlim). Çok sunuculu ortamda RedisDistributedLock ile değiştirilir
    // (RedLock algoritması) - arayüz aynı kalır. Kritik ödeme bölümünü serialize eder.
    public class InMemoryDistributedLock : IDistributedLock
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public async Task<IDisposable> AcquireAsync(string key, TimeSpan expiry)
        {
            var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            // Açıklayıcı yorum: Kilidi bekle (expiry kadar); alınamazsa null (kaynak meşgul)
            if (!await sem.WaitAsync(expiry))
                return null;
            return new Releaser(sem);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _sem;
            public Releaser(SemaphoreSlim sem) => _sem = sem;
            public void Dispose() => _sem.Release();
        }
    }
}
