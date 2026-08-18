namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Sipariş event handler arayüzü. Tüm implementasyonlar publisher tarafından çağrılır.
    public interface IOrderPlacedEventHandler
    {
        Task HandleAsync(OrderPlacedEvent @event);
    }
}
