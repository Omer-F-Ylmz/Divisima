namespace Divisima.Bussiness.Events
{
    // Açıklayıcı yorum: Sipariş sonrası loglama handler'ı (Cafixo WebOrderPaidLogHandler kalıbı).
    // Faz 2: OrderPlacedEmailHandler (sipariş onay maili) + OrderPlacedNotificationHandler (SignalR) eklenecek.
    public class OrderPlacedLogHandler : IOrderPlacedEventHandler
    {
        public Task HandleAsync(OrderPlacedEvent @event)
        {
            // Açıklayıcı yorum: Sipariş oluşturma logu (gerçek projede ILogService ile Mongo'ya)
            Console.WriteLine($"[SİPARİŞ] #{@event.order_number} oluşturuldu. Müşteri: {@event.customer_id}, Tutar: {@event.total}");
            return Task.CompletedTask;
        }
    }
}
