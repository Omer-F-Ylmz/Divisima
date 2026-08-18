using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Kategori DAL. Ortak CRUD yeterli (alt kategoriler serviste ayrı DAL ile yüklenir).
    public interface ICategoryDal : IEntityRepository<Category>
    {
    }
}
