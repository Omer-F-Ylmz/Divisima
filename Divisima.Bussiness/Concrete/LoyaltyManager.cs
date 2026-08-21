using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Loyalty;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Loyalty;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Sadakat puanı iş kuralları. Kazanım oranı + puan->kredi dönüşümü sabitlerle.
    public class LoyaltyManager : ILoyaltyService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IOrderDal _orderDal;
        private readonly ILoyaltyTransactionDal _txDal;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;

        // Açıklayıcı yorum: Her 10 TL harcamaya 1 puan; 100 puan = 10 TL kredi (1 puan = 0.10 TL)
        private const decimal SpendPerPoint = 10m;
        private const decimal CreditPerPoint = 0.10m;
        private const int MinRedeemPoints = 100;

        public LoyaltyManager(ICustomerDal customerDal, IOrderDal orderDal, ILoyaltyTransactionDal txDal, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork)
        {
            _customerDal = customerDal;
            _orderDal = orderDal;
            _txDal = txDal;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
        }

        public async Task<(HttpStatusCode, Result)> EarnPoints(int customerId, int points, string reason, int? orderId)
        {
            if (points <= 0) return (HttpStatusCode.OK, new SuccessResult(Messages.LoyaltyNoPoints));
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Concurrency DUZELTMESI: ATOMIK puan kazanimi (eszamanli kazanimda lost update engeli)
                // SESSIZ PARA KAYBI FIX (H54): 0 satir = bakiye ARTMADI (musteri satiri yok) -> defter yazma.
                var credited1 = await _customerDal.IncrementLoyaltyPointsAsync(customerId, points);
                if (credited1 == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.CreditOperationFailed));
                }
                await _txDal.AddAsync(new LoyaltyTransaction
                {
                    customer_id = customerId,
                    points = points,
                    type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = reason,
                    order_id = orderId,
                    created_at = DateTime.Now
                });
                await _unitOfWork.CommitAsync();
            }
            catch { await _unitOfWork.RollbackAsync(); return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.LoyaltyOperationFailed)); }
            return (HttpStatusCode.OK, new SuccessDataResult<int>(c.loyalty_points, Messages.LoyaltyEarned));
        }

        // Açıklayıcı yorum: Sipariş tutarından puan hesapla + kazandır (ödeme başarısında çağrılır)
        public async Task<(HttpStatusCode, Result)> EarnFromOrder(int customerId, decimal orderTotal, int orderId)
        {
            int basePoints = (int)Math.Floor(orderTotal / SpendPerPoint);
            // Açıklayıcı yorum: SADAKAT SEVİYESİ ÇARPANI uygula - Gold %50, Platinum 2x fazla puan kazanır.
            // (Aksi halde tier sistemi kozmetikti; çarpan hiç kullanılmıyordu.)
            var delivered = await _orderDal.GetListNoTrackingAsync(o =>
                o.customer_id == customerId && o.status == (byte)OrderStatusEnum.Delivered);
            decimal totalSpent = delivered.Sum(o => o.total_price);
            var tier = LoyaltyTierHelper.GetTier(totalSpent);
            int points = (int)Math.Floor(basePoints * LoyaltyTierHelper.PointMultiplier(tier));
            return await EarnPoints(customerId, points, "Sipariş puanı", orderId);
        }

        // Açıklayıcı yorum: SİPARİŞ İPTALİNDE PUAN GERİ ALIMI (farming engeli). Puanlar ödemede kazanılıyor;
        // iptal edilince kazanılan puan geri alınmazsa müşteri "sipariş ver -> puan kazan -> iptal et -> refund AL +
        // puanı krediye çevir" ile SINIRSIZ bedava kredi üretebilirdi. Kazanılan puan ledger'daki Earn kaydından bulunur.
        // Kendi transaction'ını AÇMAZ - çağıranın (iptal akışı) ambient transaction'ına katılır (nested olmaz).
        public async Task<(HttpStatusCode, Result)> ReverseForOrder(int customerId, int orderId)
        {
            var earn = await _txDal.GetAsync(t => t.order_id == orderId && t.customer_id == customerId
                                                  && t.type == (byte)LedgerEntryTypeEnum.Earn);
            if (earn == null || earn.points <= 0)
                return (HttpStatusCode.OK, new SuccessResult());   // kazanım yok -> geri alınacak bir şey yok

            // IDEMPOTENCY: bu sipariş için puan-geri-alım kaydı zaten varsa TEKRAR geri alma. İki iptal yolu (ChangeOrderStatus +
            // CancelItem) birbirini dışlasa da, savunma amaçlı: aksi halde müşterinin BAŞKA puanları varken çift-reversal
            // (haksız puan kaybı) olabilirdi. Redeem + bu order_id + bu reason var mı bak.
            const string reverseReason = "Sipariş iptali - puan geri alımı";
            var already = await _txDal.GetAsync(t => t.order_id == orderId && t.customer_id == customerId
                                                     && t.type == (byte)LedgerEntryTypeEnum.Redeem && t.reason == reverseReason);
            if (already != null)
                return (HttpStatusCode.OK, new SuccessResult());   // zaten geri alınmış

            // Güncel bakiyeyi AŞMADAN geri al: müşteri puanı zaten harcadıysa yalnız mevcut kadarını claw-back et (negatif olmaz).
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            int toDeduct = Math.Min(earn.points, c?.loyalty_points ?? 0);
            if (toDeduct <= 0)
                return (HttpStatusCode.OK, new SuccessResult());

            // Atomik sonuç KONTROL EDİLİR: eşzamanlı bir harcama bakiyeyi toDeduct altına düşürdüyse decrement başarısız olur (0)
            // -> o durumda ledger EKLENMEZ (puan zaten harcanmış, geri alınacak bir şey yok; tutarsız kayıt olmaz).
            var affected = await _customerDal.TryDecrementLoyaltyPointsAsync(customerId, toDeduct);
            if (affected > 0)
            {
                await _txDal.AddAsync(new LoyaltyTransaction
                {
                    customer_id = customerId,
                    points = toDeduct,
                    type = (byte)LedgerEntryTypeEnum.Redeem,
                    reason = reverseReason,
                    order_id = orderId,
                    created_at = DateTime.Now
                });
            }
            return (HttpStatusCode.OK, new SuccessResult());
        }

        // Açıklayıcı yorum: Puanı mağaza kredisine çevir (checkout'ta kredi kullanılır)
        public async Task<(HttpStatusCode, Result)> RedeemForCredit(int customerId, int points)
        {
            if (points < MinRedeemPoints)
                return (HttpStatusCode.BadRequest, new ErrorResult(string.Format(Messages.LoyaltyMinRedeem, MinRedeemPoints)));

            var creditAmount = points * CreditPerPoint;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Açıklayıcı yorum: ATOMİK puan düşümü - WHERE loyalty_points >= points. 0 = yetersiz/bulunamadı -> rollback.
                // Eşzamanlı iki istek aynı puanı iki kez harcayamaz (TOCTOU race yok).
                var affected = await _customerDal.TryDecrementLoyaltyPointsAsync(customerId, points);
                if (affected == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.LoyaltyInsufficient));
                }
                // Açıklayıcı yorum: Krediyi ATOMİK ekle (aynı transaction - nested transaction'dan kaçınıldı)
                // SESSIZ PARA KAYBI FIX (H54): 0 satir = bakiye ARTMADI (musteri satiri yok) -> defter yazma.
                var credited2 = await _customerDal.IncrementStoreCreditAsync(customerId, creditAmount);
                if (credited2 == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.CreditOperationFailed));
                }
                await _txDal.AddAsync(new LoyaltyTransaction
                {
                    customer_id = customerId,
                    points = points,
                    type = (byte)LedgerEntryTypeEnum.Redeem,
                    reason = "Krediye çevrildi",
                    order_id = null,
                    created_at = DateTime.Now
                });
                await _creditTxDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = customerId,
                    amount = creditAmount,
                    type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = "Puan dönüşümü",
                    order_id = null,
                    created_at = DateTime.Now
                });
                await _unitOfWork.CommitAsync();
            }
            catch { await _unitOfWork.RollbackAsync(); return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.LoyaltyOperationFailed)); }
            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(creditAmount, Messages.LoyaltyRedeemed));
        }


        // Açıklayıcı yorum: SADAKAT SEVİYESİ - teslim edilen siparişlerin toplamından hesaplanır (yaşam boyu harcama).
        // Seviye + çarpan + bir sonraki seviyeye kalan döner. Frontend rozet/ilerleme çubuğu gösterebilir.
        public async Task<(HttpStatusCode, Result)> GetTier(int customerId)
        {
            var delivered = await _orderDal.GetListNoTrackingAsync(o =>
                o.customer_id == customerId && o.status == (byte)OrderStatusEnum.Delivered);
            decimal totalSpent = delivered.Sum(o => o.total_price);
            var tier = LoyaltyTierHelper.GetTier(totalSpent);
            var dto = new LoyaltyTierDto
            {
                tier = tier.ToString(),
                total_spent = totalSpent,
                point_multiplier = LoyaltyTierHelper.PointMultiplier(tier),
                amount_to_next_tier = LoyaltyTierHelper.AmountToNextTier(totalSpent)
            };
            return (HttpStatusCode.OK, new SuccessDataResult<LoyaltyTierDto>(dto));
        }

        public async Task<(HttpStatusCode, Result)> GetBalance(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));
            return (HttpStatusCode.OK, new SuccessDataResult<int>(c.loyalty_points));
        }

        public async Task<(HttpStatusCode, Result)> GetHistory(int customerId)
        {
            var list = await _txDal.GetListNoTrackingAsync(t => t.customer_id == customerId);
            return (HttpStatusCode.OK, new SuccessDataResult<List<LoyaltyTransaction>>(list.OrderByDescending(t => t.created_at).ToList()));
        }
    }
}
