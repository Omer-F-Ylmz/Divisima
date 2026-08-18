namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Event'i tüm kayıtlı handler'lara dağıtır (Autofac IEnumerable<IHandler> resolve).
    // Cafixo WebOrderPaidEventPublisher kalıbı.
    public class OrderPlacedEventPublisher : IOrderPlacedEventPublisher
    {
        private readonly IEnumerable<IOrderPlacedEventHandler> _handlers;

        public OrderPlacedEventPublisher(IEnumerable<IOrderPlacedEventHandler> handlers)
        {
            _handlers = handlers;
        }

        public async Task PublishAsync(OrderPlacedEvent @event)
        {
            // Açıklayıcı yorum: Her handler'ı sırayla çalıştır (mail, log, bildirim...)
            foreach (var handler in _handlers)
            {
                await handler.HandleAsync(@event);
            }
        }
    }
}
