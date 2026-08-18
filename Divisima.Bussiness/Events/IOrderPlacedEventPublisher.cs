namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Sipariş event publisher (Cafixo IWebOrderPaidEventPublisher kalıbı).
    public interface IOrderPlacedEventPublisher
    {
        Task PublishAsync(OrderPlacedEvent @event);
    }
}
