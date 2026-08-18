using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Koleksiyon DAL implementasyonu. Slug ile getirme (nav property yok).
    public class EfCollectionDal : EfEntityRepositoryBase<Collection, DivisimaDbContext>, ICollectionDal
    {
        public EfCollectionDal(DivisimaDbContext context) : base(context)
        {
        }

        public async Task<Collection> GetBySlugAsync(string slug)
        {
            return await Context.Set<Collection>()
                .FirstOrDefaultAsync(c => c.slug == slug && c.is_active);
        }
    }
}
