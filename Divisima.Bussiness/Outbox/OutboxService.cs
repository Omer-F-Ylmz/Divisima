using System.Text.Json;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Outbox
{
    // Açıklayıcı yorum: Event'i JSON'a çevirip OutboxMessage olarak kaydeder (status=Pending).
    // Sipariş transaction'ı içinde çağrıldığından, sipariş commit olursa event de kalıcı olur (atomik).
    public class OutboxService : IOutboxService
    {
        private readonly IOutboxMessageDal _outboxDal;

        public OutboxService(IOutboxMessageDal outboxDal)
        {
            _outboxDal = outboxDal;
        }

        public async Task WriteAsync(string eventType, object payload)
        {
            await _outboxDal.AddAsync(new OutboxMessage
            {
                event_type = eventType,
                payload = JsonSerializer.Serialize(payload),
                status = 0, // Pending
                retry_count = 0,
                created_at = DateTime.Now
            });
        }
    }
}
