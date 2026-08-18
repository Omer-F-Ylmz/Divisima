using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Oturum DAL. Refresh token ile getirme.
    public interface IUserSessionDal : IEntityRepository<UserSession>
    {
        // Aciklayici yorum: ATOMIK toplu oturum gecersizleme (WHERE customer_id AND is_active). Sifre degisimi/logout/reset'te
        // TEK sorgu ile tum aktif oturumlari kapatir (onceki foreach-UpdateAsync N+1 idi). Dondurdugu = kapatilan oturum sayisi.
        Task<int> InvalidateAllForCustomerAsync(int customerId);
        Task<UserSession> GetByRefreshTokenAsync(string refreshToken);
    }
}
