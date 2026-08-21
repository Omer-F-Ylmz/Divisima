using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Notification;
using Divisima.Entity.Dtos.StockNotification;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Configuration;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Stok bildirim iş kuralları. Abonelik + stok gelince e-posta.
    public class StockNotificationManager : IStockNotificationService
    {
        private readonly IStockNotificationRequestDal _notificationDal;
        private readonly IProductDal _productDal;
        private readonly IMailService _mailService;

        private readonly IConfiguration _config;

        public StockNotificationManager(IStockNotificationRequestDal notificationDal, IProductDal productDal, IMailService mailService, IConfiguration config)
        {
            _notificationDal = notificationDal;
            _productDal = productDal;
            _mailService = mailService;
            _config = config;
        }

        public async Task<(HttpStatusCode, Result)> Subscribe(StockNotificationSubscribeRequestDto dto)
        {
            // Açıklayıcı yorum: Temel doğrulama
            if (string.IsNullOrWhiteSpace(dto.email) || !dto.email.Contains("@"))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidEmail));

            var product = await _productDal.GetAsync(p => p.id == dto.product_id && p.is_active);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Aynı e-posta + ürün + beden için zaten bekleyen talep varsa tekrar oluşturma (idempotent)
            var size = dto.size ?? "";
            var existing = await _notificationDal.GetAsync(n =>
                n.product_id == dto.product_id && n.size == size && n.email == dto.email && !n.is_notified);
            if (existing != null)
                return (HttpStatusCode.OK, new SuccessResult(Messages.StockNotificationAlreadySubscribed));

            await _notificationDal.AddAsync(new StockNotificationRequest
            {
                product_id = dto.product_id,
                size = size,
                email = dto.email,
                is_notified = false,
                created_at = DateTime.Now,
                unsubscribe_token = Divisima.Core.Utilities.Security.UnsubscribeToken.Yeni()
            });

            return (HttpStatusCode.OK, new SuccessResult(Messages.StockNotificationSubscribed));
        }

        public async Task NotifyBackInStock(int productId, string size)
        {
            var sz = size ?? "";
            // Açıklayıcı yorum: Bu ürün+beden için bekleyen (henüz haber verilmemiş) abonelikler (no-tracking, salt-okuma)
            var pending = await _notificationDal.GetListNoTrackingAsync(n =>
                n.product_id == productId && n.size == sz && !n.is_notified);
            if (pending == null || pending.Count == 0) return;

            var product = await _productDal.GetAsync(p => p.id == productId);
            var productName = product?.name ?? "Ürün";

            foreach (var req in pending)
            {
                // CONCURRENCY FIX (H42): ATOMİK claim ÖNCE - is_notified false->true yalnız bir çalıştırmada başarılı.
                // Eşzamanlı iki NotifyBackInStock (veya crash-retry) aynı aboneye ÇİFT e-posta atmasın (önceden
                // gönder->sonra işaretle sırası + non-atomik update = çift-gönderim riskiydi).
                if (!await _notificationDal.TryClaimForNotificationAsync(req.id))
                    continue; // başka bir çağrı zaten aldı + gönderdi

                // Açıklayıcı yorum: E-posta gönder (hata olsa da diğerlerini engelleme)
                try
                {
                    await _mailService.SendAsync(new MailMessageDto
                    {
                        To = req.email,
                        Subject = "Stokta! " + productName,
                        Body = $"Merhaba,\n\nİlgilendiğiniz \"{productName}\"" +
                               (string.IsNullOrEmpty(sz) ? "" : $" ({sz} beden)") +
                               " tekrar stokta. Kaçırmadan sipariş verebilirsiniz.\n\nDivisima",
                        IsHtml = false
                    });
                }
                catch
                {
                    // Gönderim başarısız -> claim'i geri al (tekrar denenebilsin). Gerçekte ILogger ile loglanır.
                    await _notificationDal.ResetNotificationClaimAsync(req.id);
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

            // Salt-okuma: izlemeye almaya gerek yok (EfEntityRepositoryBase.GetAsync TRACKED'dir).
            var rows = await _notificationDal.GetListNoTrackingAsync(n => n.email == email);
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
                    type = "stock",
                    product_id = r.product_id,
                    product_name = adlar.TryGetValue(r.product_id, out var ad) ? ad : null,
                    size = string.IsNullOrWhiteSpace(r.size) ? null : r.size,
                    is_notified = r.is_notified,
                    created_at = r.created_at,
                    notified_at = r.notified_at
                })
                .ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<NotificationSubscriptionDto>>(liste));
        }

        public async Task<(HttpStatusCode, Result)> RemoveMine(int id, string email)
        {
            // SAHIPLIK: yalniz id ile silmek IDOR olurdu. E-posta esleşmezse "bulunamadi" doner -
            // "var ama senin degil" demek, baskasinin aboneliginin VARLIGINI sizdirirdi.
            var row = await _notificationDal.GetAsync(n => n.id == id && n.email == email);
            if (row == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotificationNotFound));

            await _notificationDal.DeleteAsync(row);
            return (HttpStatusCode.OK, new SuccessResult(Messages.NotificationUnsubscribed));
        }

        public async Task<(HttpStatusCode, Result)> UnsubscribeByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotificationNotFound));

            var row = await _notificationDal.GetAsync(n => n.unsubscribe_token == token);
            if (row == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotificationNotFound));

            await _notificationDal.DeleteAsync(row);
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
