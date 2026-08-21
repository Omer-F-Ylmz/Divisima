using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Kupon DAL implementasyonu.
    public class EfCouponDal : EfEntityRepositoryBase<Coupon, DivisimaDbContext>, ICouponDal
    {
        public EfCouponDal(DivisimaDbContext context) : base(context)
        {
        }

        // Açıklayıcı yorum: Kod büyük/küçük harf duyarsız aranır (frontend toUpperCase)
        public async Task<Coupon> GetByCodeAsync(string code)
        {
            var normalized = (code ?? "").Trim().ToUpper();
            return await Context.Set<Coupon>()
                .FirstOrDefaultAsync(c => c.code.ToUpper() == normalized && c.is_active);
        }

        // SPRINT 8 MADDE 1: used_count'u coupon_usages satirlarindan TURET.
        // TEK ifade, TEK gidis-donus; okuma ve yazma arasinda yaris yok. `ExecuteUpdateAsync`
        // change-tracker'i ATLAR (bkz. CLAUDE.md tuzagi) - cagiranin elindeki `Coupon` nesnesi
        // BAYAT kalir; bu metodun cagricisi zaten sayaci okumuyor, ama okuyacaksa taze
        // (`GetListNoTrackingAsync`) okumak zorunda.
        public async Task<int> SyncUsedCountAsync(int couponId) =>
            await Context.Set<Coupon>()
                .Where(c => c.id == couponId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    c => c.used_count,
                    c => Context.Set<CouponUsage>().Count(u => u.coupon_id == c.id)));
    }
}
