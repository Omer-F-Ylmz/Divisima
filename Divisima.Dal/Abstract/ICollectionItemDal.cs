using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Koleksiyon-ürün DAL. Ortak CRUD yeterli.
    public interface ICollectionItemDal : IEntityRepository<CollectionItem>
    {
    }
}
