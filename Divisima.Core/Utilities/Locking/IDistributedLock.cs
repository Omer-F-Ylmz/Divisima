namespace Divisima.Core.Utilities.Locking
{
    // Açıklayıcı yorum: Dağıtık kilit soyutlaması. Aynı kaynağa (ör. sipariş) eşzamanlı kritik işlemi engeller.
    // Örn: aynı siparişe iki paralel ödeme callback'i -> sadece biri işler.
    public interface IDistributedLock
    {
        Task<IDisposable> AcquireAsync(string key, TimeSpan expiry);
    }
}
