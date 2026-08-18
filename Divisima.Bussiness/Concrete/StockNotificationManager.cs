using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.StockNotification;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Stok bildirim iş kuralları. Abonelik + stok gelince e-posta.
    public class StockNotificationManager : IStockNotificationService
    {
        private readonly IStockNotificationRequestDal _notificationDal;
        private readonly IProductDal _productDal;
        private readonly IMailService _mailService;

        public StockNotificationManager(IStockNotificationRequestDal notificationDal, IProductDal productDal, IMailService mailService)
        {
            _notificationDal = notificationDal;
            _productDal = productDal;
            _mailService = mailService;
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
                created_at = DateTime.Now
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
    }
}
