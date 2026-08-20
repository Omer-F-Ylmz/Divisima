using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfPaymentDal : EfEntityRepositoryBase<Payment, DivisimaDbContext>, IPaymentDal
    {
        public EfPaymentDal(DivisimaDbContext context) : base(context) { }
        public async Task<Payment> GetByConversationIdAsync(string conversationId) =>
            await Context.Set<Payment>().FirstOrDefaultAsync(p => p.conversation_id == conversationId);

        // Aciklayici yorum: ATOMIK durum gecisi - EfReturnRequestDal.TryTransitionAsync ile ayni kalip.
        // ExecuteUpdateAsync change-tracker'i ATLAR, bu yuzden bayat izlenen nesne sonucu bozamaz.
        public async Task<int> TryTransitionStatusAsync(int paymentId, byte fromStatus, byte toStatus) =>
            await Context.Set<Payment>()
                .Where(p => p.id == paymentId && p.payment_status == fromStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.payment_status, toStatus));
    }
}
