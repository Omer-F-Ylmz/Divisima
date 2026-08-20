using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Sipariş DAL implementasyonu. Cafixo minimal tarzı (kalemler serviste GetListAsync ile).
    public class EfOrderDal : EfEntityRepositoryBase<Order, DivisimaDbContext>, IOrderDal
    {
        public EfOrderDal(DivisimaDbContext context) : base(context)
        {
        }

        // Aciklayici yorum: ATOMIK compare-and-swap. WHERE'de HEM beklenen deger HEM de ust sinir
        // var: iki eszamanlı iade ayni "kalan hakki" iki kez tahsis EDEMEZ (lost update yok) ve
        // toplam hicbir kosulda total_price'i asamaz (savunma cift katmanli).
        public async Task<int> TryAddRefundedAmountAsync(int orderId, decimal amount, decimal expectedCurrent) =>
            await Context.Set<Order>()
                .Where(o => o.id == orderId
                            && o.refunded_amount == expectedCurrent
                            && o.refunded_amount + amount <= o.total_price)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.refunded_amount, o => o.refunded_amount + amount));

        // Aciklayici yorum: Tahsisi geri birak - saglayici iadesi basarisiz olduysa kalan iade hakki
        // BLOKE KALMAMALI. Alt sinir 0 (WHERE ile korunur) - sayac negatife dusemez.
        public async Task<int> ReleaseRefundedAmountAsync(int orderId, decimal amount) =>
            await Context.Set<Order>()
                .Where(o => o.id == orderId && o.refunded_amount >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.refunded_amount, o => o.refunded_amount - amount));
    }
}
