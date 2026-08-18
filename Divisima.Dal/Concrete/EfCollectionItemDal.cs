using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Koleksiyon-ürün DAL implementasyonu.
    public class EfCollectionItemDal : EfEntityRepositoryBase<CollectionItem, DivisimaDbContext>, ICollectionItemDal
    {
        public EfCollectionItemDal(DivisimaDbContext context) : base(context)
        {
        }
    }
}
