using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Sepet DAL implementasyonu. Cafixo minimal tarzı.
    public class EfCartDal : EfEntityRepositoryBase<Cart, DivisimaDbContext>, ICartDal
    {
        public EfCartDal(DivisimaDbContext context) : base(context)
        {
        }
    
        // ATOMIK claim - tek UPDATE: reminder_sent_at NULL ise damgala. affected=1 -> bu calistirma kazandi.
        public async Task<bool> TryClaimReminderAsync(int cartId)
        {
            var affected = await Context.Set<Cart>()
                .Where(c => c.id == cartId && c.reminder_sent_at == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.reminder_sent_at, DateTime.Now));
            return affected > 0;
        }

        // Mail gonderimi basarisizsa claim'i geri al (tekrar denenebilsin).
        public async Task ResetReminderClaimAsync(int cartId)
        {
            await Context.Set<Cart>()
                .Where(c => c.id == cartId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.reminder_sent_at, (DateTime?)null));
        }
}
}
