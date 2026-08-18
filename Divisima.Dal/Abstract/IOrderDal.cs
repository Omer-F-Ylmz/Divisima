using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Sipariş DAL. Müşteriye göre getirme (kalemler serviste ayrı yüklenir).
    public interface IOrderDal : IEntityRepository<Order>
    {
    }
}
