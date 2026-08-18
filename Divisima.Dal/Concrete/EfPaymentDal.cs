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
    }
}
