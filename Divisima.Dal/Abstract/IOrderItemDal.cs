using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: OrderItem DAL arayüzü.
    public interface IOrderItemDal : IEntityRepository<OrderItem>
    {
    }
}
