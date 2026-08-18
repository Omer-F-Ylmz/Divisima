using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: İçerik DAL. Slug ile getirme.
    public interface IContentDal : IEntityRepository<Content>
    {
        Task<Content> GetBySlugAsync(string slug);
    }
}
