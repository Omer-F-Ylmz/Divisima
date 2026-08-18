using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: İçerik DAL implementasyonu.
    public class EfContentDal : EfEntityRepositoryBase<Content, DivisimaDbContext>, IContentDal
    {
        public EfContentDal(DivisimaDbContext context) : base(context)
        {
        }

        public async Task<Content> GetBySlugAsync(string slug)
        {
            return await Context.Set<Content>()
                .FirstOrDefaultAsync(c => c.slug == slug && c.is_active);
        }
    }
}
