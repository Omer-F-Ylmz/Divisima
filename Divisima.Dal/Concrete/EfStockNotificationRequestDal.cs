using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: EF implementasyonu (DivisimaDbContext).
    public class EfStockNotificationRequestDal : EfEntityRepositoryBase<StockNotificationRequest, DivisimaDbContext>, IStockNotificationRequestDal
    {
        // Açıklayıcı yorum: DbContext'i base'e ilet (EfEntityRepositoryBase parametresiz ctor'a sahip DEĞİL -> zorunlu).
        public EfStockNotificationRequestDal(DivisimaDbContext context) : base(context)
        {
        }
    
        // Açıklayıcı yorum: ATOMİK claim - is_notified'ı YALNIZ false ise true yap (tek UPDATE). affected=1 -> bu çağrı kazandı.
        // Eşzamanlı bildirim çağrıları aynı kaydı çift göndermesin (outbox TryClaimAsync deseni).
        public async Task<bool> TryClaimForNotificationAsync(int id)
        {
            var affected = await Context.Set<StockNotificationRequest>()
                .Where(x => x.id == id && !x.is_notified)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.is_notified, true));
            return affected > 0;
        }

        // Açıklayıcı yorum: Mail gönderimi başarısızsa claim'i geri al (tekrar denenebilsin).
        public async Task ResetNotificationClaimAsync(int id)
        {
            await Context.Set<StockNotificationRequest>()
                .Where(x => x.id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.is_notified, false));
        }
}
}
