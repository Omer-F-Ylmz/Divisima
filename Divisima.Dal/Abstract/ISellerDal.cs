using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Satıcı DAL. Login/brute-force metotları Customer ile aynı atomik kalıp (lost-update yok).
    public interface ISellerDal : IEntityRepository<Seller>
    {
        Task<Seller> GetByEmailAsync(string email);

        // Açıklayıcı yorum: ATOMİK başarısız-login artışı - YENİ değeri döner (brute-force kilidi güvenli).
        Task<int> IncrementFailedLoginAsync(int sellerId);
        // Açıklayıcı yorum: ATOMİK hesap kilitle (lockout_end set + sayaç sıfırla).
        Task LockAccountAsync(int sellerId, DateTime until);
        // Açıklayıcı yorum: ATOMİK login durumu sıfırla (başarılı giriş: sayaç 0 + kilit yok).
        Task ResetLoginStateAsync(int sellerId);
    }
}
