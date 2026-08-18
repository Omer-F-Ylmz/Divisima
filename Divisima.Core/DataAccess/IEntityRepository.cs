using System.Linq.Expressions;
using Divisima.Core.Entities.Abstract;
using Divisima.Core.Utilities.Dtos;

namespace Divisima.Core.DataAccess
{
    // Generic repository (Cafixo IEntityRepository kalibi) + performans/paging eklentileri.
    public interface IEntityRepository<T> where T : class, IEntity, new()
    {
        T Get(Expression<Func<T, bool>> filter);
        List<T> GetList(Expression<Func<T, bool>> filter = null);
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);

        Task<T> GetAsync(Expression<Func<T, bool>> filter);
        Task<List<T>> GetListAsync(Expression<Func<T, bool>> filter = null);

        // Degisiklik izleme kapali okuma (read-only sorgular - daha hizli, az bellek)
        Task<List<T>> GetListNoTrackingAsync(Expression<Func<T, bool>> filter = null);

        // Generic sayfali sorgu (filtre + siralama + sayfa). Tum liste endpoint'leri kullanir.
        Task<PagedResult<T>> GetPagedAsync(
            PagingRequestDto paging,
            Expression<Func<T, bool>> filter = null,
            Expression<Func<T, object>> orderBy = null,
            bool descending = false);

        // PERFORMANS (H51): SAYIM SQL'de yapilir. Onceden GetListAsync(...).Count kullaniliyordu ->
        // eslesen TUM satirlar bellege cekiliyordu. COUNT(*) tek sayi doner.
        Task<int> CountAsync(Expression<Func<T, bool>> filter = null);
        // Varlik kontrolu icin EXISTS - Count>0'dan ucuz (ilk kayitta durur).
        Task<bool> AnyAsync(Expression<Func<T, bool>> filter = null);

        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        // TOPLU sil - tek SQL DELETE ... WHERE (foreach tek-tek Remove+SaveChanges N+1 yerine).
        Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate);
        Task DeleteAsync(T entity);

        // Global query filter'i (soft-delete) ATLAYARAK getir - admin reaktivasyon / inaktif goruntuleme icin.
        // NOT (H55): bu iki bildirim dosyanin EN BASINDA, namespace disinda duruyordu ve using'ler
        // arkalarina yapismisti -> CS1529. Ayrica tip parametresi "TEntity" yazilmisti, dogrusu "T".
        Task<T> GetIgnoringFiltersAsync(Expression<Func<T, bool>> filter);
        Task<List<T>> GetListIgnoringFiltersAsync(Expression<Func<T, bool>> filter = null);
    }
}
