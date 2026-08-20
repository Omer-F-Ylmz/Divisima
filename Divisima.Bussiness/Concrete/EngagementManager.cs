using System;
using System.Collections.Generic;
using System.Linq;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Etkileşim kampanyaları. Tümü izole (bir müşteri hatası diğerlerini durdurmaz),
    // idempotent (tekrar gönderim önleme alanlarıyla), yalnız bildirim izni olanlara (notify_email).
    public class EngagementManager : IEngagementService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IOrderDal _orderDal;
        private readonly IMailService _mailService;
        private readonly IOutboxService _outboxService;

        // Açıklayıcı yorum: Eşikler - üretimde konfigürasyona taşınabilir
        private const int WinBackDaysThreshold = 60;   // 60 gündür sipariş yoksa
        private const int WinBackCooldownDays = 30;    // 30 günde bir win-back (spam önleme)
        private const int ReviewInviteDaysAfterDelivery = 7; // teslimden 7 gün sonra

        private readonly IMarketingGate _marketingGate;

        public EngagementManager(ICustomerDal customerDal, IOrderDal orderDal, IMailService mailService, IOutboxService outboxService,
            IMarketingGate marketingGate)
        {
            _customerDal = customerDal;
            _orderDal = orderDal;
            _mailService = mailService;
            _outboxService = outboxService;
            _marketingGate = marketingGate;
        }

        public async Task<int> SendBirthdayOffers()
        {
            // IYS KAPISI: bu bir TICARI ELEKTRONIK ILETIDIR - bayrak kapaliysa hic taranmaz.
            if (!_marketingGate.Enabled) return 0;
            var today = DateTime.Now;
            // Açıklayıcı yorum: Bugün doğum günü olan, bildirim izinli, bu yıl teklif almamış aktif müşteriler
            var customers = await _customerDal.GetListAsync(c => c.is_active && c.notify_email && c.birthdate.HasValue);
            int sent = 0;
            foreach (var c in customers)
            {
                if (!c.birthdate.HasValue) continue;
                var b = c.birthdate.Value;
                bool isBirthday = b.Day == today.Day && b.Month == today.Month;
                bool alreadySentThisYear = c.birthday_offer_sent_year.HasValue && c.birthday_offer_sent_year.Value.Year == today.Year;
                if (!isBirthday || alreadySentThisYear) continue;
                // IYS: en guncel pazarlama rizasi + tercih (bayrak zaten metot basinda kontrol edildi).
                if (!await _marketingGate.CanSendToCustomerAsync(c.id)) continue;

                try
                {
                    c.birthday_offer_sent_year = today;
                    await _customerDal.UpdateAsync(c);
                    // ISARETLE-SONRA-GONDER (H54): once damgala, sonra outbox'a yaz. Onceki sira (yaz->damgala)
                    // + BOS catch: damgalama hata alirsa mesaj ZATEN yazilmisti -> sonraki calistirmada AYNI
                    // e-posta TEKRAR gonderiliyordu (H42/H45b'deki gonder-sonra-isaretle ailesinin 4. ornegi).
                    // Pazarlama e-postasinda en-fazla-bir-kez, en-az-bir-kez'den iyidir.
                    await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                    {
                        To = c.email,
                        Subject = "Doğum gününüz kutlu olsun! 🎉",
                        Body = $"Merhaba {c.name},\n\nDoğum gününüzü kutlarız! Size özel indirim kodunuz: DOGUMGUNU (bu hafta geçerli).\n\nDivisima",
                        IsHtml = false
                    });
                    sent++;
                }
                catch { }
            }
            return sent;
        }

        public async Task<int> SendWinBackCampaigns()
        {
            // IYS KAPISI: bu bir TICARI ELEKTRONIK ILETIDIR - bayrak kapaliysa hic taranmaz.
            if (!_marketingGate.Enabled) return 0;
            var now = DateTime.Now;
            var cutoff = now.AddDays(-WinBackDaysThreshold);
            var customers = await _customerDal.GetListAsync(c => c.is_active && c.notify_email);
            int sent = 0;
            foreach (var c in customers)
            {
                // Açıklayıcı yorum: Müşterinin en son siparişi (denormalize last_order_at yoksa Order tablosundan)
                var lastOrder = (await _orderDal.GetListNoTrackingAsync(o => o.customer_id == c.id))
                    .OrderByDescending(o => o.created_at).FirstOrDefault();
                if (lastOrder == null) continue; // hiç sipariş vermemiş (bu win-back değil, onboarding)
                if (lastOrder.created_at > cutoff) continue; // yakın zamanda sipariş vermiş

                // IYS: en guncel pazarlama rizasi + tercih (bayrak zaten metot basinda kontrol edildi).
                if (!await _marketingGate.CanSendToCustomerAsync(c.id)) continue;

                // Açıklayıcı yorum: Cooldown - son win-back'ten bu yana yeterli süre geçmiş mi
                if (c.last_winback_sent_at.HasValue && c.last_winback_sent_at.Value > now.AddDays(-WinBackCooldownDays)) continue;

                try
                {
                    c.last_winback_sent_at = now;
                    await _customerDal.UpdateAsync(c);
                    // ISARETLE-SONRA-GONDER (H54): once damgala, sonra outbox'a yaz. Onceki sira (yaz->damgala)
                    // + BOS catch: damgalama hata alirsa mesaj ZATEN yazilmisti -> sonraki calistirmada AYNI
                    // e-posta TEKRAR gonderiliyordu (H42/H45b'deki gonder-sonra-isaretle ailesinin 4. ornegi).
                    // Pazarlama e-postasinda en-fazla-bir-kez, en-az-bir-kez'den iyidir.
                    await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                    {
                        To = c.email,
                        Subject = "Sizi özledik! 💜",
                        Body = $"Merhaba {c.name},\n\nUzun zamandır görüşemedik. Yeni koleksiyonumuza göz atın; size özel GERIDÖN koduyla indirim sizi bekliyor.\n\nDivisima",
                        IsHtml = false
                    });
                    sent++;
                }
                catch { }
            }
            return sent;
        }

        public async Task<int> SendReviewInvites()
        {
            // IYS KAPISI: bu bir TICARI ELEKTRONIK ILETIDIR - bayrak kapaliysa hic taranmaz.
            if (!_marketingGate.Enabled) return 0;
            var now = DateTime.Now;
            var target = now.AddDays(-ReviewInviteDaysAfterDelivery);
            // Açıklayıcı yorum: Teslim edilmiş, davet gönderilmemiş, teslim tarihi ~N gün önce olan siparişler
            var orders = await _orderDal.GetListAsync(o =>
                o.status == (byte)Divisima.Core.Utilities.Enums.OrderStatusEnum.Delivered
                && o.review_invite_sent_at == null);
            int sent = 0;
            foreach (var o in orders)
            {
                // DERLEME + MANTIK FIX (H44): Order entity'sinde updated_at YOK (CS1061 -> build patlardı).
                // Doğru alan zaten var: delivered_at (teslim anında damgalanır). Semantik olarak da doğrusu bu -
                // updated_at herhangi bir güncellemede değişirdi, delivered_at ise SADECE teslim zamanıdır.
                var deliveredAt = o.delivered_at ?? o.created_at;
                if (deliveredAt > target) continue; // daha N gün olmamış

                var customer = await _customerDal.GetAsync(c => c.id == o.customer_id && c.is_active);
                // IYS: notify_email tercihine EK OLARAK en guncel pazarlama rizasi da sorulur.
                // Yorum daveti ticari elektronik iletidir - siparise bagli olmasi onu islemsel yapmaz.
                if (customer == null || !customer.notify_email
                    || !await _marketingGate.CanSendToCustomerAsync(customer.id))
                {
                    // Açıklayıcı yorum: İzin yoksa yine damgala (tekrar taranmasın)
                    o.review_invite_sent_at = now;
                    await _orderDal.UpdateAsync(o);
                    continue;
                }

                try
                {
                    await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                    {
                        To = customer.email,
                        Subject = "Siparişinizi değerlendirin ⭐",
                        Body = $"Merhaba {customer.name},\n\n#{o.id} numaralı siparişiniz nasıldı? Deneyiminizi diğer müşterilerle paylaşın, yorumunuz bizim için değerli.\n\nDivisima",
                        IsHtml = false
                    });
                    o.review_invite_sent_at = now;
                    await _orderDal.UpdateAsync(o);
                    sent++;
                }
                catch { }
            }
            return sent;
        }
    }
}
