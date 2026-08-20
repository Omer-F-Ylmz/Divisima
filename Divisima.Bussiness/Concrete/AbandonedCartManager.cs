using System;
using System.Collections.Generic;
using System.Linq;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Terk edilmiş sepet iş kuralları. Atıl + dolu + hatırlatılmamış sepetlere tek e-posta.
    public class AbandonedCartManager : IAbandonedCartService
    {
        private readonly ICartDal _cartDal;
        private readonly ICartItemDal _cartItemDal;
        private readonly ICustomerDal _customerDal;
        private readonly IMailService _mailService;

        // Açıklayıcı yorum: Sepet en az bu kadar süredir atılsa hatırlatma gönderilir
        private static readonly TimeSpan IdleThreshold = TimeSpan.FromHours(24);

        private readonly IMarketingGate _marketingGate;

        public AbandonedCartManager(ICartDal cartDal, ICartItemDal cartItemDal, ICustomerDal customerDal, IMailService mailService,
            IMarketingGate marketingGate)
        {
            _cartDal = cartDal;
            _cartItemDal = cartItemDal;
            _customerDal = customerDal;
            _mailService = mailService;
            _marketingGate = marketingGate;
        }

        public async Task<int> SendReminders()
        {
            // İYS KAPISI: terk-sepet hatırlatması TİCARİ ELEKTRONİK İLETİDİR. Bayrak kapalıysa
            // hiç aday taranmaz - job boşuna DB gezmez.
            if (!_marketingGate.Enabled) return 0;

            var cutoff = DateTime.Now - IdleThreshold;

            // Açıklayıcı yorum: Aktif + hatırlatma gönderilmemiş + atıl (son hareket cutoff'tan eski) sepetler.
            // Son hareket = updated_at (yoksa created_at).
            var candidates = await _cartDal.GetListNoTrackingAsync(c =>
                c.is_active
                && c.reminder_sent_at == null
                && ((c.updated_at != null && c.updated_at < cutoff) || (c.updated_at == null && c.created_at < cutoff)));

            if (candidates.Count == 0) return 0;

            int sent = 0;
            foreach (var cart in candidates)
            {
                // Açıklayıcı yorum: Sepette aktif ürün var mı (boş sepete hatırlatma gönderme)
                // PERFORMANS (H51): EXISTS - her sepet icin kalemleri cekmek yerine "bos mu" sor (job N sepet gezer).
                var hasItems = await _cartItemDal.AnyAsync(i => i.cart_id == cart.id && i.is_active);
                if (!hasItems) continue;

                var customer = await _customerDal.GetAsync(c => c.id == cart.customer_id);
                if (customer == null || string.IsNullOrWhiteSpace(customer.email)) continue;

                // İYS: rıza + tercih kontrolü. ÖNCEDEN YOKTU - bu job yalnız is_active bakıyordu,
                // yani pazarlama rızası VERMEMİŞ ve notify_email'i KAPALI müşterilere de
                // hatırlatma gidiyordu. Claim'den ÖNCE sorulur: izin yoksa reminder_sent_at
                // damgalanmasın, kişi izin verirse ileride gönderilebilsin.
                if (!await _marketingGate.CanSendToCustomerAsync(customer.id)) continue;

                // CONCURRENCY FIX (H45b): ATOMİK claim ÖNCE - reminder_sent_at NULL ise damgala, kazanan gönderir.
                // Öncesinde "gönder -> sonra damgala" sırası vardı: eşzamanlı iki job (veya gönderim sonrası crash +
                // retry) AYNI müşteriye ÇİFT hatırlatma maili atardı. (H42'de StockNotification/PriceDrop için
                // uygulanan desenin aynısı; bu manager o turda gözden kaçmıştı.)
                if (!await _cartDal.TryClaimReminderAsync(cart.id))
                    continue;   // başka bir çalıştırma zaten aldı + gönderdi

                try
                {
                    await _mailService.SendAsync(new MailMessageDto
                    {
                        To = customer.email,
                        Subject = "Sepetinizi unuttunuz mu?",
                        Body = "Merhaba,\n\nSepetinizde ürünler sizi bekliyor. Tükenmeden siparişinizi tamamlayabilirsiniz.\n\nDivisima",
                        IsHtml = false
                    });
                    sent++;
                }
                catch
                {
                    // Gönderim başarısız -> claim'i geri al (sonraki çalıştırma tekrar denesin).
                    await _cartDal.ResetReminderClaimAsync(cart.id);
                }
            }
            return sent;
        }
    }
}
