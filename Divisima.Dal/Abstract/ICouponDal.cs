using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Kupon DAL. Koda göre getirme özel sorgusu.
    public interface ICouponDal : IEntityRepository<Coupon>
    {
        Task<Coupon> GetByCodeAsync(string code);

        // SPRINT 8 MADDE 1 - IDEMPOTENT KUPON SAYACI.
        // used_count artik "+1" ile DEGIL, coupon_usages satirlarindan TURETILEREK yazilir.
        // Gerekce: eski `IncrementCouponUsageWithRetry` duz bir sayac artisiydi; callback tam
        // bir kez kostugu icin bugun zararsizdi, ama B bolgesi at-least-once bir mekanizmaya
        // (outbox - madde 3) tasindiginda ayni siparis icin sayac FAZLA sayardi ve kupon limiti
        // gercekte dolmadan "dolmus" gorunurdu.
        // Turetme TANIMI GEREGI idempotenttir: kac kez kosarsa kossun ayni sonucu verir.
        // Ikinci savunma hatti `coupon_usages(coupon_id, order_id)` UNIQUE indeksidir - ayni
        // siparis icin iki kullanim satiri hic olusamaz.
        Task<int> SyncUsedCountAsync(int couponId);
    }
}
