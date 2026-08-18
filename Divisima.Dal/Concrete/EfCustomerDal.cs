using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Müşteri DAL implementasyonu.
    public class EfCustomerDal : EfEntityRepositoryBase<Customer, DivisimaDbContext>, ICustomerDal
    {
        public EfCustomerDal(DivisimaDbContext context) : base(context)
        {
        }

        // Açıklayıcı yorum: E-posta ile müşteri (login + kayıt duplikat kontrolü)
        public async Task<Customer> GetByEmailAsync(string email)
        {
            var normalized = (email ?? "").Trim().ToLower();
            return await Context.Set<Customer>()
                .FirstOrDefaultAsync(c => c.email.ToLower() == normalized);
        }

        // Açıklayıcı yorum: ATOMİK düşüm - WHERE guard'ı DB'de değerlendirilir, iki eşzamanlı istek asla aynı bakiyeyi
        // iki kez harcayamaz (biri koşulu geçemez, 0 döner). row_version gerektirmez, diğer Customer yazma yollarını etkilemez.
        public async Task<int> TryDecrementStoreCreditAsync(int customerId, decimal amount)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId && c.store_credit >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.store_credit, c => c.store_credit - amount));
        }

        public async Task<int> TryDecrementLoyaltyPointsAsync(int customerId, int points)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId && c.loyalty_points >= points)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.loyalty_points, c => c.loyalty_points - points));
        }

        public async Task<int> IncrementStoreCreditAsync(int customerId, decimal amount)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.store_credit, c => c.store_credit + amount));
        }

        // Aciklayici yorum: ATOMIK puan ekleme - tek UPDATE (read-modify-write lost update'ini onler)
        public async Task<int> IncrementLoyaltyPointsAsync(int customerId, int points)
        {
            return await Context.Set<Customer>()
                .Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.loyalty_points, c => c.loyalty_points + points));
        }

        // Aciklayici yorum: ATOMIK basarisiz-login artisi + YENI degeri don (eszamanli denemeler artisi kaybetmez).
        public async Task<int> IncrementFailedLoginAsync(int customerId)
        {
            await Context.Set<Customer>().Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.failed_login_attempts, c => c.failed_login_attempts + 1));
            return await Context.Set<Customer>().Where(c => c.id == customerId)
                .Select(c => c.failed_login_attempts).FirstAsync();
        }

        public async Task LockAccountAsync(int customerId, DateTime until)
        {
            await Context.Set<Customer>().Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.lockout_end, until)
                    .SetProperty(c => c.failed_login_attempts, 0));
        }

        public async Task ResetLoginStateAsync(int customerId, DateTime lastLogin)
        {
            await Context.Set<Customer>().Where(c => c.id == customerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.failed_login_attempts, 0)
                    .SetProperty(c => c.lockout_end, (DateTime?)null)
                    .SetProperty(c => c.last_login_at, (DateTime?)lastLogin));
        }
    }
}
