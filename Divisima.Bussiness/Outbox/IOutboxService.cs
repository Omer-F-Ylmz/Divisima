namespace Divisima.Bussiness.Outbox
{
    // Açıklayıcı yorum: Event'i outbox tablosuna yazar (sipariş transaction'ı içinde çağrılır).
    public interface IOutboxService
    {
        Task WriteAsync(string eventType, object payload);
    }
}
