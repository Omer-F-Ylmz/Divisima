using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Mağaza kredisi iş kuralları. Bakiye Customer.store_credit, denetim StoreCreditTransaction.
    public class StoreCreditManager : IStoreCreditService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IStoreCreditTransactionDal _txDal;
        private readonly IUnitOfWork _unitOfWork;

        public StoreCreditManager(ICustomerDal customerDal, IStoreCreditTransactionDal txDal, IUnitOfWork unitOfWork)
        {
            _customerDal = customerDal;
            _txDal = txDal;
            _unitOfWork = unitOfWork;
        }

        public async Task<(HttpStatusCode, Result)> AddCredit(int customerId, decimal amount, string reason, int? orderId)
        {
            if (amount <= 0) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CreditInvalidAmount));
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // LOST-UPDATE DÜZELTMESİ: ATOMİK artır (UseCredit ile tutarlı). Önceki "c.store_credit += amount; UpdateAsync(c)"
                // read-modify-write idi -> iki eşzamanlı AddCredit (ör. admin ekleme + iade) AYNI bakiyeyi okuyup birbirini
                // EZERDI (kredi kaybı - lost update). Tek UPDATE ... SET store_credit = store_credit + amount ile atomik.
                // SESSIZ PARA KAYBI FIX (H54): 0 satir = bakiye ARTMADI (musteri satiri yok) -> defter yazma.
                var credited1 = await _customerDal.IncrementStoreCreditAsync(customerId, amount);
                if (credited1 == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.CreditOperationFailed));
                }
                await _txDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = customerId,
                    amount = amount,
                    type = (byte)LedgerEntryTypeEnum.Earn,
                    reason = reason,
                    order_id = orderId,
                    created_at = DateTime.Now
                });
                await _unitOfWork.CommitAsync();
            }
            catch { await _unitOfWork.RollbackAsync(); return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.CreditOperationFailed)); }
            // Güncel bakiyeyi TAZE oku (atomik artış sonrası izlenen entity stale) - yanıt için doğru değer.
            var updated = await _customerDal.GetAsync(x => x.id == customerId);
            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(updated?.store_credit ?? (c.store_credit + amount), Messages.CreditAdded));
        }

        public async Task<(HttpStatusCode, Result)> UseCredit(int customerId, decimal amount, string reason, int? orderId)
        {
            if (amount <= 0) return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CreditInvalidAmount));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Açıklayıcı yorum: ATOMİK düşüm - bakiye kontrolü + düşüm tek UPDATE ... WHERE store_credit >= amount.
                // İki eşzamanlı istek asla aynı bakiyeyi iki kez harcayamaz (biri WHERE'i geçemez -> 0 döner). TOCTOU race yok.
                var affected = await _customerDal.TryDecrementStoreCreditAsync(customerId, amount);
                if (affected == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CreditInsufficient));
                }
                await _txDal.AddAsync(new StoreCreditTransaction
                {
                    customer_id = customerId,
                    amount = amount,
                    type = (byte)LedgerEntryTypeEnum.Redeem,
                    reason = reason,
                    order_id = orderId,
                    created_at = DateTime.Now
                });
                await _unitOfWork.CommitAsync();
            }
            catch { await _unitOfWork.RollbackAsync(); return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.CreditOperationFailed)); }
            // Açıklayıcı yorum: Güncel bakiyeyi yanıt için taze oku (atomik düşüm sonrası)
            var updated = await _customerDal.GetAsync(x => x.id == customerId);
            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(updated?.store_credit ?? 0m, Messages.CreditUsed));
        }

        public async Task<(HttpStatusCode, Result)> GetBalance(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));
            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(c.store_credit));
        }

        public async Task<(HttpStatusCode, Result)> GetHistory(int customerId)
        {
            var list = await _txDal.GetListNoTrackingAsync(t => t.customer_id == customerId);
            var ordered = list.OrderByDescending(t => t.created_at).ToList();
            return (HttpStatusCode.OK, new SuccessDataResult<List<StoreCreditTransaction>>(ordered));
        }
    }
}
