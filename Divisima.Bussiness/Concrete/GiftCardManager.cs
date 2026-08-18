using Divisima.Core.DataAccess;
using System;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Hediye kartı iş kuralları. Bozdurma -> mağaza kredisi (INLINE, nested transaction yok).
    public class GiftCardManager : IGiftCardService
    {
        private readonly IGiftCardDal _giftCardDal;
        private readonly ICustomerDal _customerDal;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;

        public GiftCardManager(IGiftCardDal giftCardDal, ICustomerDal customerDal, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork)
        {
            _giftCardDal = giftCardDal;
            _customerDal = customerDal;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
        }

        public async Task<(HttpStatusCode, Result)> Create(decimal amount)
        {
            if (amount <= 0) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.GiftCardInvalidAmount));

            // Açıklayıcı yorum: Benzersiz kod üret (çakışma olasılığı ihmal edilebilir - 16 hex)
            var code = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            await _giftCardDal.AddAsync(new GiftCard
            {
                code = code, initial_amount = amount, balance = amount, is_active = true, created_at = DateTime.Now
            });
            return (HttpStatusCode.Created, new SuccessDataResult<string>(code, Messages.GiftCardCreated));
        }

        public async Task<(HttpStatusCode, Result)> CheckBalance(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.GiftCardNotFound));
            var card = await _giftCardDal.GetAsync(g => g.code == code && g.is_active);
            if (card == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.GiftCardNotFound));
            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(card.balance));
        }

        public async Task<(HttpStatusCode, Result)> Redeem(int customerId, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.GiftCardNotFound));
            var card = await _giftCardDal.GetAsync(g => g.code == code && g.is_active);
            if (card == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.GiftCardNotFound));
            if (card.balance <= 0) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.GiftCardEmpty));

            var amount = card.balance;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Açıklayıcı yorum: ATOMİK bozdurma (compare-and-swap) - kart yalnızca beklenen bakiyeyle EŞLEŞİRSE sıfırlanır.
                // İki eşzamanlı bozdurma isteğinden yalnızca biri affected=1 alır; diğeri 0 alır -> çift kredi ENGELLENİR.
                var consumed = await _giftCardDal.TryRedeemAsync(card.id, amount, customerId, DateTime.Now);
                if (consumed == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.Conflict, new ErrorResult(Messages.GiftCardEmpty));
                }

                // Açıklayıcı yorum: Krediyi ATOMİK ekle (aynı transaction)
                // SESSIZ PARA KAYBI FIX (H54): 0 satir = bakiye ARTMADI (musteri satiri yok) -> defter yazma.
                var credited1 = await _customerDal.IncrementStoreCreditAsync(customerId, amount);
                if (credited1 == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.CreditOperationFailed));
                }

                await _creditTxDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = customerId, amount = amount, type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = "Hediye kartı bozdurma", order_id = null, created_at = DateTime.Now
                });
                await _unitOfWork.CommitAsync();
            }
            catch { await _unitOfWork.RollbackAsync(); return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.GiftCardRedeemFailed)); }

            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(amount, Messages.GiftCardRedeemed));
        }
    }
}
