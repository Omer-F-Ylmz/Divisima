using System.Text.Json;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Events;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;

namespace Divisima.Bussiness.Outbox
{
    // Açıklayıcı yorum: Bekleyen outbox mesajlarını işler (Hangfire recurring job çağırır).
    // Her mesajı ilgili event publisher'a yönlendirir; başarılıysa Processed, hata olursa retry_count++.
    public class OutboxProcessor
    {
        private readonly IOutboxMessageDal _outboxDal;
        private readonly IOrderPlacedEventPublisher _orderPlacedPublisher;
        private readonly IMailService _mailService;

        public OutboxProcessor(IOutboxMessageDal outboxDal, IOrderPlacedEventPublisher orderPlacedPublisher, IMailService mailService)
        {
            _outboxDal = outboxDal;
            _orderPlacedPublisher = orderPlacedPublisher;
            _mailService = mailService;
        }

        // Açıklayıcı yorum: Bekleyen mesajları (max 50) işle
        public async Task ProcessPendingAsync()
        {
            // Açıklayıcı yorum: CRASH KURTARMA - önceki bir çalışma yarıda kaldıysa (Processing + 5dk'dan eski)
            // mesajları yeniden Pending yap ki teslim edilebilsinler (processor çökerse mesaj takılı kalmasın).
            await _outboxDal.ReclaimStaleAsync(DateTime.Now.AddMinutes(-5));

            var messages = await _outboxDal.GetPendingAsync(50);
            foreach (var msg in messages)
            {
                // Açıklayıcı yorum: ATOMİK CLAIM - mesajı Pending->Processing geçir. İki processor instance (yatay ölçekleme
                // veya job overlap) AYNI mesajı işleyemez: yalnız biri claim=1 alır, diğeri 0 -> SKIP. Çift teslim ENGELİ.
                var claimed = await _outboxDal.TryClaimAsync(msg.id);
                if (claimed == 0) continue;   // başka instance zaten aldı

                try
                {
                    // Açıklayıcı yorum: Event tipine göre yönlendir (yeni event tipleri buraya eklenir)
                    switch (msg.event_type)
                    {
                        case "OrderPlaced":
                            var evt = JsonSerializer.Deserialize<OrderPlacedEvent>(msg.payload);
                            await _orderPlacedPublisher.PublishAsync(evt);
                            break;
                        // Açıklayıcı yorum: D4 - Engagement e-postaları outbox üzerinden (retry + Failed durumu ile dayanıklı)
                        case "EmailNotification":
                            var mail = JsonSerializer.Deserialize<MailMessageDto>(msg.payload);
                            if (mail != null) await _mailService.SendAsync(mail);
                            break;
                    }
                    msg.status = 1; // Processed
                    msg.processed_at = DateTime.Now;
                    msg.error = null;
                }
                catch (Exception ex)
                {
                    // Açıklayıcı yorum: Hata - retry sayacını artır, 5'te kalıcı hata (status=Failed), aksi halde
                    // yeniden Pending (0) yap ki sonraki çalışmada tekrar denensin (Processing'de takılı kalmasın).
                    msg.retry_count += 1;
                    msg.error = ex.Message;
                    msg.status = msg.retry_count >= 5 ? (byte)2 : (byte)0; // Failed : Pending (retry)
                    msg.processed_at = null;
                }
                await _outboxDal.UpdateAsync(msg);
            }
        }
    }
}
