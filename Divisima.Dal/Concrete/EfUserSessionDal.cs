using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Oturum DAL implementasyonu.
    public class EfUserSessionDal : EfEntityRepositoryBase<UserSession, DivisimaDbContext>, IUserSessionDal
    {
        public EfUserSessionDal(DivisimaDbContext context) : base(context)
        {
        }

        // Açıklayıcı yorum: Refresh token ile aktif oturum
        public async Task<UserSession> GetByRefreshTokenAsync(string refreshToken)
        {
            return await Context.Set<UserSession>()
                .FirstOrDefaultAsync(s => s.refresh_token == refreshToken && s.is_active);
        }

        // Aciklayici yorum: TEK atomik UPDATE - tum aktif oturumlari kapatir (foreach yerine).
        public async Task<int> InvalidateAllForCustomerAsync(int customerId)
        {
            return await Context.Set<UserSession>()
                .Where(us => us.customer_id == customerId && us.is_active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(us => us.is_active, false));
        }
    }
}
