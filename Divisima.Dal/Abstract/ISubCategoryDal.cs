using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Alt kategori DAL. Ortak CRUD yeterli.
    public interface ISubCategoryDal : IEntityRepository<SubCategory>
    {
    }
}
