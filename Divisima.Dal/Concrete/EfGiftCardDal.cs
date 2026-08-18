using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Hediye kartı DAL implementasyonu.
    public class EfGiftCardDal : EfEntityRepositoryBase<GiftCard, DivisimaDbContext>, IGiftCardDal
    {
        public EfGiftCardDal(DivisimaDbContext context) : base(context)
        {
        }

        // Açıklayıcı yorum: Compare-and-swap ile atomik bozdurma - beklenen bakiye eşleşmezse (başkası bozdurduysa) 0 döner.
        // Tüm bozdurma alanları tek UPDATE'te set edilir (tracked entity ile ikinci update balance=0'ı ezerdi - o yüzden burada).
        public async Task<int> TryRedeemAsync(int cardId, decimal expectedBalance, int redeemedBy, System.DateTime redeemedAt)
        {
            return await Context.Set<GiftCard>()
                .Where(g => g.id == cardId && g.balance == expectedBalance && g.balance > 0m)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.balance, 0m)
                    .SetProperty(g => g.is_active, false)
                    .SetProperty(g => g.redeemed_by, (int?)redeemedBy)
                    .SetProperty(g => g.redeemed_at, (System.DateTime?)redeemedAt));
        }
    }
}
