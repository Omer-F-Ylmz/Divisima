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
        //
        // ══ KALITE SUPURMESI B2 - KUPON KODU KIMLIK DIZGESIDIR ═════════════════════════════
        // ONCEKI HALI: `.ToUpper()` (kultur duyarli). Uygulama tr-TR'ye pinli oldugu icin
        // 'i' -> 'İ' (U+0130) oluyordu. OLCULEN UCLU AYRISMA:
        //   admin paneli : JS 'indirim10'.toUpperCase() = 'INDIRIM10'   -> BU SAKLANIYORDU
        //   backend      : C# tr-TR 'indirim10'.ToUpper() = 'İNDİRİM10'
        //   storefront   : kodu HAM gonderiyor (yalniz trim)
        // Sonuc: "i" iceren bir kupon YALNIZ buyuk harfle yazilinca calisiyordu.
        // KANONIK BICIM = KimlikDizgesi.KanonikKod, HEM yazmada HEM dogrulamada (CouponManager de ayni).
        // SQL tarafindaki UPPER() KALDIRILDI: saklanan deger artik her zaman kanonik oldugu
        // icin dogrudan esitlik hem DOGRU hem indeks dostu (EfCustomerDal ile ayni gerekce).
        //
        // NEDEN DUZ ToUpperInvariant YETMEDI (pin yazarken OLCULDU): Turkce klavyede buyuk
        // harf 'i' -> 'İ' (U+0130) ve invariant casing bunu ASCII 'I'ya CEVIRMEZ - musteri
        // kodu Turkce klavyede BUYUK yazdiginda ('İNDİRİM10') hicbir sey eslesmiyordu.
        // KanonikKod once Turkce'ye ozgu harfleri katlar, sonra invariant buyultur.
        public async Task<Coupon> GetByCodeAsync(string code)
        {
            var normalized = Divisima.Core.Utilities.Text.KimlikDizgesi.KanonikKod(code);
            return await Context.Set<Coupon>()
                .FirstOrDefaultAsync(c => c.code == normalized && c.is_active);
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
