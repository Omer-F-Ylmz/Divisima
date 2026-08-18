using Divisima.Core.DataAccess;
using Divisima.DataAccess.Concrete.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Divisima.DataAccess.Concrete
{
    // Açıklayıcı yorum: EF Core transaction sarmalayıcısı. Autofac InstancePerLifetimeScope sayesinde
    // aynı istekteki tüm DAL'lar aynı DbContext örneğini paylaşır; buradaki transaction hepsini kapsar.
    // Her DAL yine SaveChanges çağırır ama commit'e kadar kalıcı olmaz (transaction içinde).
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DivisimaDbContext _context;
        private IDbContextTransaction _transaction;

        public UnitOfWork(DivisimaDbContext context)
        {
            _context = context;
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        // Aciklayici yorum: RETRY-GUVENLI transaction sarmalayici. EnableRetryOnFailure aktifken
        // manuel BeginTransaction reddedilir; execution strategy tum islemi tek retriable birim yapar.
        // Gecici DB kopmalarinda otomatik yeniden dener (tum begin->is->commit tekrarlanir - idempotent olmali).
        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await operation();
                    await tx.CommitAsync();
                    return result;
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
        }
    }
}
