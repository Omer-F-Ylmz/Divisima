using System;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.PriceDrop;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Fiyat düşüş bildirimi. Abone olunan fiyatın altına inince e-posta (StockNotification kardeşi).
    public class PriceDropManager : IPriceDropService
    {
        private readonly IPriceDropSubscriptionDal _subDal;
        private readonly IProductDal _productDal;
        private readonly IMailService _mailService;

        private readonly IMarketingGate _marketingGate;

        public PriceDropManager(IPriceDropSubscriptionDal subDal, IProductDal productDal, IMailService mailService,
            IMarketingGate marketingGate)
        {
            _subDal = subDal;
            _productDal = productDal;
            _mailService = mailService;
            _marketingGate = marketingGate;
        }

        public async Task<(HttpStatusCode, Result)> Subscribe(PriceDropSubscribeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.email) || !dto.email.Contains("@"))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidEmail));

            var product = await _productDal.GetAsync(p => p.id == dto.product_id && p.is_active);
            if (product == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Aynı e-posta+ürün için bekleyen abonelik varsa fiyatı güncelle (idempotent)
            var existing = await _subDal.GetAsync(s => s.product_id == dto.product_id && s.email == dto.email && !s.is_notified);
            if (existing != null)
            {
                existing.subscribed_price = product.price;
                await _subDal.UpdateAsync(existing);
                return (HttpStatusCode.OK, new SuccessResult(Messages.PriceDropAlreadySubscribed));
            }

            await _subDal.AddAsync(new PriceDropSubscription
            {
                product_id = dto.product_id, email = dto.email, subscribed_price = product.price,
                is_notified = false, created_at = DateTime.Now
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
                               $"(önceki takip fiyatınız {sub.subscribed_price:N2} TL). Kaçırmayın!\n\nDivisima",
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
    }
}
