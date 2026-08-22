using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Satıcı DAL implementasyonu. Login/brute-force atomik (Customer ile birebir kalıp).
    public class EfSellerDal : EfEntityRepositoryBase<Seller, DivisimaDbContext>, ISellerDal
    {
        public EfSellerDal(DivisimaDbContext context) : base(context)
        {
        }

        // Açıklayıcı yorum: E-posta ile satıcı (login + kayıt duplikat kontrolü). Case-insensitive.
        // KALITE SUPURMESI B1: e-posta KIMLIK dizgesidir - kultursuz normalize edilir.
        // Gerekcenin tamami EfCustomerDal.GetByEmailAsync uzerinde (ayni hata sinifi).
        // Seller modulu bugun veri duzeyinde KAPALI (0 satir) ama ayni kok ilkeye tabi:
        // yarin acilirsa ayni tuzaga dusmesin.
        public async Task<Seller> GetByEmailAsync(string email)
        {
            var normalized = (email ?? "").Trim().ToLowerInvariant();
            return await Context.Set<Seller>()
                .FirstOrDefaultAsync(s => s.email == normalized);
        }

        // Açıklayıcı yorum: ATOMİK başarısız-login artışı - tek UPDATE, YENİ değeri döner.
        public async Task<int> IncrementFailedLoginAsync(int sellerId)
        {
            await Context.Set<Seller>().Where(s => s.id == sellerId)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.failed_login_attempts, s => s.failed_login_attempts + 1));
            return await Context.Set<Seller>().Where(s => s.id == sellerId)
                .Select(s => s.failed_login_attempts).FirstOrDefaultAsync();
        }

        // Açıklayıcı yorum: ATOMİK hesap kilitle (lockout_end + sayaç sıfırla).
        public async Task LockAccountAsync(int sellerId, DateTime until)
        {
            await Context.Set<Seller>().Where(s => s.id == sellerId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(s => s.lockout_end, until)
                    .SetProperty(s => s.failed_login_attempts, 0));
        }

        // Açıklayıcı yorum: ATOMİK login durumu sıfırla (başarılı giriş).
        public async Task ResetLoginStateAsync(int sellerId)
        {
            await Context.Set<Seller>().Where(s => s.id == sellerId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(s => s.failed_login_attempts, 0)
                    .SetProperty(s => s.lockout_end, (DateTime?)null));
        }
    }
}
