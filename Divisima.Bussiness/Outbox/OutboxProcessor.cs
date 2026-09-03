using System.Text.Json;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Events;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Outbox
{
    // Açıklayıcı yorum: Bekleyen outbox mesajlarını işler (Hangfire recurring job çağırır).
    // Her mesajı ilgili event publisher'a yönlendirir; başarılıysa Processed, hata olursa retry_count++.
    public class OutboxProcessor
    {
        private readonly IOutboxMessageDal _outboxDal;
        private readonly IOrderPlacedEventPublisher _orderPlacedPublisher;
        private readonly IMailService _mailService;
        // Kalici basarisizlik SESSIZ kalmasin: zaman cizelgesine "KRITIK" notu dusulur (H53 kalibi).
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly ILogger<OutboxProcessor> _logger;

        // SPRINT 8 MADDE 3 - MESAJ BASINA AYRI DI SCOPE. GEREKCESI OLCULDU, TAHMIN DEGIL:
        //
        // Isleyici ve tum bagimliliklari TEK bir scope'ta (dolayisiyla TEK DbContext'te) kosuyordu.
        // Bir yan etki adimi SaveChanges sirasinda patladiginda (or. UX_loyalty_transactions_order_earn
        // ihlali - at-least-once teslimatta BEKLENEN bir durum) basarisiz varlik change tracker'da
        // "Added" halinde KALIYOR. Hemen ardindan gelen `_outboxDal.UpdateAsync(msg)` cagrisi ayni
        // context uzerinde SaveChanges yapinca o BEKLEYEN varligi TEKRAR yazmaya calisiyor ve AYNI
        // hatayla patliyor - bu kez OUTBOX'IN KENDI KAYIT YAZIMINDA.
        // OLCULEN ZARAR: istisna dongunun DISINA cikiyor; mesajin retry_count'u HIC KAYDEDILMIYOR
        // ve ayni parti sonsuza kadar yeniden isleniyor. Ustelik ayni turdaki DIGER mesajlar da
        // hic islenmiyor.
        // COZUM: her mesaj KENDI child scope'unda islenir. Zehirlenen context o scope ile birlikte
        // atilir; outbox'in kendi defter yazimi TEMIZ context'te kalir ve mesajlar birbirini
        // ETKILEMEZ.
        private readonly IServiceScopeFactory _scopeFactory;

        public OutboxProcessor(IOutboxMessageDal outboxDal, IOrderPlacedEventPublisher orderPlacedPublisher, IMailService mailService,
            IOrderStatusHistoryService statusHistory, ILogger<OutboxProcessor> logger, IServiceScopeFactory scopeFactory)
        {
            _outboxDal = outboxDal;
            _orderPlacedPublisher = orderPlacedPublisher;
            _mailService = mailService;
            _statusHistory = statusHistory;
            _logger = logger;
            _scopeFactory = scopeFactory;
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
                        // SPRINT 8 MADDE 3 - ODEME ONAYI YAN ETKILERI.
                        // Dort adim (fatura, sadakat, referans odulu, kupon sayaci) burada kosar.
                        // AT-LEAST-ONCE: mesaj birden fazla teslim edilebilir; dordu de idempotent
                        // (dayanaklari IPaymentConfirmedSideEffects yorumunda tek tek yazili).
                        case "PaymentConfirmed":
                            var odeme = JsonSerializer.Deserialize<Divisima.Bussiness.Events.PaymentConfirmedEvent>(msg.payload);
                            if (odeme != null)
                            {
                                // AYRI SCOPE: zehirlenen context bu blokla birlikte atilir
                                // (gerekce yukarida, _scopeFactory alanında).
                                using var scope = _scopeFactory.CreateScope();
                                await scope.ServiceProvider
                                    .GetRequiredService<Divisima.Bussiness.Events.IPaymentConfirmedSideEffects>()
                                    .ApplyAsync(odeme);
                            }
                            break;
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

                    // SPRINT 8 MADDE 3 - KALICI BASARISIZLIK SESSIZ KALMAZ (H53 kalibi).
                    // Yeniden deneme hakki bittiginde mesaj Failed olur. Onceden bu yalniz
                    // outbox_messages tablosunda bir satirdi - kimse bakmazsa GORUNMEZDI.
                    // Artik hem log hem SIPARIS ZAMAN CIZELGESI: operator panelde gorur.
                    if (msg.status == 2)
                        await KaliciHataylaBirakAsync(msg, ex);
                }
                await _outboxDal.UpdateAsync(msg);
            }
        }

        // SPRINT 8 MADDE 3 - kalici basarisizligi GORUNUR kilar.
        // OrderManager'daki "KRITIK: para iadesi BASARISIZ" ve S7'deki "UYARI: '<adim>' adimi
        // basarisiz" notlariyla AYNI kalip: operator paneldeki siparis zaman cizelgesinde gorur.
        // Not yazmanin kendisi patlarsa log TEK kanal olarak kalir - istisna yukari sizip
        // outbox dongusunu KIRMAZ (diger mesajlar islenmeye devam etmeli).
        private async Task KaliciHataylaBirakAsync(OutboxMessage msg, Exception ex)
        {
            // GF-3/K2: ONCEDEN hem `ex` NESNESI hem ham `ex.Message` yaziliyordu. Ayni metin
            // `DashboardManager:241`'de ZATEN maskeden geciyordu - yani depo ayni degeri bir
            // yerde maskeleyip bir yerde HAM yaziyordu (ASIMETRI, GF-3 on olcum A).
            // Istisna NESNESI de gecilmiyor: Serilog'un {Exception} alani ex.ToString()'i HAM
            // yazar ve mail gonderim istisnalari ALICI ADRESINI tasir. Yigin izi kaybolmuyor,
            // maskeden gecirilip metne konuyor (olculdu: 113 gercek yigin satirinda maskelenen
            // parca YALNIZ 5 ve besi de derleyici uretimi ad).
            _logger.LogError(
                "OUTBOX KALICI HATA. id={Id} tip={Tip} deneme={Deneme} hata={Hata}",
                msg.id, msg.event_type, msg.retry_count,
                Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(ex.ToString()));

            try
            {
                // LAUNCH-FIX A1(b): "OrderPlaced" da artik uretimde kullaniliyor (siparis onay maili
                // + admin bildirimi bu mesajla tasiniyor). Kalici basarisizligi PaymentConfirmed ile
                // AYNI kanaldan gorunur kilinir - aksi halde "musteriye onay maili hic gitmedi"
                // durumu yalniz log satirinda kalirdi ve panelde IZI OLMAZDI.
                if (msg.event_type == "OrderPlaced")
                {
                    var siparis = JsonSerializer.Deserialize<Divisima.Bussiness.Events.OrderPlacedEvent>(msg.payload);
                    if (siparis == null || siparis.order_id <= 0) return;
                    // Durum olarak Pending kullaniliyor: bu not "Sipariş oluşturuldu" ANINA aittir
                    // (OrderManager'daki o satir da Pending yaziyor), yeni bir gecis DEGILDIR.
                    await _statusHistory.RecordAsync(siparis.order_id, (byte)OrderStatusEnum.Pending,
                        $"KRITIK: sipariş bildirimleri {msg.retry_count} denemede tamamlanamadı " +
                        $"(onay e-postası/admin bildirimi). Son hata: {ex.Message}");
                    return;
                }

                if (msg.event_type != "PaymentConfirmed") return;
                var evt = JsonSerializer.Deserialize<Divisima.Bussiness.Events.PaymentConfirmedEvent>(msg.payload);
                if (evt == null || evt.order_id <= 0) return;
                await _statusHistory.RecordAsync(evt.order_id, (byte)OrderStatusEnum.Confirmed,
                    $"KRITIK: ödeme sonrası yan etkiler {msg.retry_count} denemede tamamlanamadı " +
                    $"(fatura/puan/ödül/kupon sayacı). Son hata: {ex.Message}");
            }
            catch (Exception izEx)
            {
                _logger.LogError(izEx, "OUTBOX kalici hata notu zaman cizelgesine yazilamadi. id={Id}", msg.id);
            }
        }
    }
}
