using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Koleksiyon DAL. Slug ile getirme (ürünler serviste ayrı yüklenir).
    public interface ICollectionDal : IEntityRepository<Collection>
    {
        Task<Collection> GetBySlugAsync(string slug);
    }
}
