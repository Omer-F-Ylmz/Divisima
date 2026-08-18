using System.Linq.Expressions;
using Divisima.Core.Entities.Abstract;
using Divisima.Core.Utilities.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Divisima.Core.DataAccess.EntityFramework
{
    // Açıklayıcı yorum: EF Core generic repository (Cafixo kalıbı) + AsNoTracking + paging.
    public class EfEntityRepositoryBase<TEntity, TContext> : IEntityRepository<TEntity>
        where TEntity : class, IEntity, new()
        where TContext : DbContext  // new() kaldırıldı: DbContext options-ctor kullanır (EF best practice), new TContext() zaten kullanılmıyor
    {
        protected readonly TContext Context;

        public EfEntityRepositoryBase(TContext context)
        {
            Context = context;
        }

        public TEntity Get(Expression<Func<TEntity, bool>> filter) =>
            Context.Set<TEntity>().FirstOrDefault(filter);

        public List<TEntity> GetList(Expression<Func<TEntity, bool>> filter = null) =>
            filter == null ? Context.Set<TEntity>().ToList() : Context.Set<TEntity>().Where(filter).ToList();

        public void Add(TEntity entity) { Context.Set<TEntity>().Add(entity); Context.SaveChanges(); }
        public void Update(TEntity entity) { Context.Set<TEntity>().Update(entity); Context.SaveChanges(); }
        public void Delete(TEntity entity) { Context.Set<TEntity>().Remove(entity); Context.SaveChanges(); }

        public async Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> filter) =>
            await Context.Set<TEntity>().FirstOrDefaultAsync(filter);

        // Açıklayıcı yorum: Query filter'ı atla - TRACKED (ChangeStatus/reaktivasyon güncelleyecek)
        public async Task<TEntity> GetIgnoringFiltersAsync(Expression<Func<TEntity, bool>> filter) =>
            await Context.Set<TEntity>().IgnoreQueryFilters().FirstOrDefaultAsync(filter);

        // Açıklayıcı yorum: Query filter'ı atla - NoTracking (admin inaktif dahil listeleme)
        public async Task<List<TEntity>> GetListIgnoringFiltersAsync(Expression<Func<TEntity, bool>> filter = null)
        {
            var q = Context.Set<TEntity>().IgnoreQueryFilters().AsNoTracking();
            if (filter != null) q = q.Where(filter);
            return await q.ToListAsync();
        }

        public async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> filter = null) =>
            filter == null ? await Context.Set<TEntity>().ToListAsync()
                           : await Context.Set<TEntity>().Where(filter).ToListAsync();

        // Açıklayıcı yorum: Read-only - change tracker'a eklenmez (liste sorgularında performans)
        public async Task<List<TEntity>> GetListNoTrackingAsync(Expression<Func<TEntity, bool>> filter = null)
        {
            var q = Context.Set<TEntity>().AsNoTracking();
            if (filter != null) q = q.Where(filter);
            return await q.ToListAsync();
        }

        // Açıklayıcı yorum: Generic sayfalama - toplam sayı + istenen sayfa, AsNoTracking ile
        public async Task<PagedResult<TEntity>> GetPagedAsync(
            PagingRequestDto paging,
            Expression<Func<TEntity, bool>> filter = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool descending = false)
        {
            var q = Context.Set<TEntity>().AsNoTracking();
            if (filter != null) q = q.Where(filter);

            var total = await q.CountAsync();

            if (orderBy != null)
                q = descending ? q.OrderByDescending(orderBy) : q.OrderBy(orderBy);

            // Açıklayıcı yorum: PAGINATION SINIR (TÜM çağıranlar için MERKEZİ savunma): page>=1, size 1..100. Clamp'siz
            // size çok büyük -> DB'den devasa sonuç (DoS - bellek/bant); page<=0 -> negatif OFFSET (SQL hatası). Girdi clamp'lenir.
            var page = paging.page < 1 ? 1 : paging.page;
            var size = paging.size < 1 ? 20 : (paging.size > 100 ? 100 : paging.size);
            var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();

            return new PagedResult<TEntity>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                Size = size
            };
        }

        // PERFORMANS (H51): COUNT(*) - satirlari cekmeden sayar. AsNoTracking + filtre opsiyonel.
        public async Task<int> CountAsync(Expression<Func<TEntity, bool>> filter = null)
        {
            var q = Context.Set<TEntity>().AsNoTracking();
            if (filter != null) q = q.Where(filter);
            return await q.CountAsync();
        }

        // PERFORMANS (H51): EXISTS - ilk eslesmede durur, Count>0'dan ucuz.
        public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter = null)
        {
            var q = Context.Set<TEntity>().AsNoTracking();
            if (filter != null) q = q.Where(filter);
            return await q.AnyAsync();
        }

        public async Task AddAsync(TEntity entity) { await Context.Set<TEntity>().AddAsync(entity); await Context.SaveChangesAsync(); }
        public async Task UpdateAsync(TEntity entity) { Context.Set<TEntity>().Update(entity); await Context.SaveChangesAsync(); }
        public async Task DeleteAsync(TEntity entity) { Context.Set<TEntity>().Remove(entity); await Context.SaveChangesAsync(); }
        // Açıklayıcı yorum: TOPLU sil - tek SQL DELETE ... WHERE (retention/cleanup için; foreach Remove N+1 değil).
        public async Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> predicate)
            => await Context.Set<TEntity>().Where(predicate).ExecuteDeleteAsync();
    }
}
