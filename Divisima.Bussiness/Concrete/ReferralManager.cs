using System;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Referans (arkadaşını getir) iş kuralları. Ödül ilk sipariş TAMAMLANINCA iki tarafa mağaza kredisi.
    public class ReferralManager : IReferralService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IOrderDal _orderDal;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;

        // Açıklayıcı yorum: Her iki tarafa verilecek kredi
        private const decimal ReferrerReward = 50m;
        private const decimal RefereeReward = 50m;

        public ReferralManager(ICustomerDal customerDal, IOrderDal orderDal, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork)
        {
            _customerDal = customerDal;
            _orderDal = orderDal;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
        }

        public async Task<(HttpStatusCode, Result)> GetOrCreateMyCode(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            if (string.IsNullOrEmpty(c.referral_code))
            {
                // Açıklayıcı yorum: Benzersiz kısa kod üret (çakışma olasılığı düşük - kontrol edilir)
                string code;
                do { code = "REF" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(); }
                while (await _customerDal.GetAsync(x => x.referral_code == code) != null);
                c.referral_code = code;
                await _customerDal.UpdateAsync(c);
            }
            return (HttpStatusCode.OK, new SuccessDataResult<string>(c.referral_code));
        }

        public async Task<int?> ResolveReferrer(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var referrer = await _customerDal.GetAsync(x => x.referral_code == code && x.is_active);
            return referrer?.id;
        }

        public async Task RewardOnFirstOrder(int customerId, int orderId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null || !c.referred_by.HasValue) return; // davet edilmemiş
            // Açıklayıcı yorum: SELF-REFERRAL engeli - kimse kendini refere edip iki tarafın ödülünü alamaz.
            if (c.referred_by.Value == c.id) return;

            // Açıklayıcı yorum: Bu müşterinin tamamlanmış (Confirmed+) sipariş sayısı - en az bir tamamlanmış sipariş olmalı.
            // PERFORMANS (H51): EXISTS - odul kontrolu icin siparis satirlarini cekmeye gerek yok.
            var hasCompletedOrder = await _orderDal.AnyAsync(o =>
                o.customer_id == customerId && PaidOrderSpec.PaidStatuses.Contains(o.status));   // H52: merkezi kural
            if (!hasCompletedOrder) return; // henüz tamamlanmış sipariş yok

            // KALICI IDEMPOTENCY: bu müşteri için referans ödülü DAHA ÖNCE verildiyse tekrar VERME.
            // Önceki "Count != 1" kontrolü İPTALDE sıfırlanıyordu -> sipariş ver+iptal et+tekrar ver ile ödül FARMING mümkündü.
            // Ledger (StoreCreditTransaction) kalıcı: iptal edilse bile ödül kaydı kalır -> tekrar tetiklenmez.
            var alreadyRewarded = await _creditTxDal.GetAsync(t =>
                t.customer_id == c.id && t.reason == "Referans ödülü (davet edilen)");
            if (alreadyRewarded != null) return;

            var referrer = await _customerDal.GetAsync(x => x.id == c.referred_by.Value && x.is_active);
            if (referrer == null) return;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Açıklayıcı yorum: İki tarafa da INLINE kredi (nested transaction'dan kaçınmak için servis çağrılmaz)
                // Concurrency DUZELTMESI: ATOMIK odul (lost update engeli)
                // SESSIZ PARA KAYBI FIX (H54): 0 satir = bakiye ARTMADI (musteri satiri yok) -> defter yazma.
                var credited1 = await _customerDal.IncrementStoreCreditAsync(referrer.id, ReferrerReward);
                if (credited1 == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return;   // H54: void metot - odul verilemedi, defter yazilmadan cikilir (rollback yapildi)
                }
                await _creditTxDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = referrer.id,
                    amount = ReferrerReward,
                    type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = "Referans ödülü (davet eden)",
                    order_id = orderId,
                    created_at = DateTime.Now
                });

                // Concurrency DUZELTMESI: ATOMIK odul (lost update engeli)
                // SESSIZ PARA KAYBI FIX (H54): 0 satir = bakiye ARTMADI (musteri satiri yok) -> defter yazma.
                var credited2 = await _customerDal.IncrementStoreCreditAsync(c.id, RefereeReward);
                if (credited2 == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return;   // H54: void metot - odul verilemedi, defter yazilmadan cikilir (rollback yapildi)
                }
                await _creditTxDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = c.id,
                    amount = RefereeReward,
                    type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = "Referans ödülü (davet edilen)",
                    order_id = orderId,
                    created_at = DateTime.Now
                });

                await _unitOfWork.CommitAsync();
            }
            catch { await _unitOfWork.RollbackAsync(); }
        }
    }
}
