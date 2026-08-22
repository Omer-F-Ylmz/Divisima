using System;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Notification;
using Divisima.Entity.Dtos.PriceDrop;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Configuration;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Fiyat düşüş bildirimi. Abone olunan fiyatın altına inince e-posta (StockNotification kardeşi).
    public class PriceDropManager : IPriceDropService
    {
        private readonly IPriceDropSubscriptionDal _subDal;
        private readonly IProductDal _productDal;
        private readonly IMailService _mailService;

        private readonly IMarketingGate _marketingGate;
        private readonly IConfiguration _config;

        public PriceDropManager(IPriceDropSubscriptionDal subDal, IProductDal productDal, IMailService mailService,
            IMarketingGate marketingGate, IConfiguration config)
        {
            _subDal = subDal;
            _productDal = productDal;
            _mailService = mailService;
            _marketingGate = marketingGate;
            _config = config;
        }

        public async Task<(HttpStatusCode, Result)> Subscribe(PriceDropSubscribeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.email) || !dto.email.Contains("@"))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidEmail));

            var product = await _productDal.GetAsync(p => p.id == dto.product_id && p.is_active);
            if (product == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // B1: e-posta KIMLIK dizgesidir - kanonik saklanir (gerekce StockNotificationManager).
            var eposta = dto.email.Trim().ToLowerInvariant();

            // Açıklayıcı yorum: Aynı e-posta+ürün için bekleyen abonelik varsa fiyatı güncelle (idempotent)
            var existing = await _subDal.GetAsync(s => s.product_id == dto.product_id && s.email == eposta && !s.is_notified);
            if (existing != null)
            {
                existing.subscribed_price = product.price;
                await _subDal.UpdateAsync(existing);
                return (HttpStatusCode.OK, new SuccessResult(Messages.PriceDropAlreadySubscribed));
            }

            await _subDal.AddAsync(new PriceDropSubscription
            {
                product_id = dto.product_id,
                email = eposta,
                subscribed_price = product.price,
                is_notified = false,
                created_at = DateTime.Now,
                unsubscribe_token = Divisima.Core.Utilities.Security.UnsubscribeToken.Yeni()
            });
            return (HttpStatusCode.OK, new SuccessResult(Messages.PriceDropSubscribed));
        }

        public async Task NotifyPriceDrop(int productId, decimal newPrice)
        {
            // Açıklayıcı yorum: Yeni fiyat, abone olunan fiyatın ALTINDA olan bekleyen abonelikler (no-tracking, salt-okuma)
            var pending = await _subDal.GetListNoTrackingAsync(s => s.product_id == productId && !s.is_notified && s.subscribed_price > newPrice);
            if (pending == null || pending.Count == 0) return;

            var product = await _productDal.GetAsync(p => p.id == productId);
            var productName = product?.name ?? "Ürün";

            foreach (var sub in pending)
            {
                // İYS: fiyat düşüşü bildirimi ticari elektronik iletidir. Claim'den ÖNCE sorulur -
                // izin yoksa kayıt "bildirildi" damgalanmasın, kişi izin verirse ileride gidebilsin.
                if (!await _marketingGate.CanSendToEmailAsync(sub.email)) continue;

                // CONCURRENCY FIX (H42): ATOMİK claim ÖNCE - eşzamanlı iki fiyat-güncelleme aynı aboneye ÇİFT mail atmasın.
                if (!await _subDal.TryClaimForNotificationAsync(sub.id))
                    continue; // başka bir çağrı zaten aldı + gönderdi
                try
                {
                    await _mailService.SendAsync(new MailMessageDto
                    {
                        To = sub.email,
                        Subject = "Fiyat düştü! " + productName,
                        Body = $"Merhaba,\n\nİlgilendiğiniz \"{productName}\" ürününün fiyatı {newPrice:N2} TL'ye düştü " +
                               $"(önceki takip fiyatınız {sub.subscribed_price:N2} TL). Kaçırmayın!\n\nDivisima" +
                               AbonelikCikisMetni("/api/price-drop/unsubscribe", sub.unsubscribe_token),
                        IsHtml = false
                    });
                }
                catch
                {
                    // Gönderim başarısız -> claim'i geri al (tekrar denenebilsin).
                    await _subDal.ResetNotificationClaimAsync(sub.id);
                }
            }
        }

        // ── SPRINT 8 MADDE 10 - ABONELIK YONETIMI ──────────────────────────────────
        //
        // OLCULEN BOSLUK: backend'de YALNIZ "subscribe" vardi. Kullanici kurdugu bildirimi ne
        // gorebiliyor ne kapatabiliyordu; e-postada da bir "abonelikten cik" baglantisi yoktu.
        // Ticari elektronik ileti icin izin GERI ALINABILIR olmali - bu yalniz bir kolaylik degil.

        public async Task<(HttpStatusCode, Result)> GetMine(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidEmail));

            // B1: sahiplik KIMLIK esitligidir - terim de kanonik bicime cevrilir.
            email = email.Trim().ToLowerInvariant();

            // Salt-okuma: izlemeye almaya gerek yok (EfEntityRepositoryBase.GetAsync TRACKED'dir).
            var rows = await _subDal.GetListNoTrackingAsync(n => n.email == email);
            if (rows == null || rows.Count == 0)
                return (HttpStatusCode.OK, new SuccessDataResult<List<NotificationSubscriptionDto>>(new List<NotificationSubscriptionDto>()));

            // Urun adlari TEK sorguda cozulur - satir basina cagri N+1 olurdu.
            var ids = rows.Select(r => r.product_id).Distinct().ToList();
            var adlar = (await _productDal.GetListNoTrackingAsync(p => ids.Contains(p.id)))
                .ToDictionary(p => p.id, p => p.name);

            var liste = rows
                .OrderByDescending(r => r.created_at)
                .Select(r => new NotificationSubscriptionDto
                {
                    id = r.id,
                    type = "price_drop",
                    product_id = r.product_id,
                    product_name = adlar.TryGetValue(r.product_id, out var ad) ? ad : null,
                    subscribed_price = r.subscribed_price,
                    is_notified = r.is_notified,
                    created_at = r.created_at,
                    notified_at = r.notified_at
                })
                .ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<NotificationSubscriptionDto>>(liste));
        }

        public async Task<(HttpStatusCode, Result)> RemoveMine(int id, string email)
        {
            email = (email ?? "").Trim().ToLowerInvariant();   // B1: kanonik sahiplik anahtari
            // SAHIPLIK: yalniz id ile silmek IDOR olurdu. E-posta esleşmezse "bulunamadi" doner -
            // "var ama senin degil" demek, baskasinin aboneliginin VARLIGINI sizdirirdi.
            var row = await _subDal.GetAsync(n => n.id == id && n.email == email);
            if (row == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.PriceDropNotFound));

            await _subDal.DeleteAsync(row);
            return (HttpStatusCode.OK, new SuccessResult(Messages.NotificationUnsubscribed));
        }

        public async Task<(HttpStatusCode, Result)> UnsubscribeByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.PriceDropNotFound));

            var row = await _subDal.GetAsync(n => n.unsubscribe_token == token);
            if (row == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.PriceDropNotFound));

            await _subDal.DeleteAsync(row);
            return (HttpStatusCode.OK, new SuccessResult(Messages.NotificationUnsubscribed));
        }

        // SPRINT 8 MADDE 10 - E-POSTADAKI "ABONELIKTEN CIK" BAGLANTISI.
        // Ticari elektronik iletide izin GERI ALINABILIR olmali; baglanti bu yuzden metne eklenir.
        // Adres API'nin PUBLIC tabanidir (uc API'de). "Api:PublicBaseUrl" yoksa gorsellerin
        // servis edildigi "Storage:PublicBaseUrl"e duseriz - OLCULEN gerekce: gorseller de API'nin
        // wwwroot'undan servis ediliyor, yani ayni origin. Ikisi de bossa baglanti YERINE ne
        // yapilacagi ACIKCA yazilir; sessizce bos birakilmaz.
        private string AbonelikCikisMetni(string yol, string token)
        {
            var taban = (_config["Api:PublicBaseUrl"] ?? _config["Storage:PublicBaseUrl"] ?? "").TrimEnd('/');
            if (string.IsNullOrWhiteSpace(taban))
                return "\n\nBu bildirimleri almak istemiyorsan Hesabım > Bildirimlerim sayfasından aboneliğini kaldırabilirsin.";
            return $"\n\nBu bildirimleri almak istemiyorsan: {taban}{yol}?token={Uri.EscapeDataString(token)}";
        }
    }
}
