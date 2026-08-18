using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfCustomerDeviceDal : EfEntityRepositoryBase<CustomerDevice, DivisimaDbContext>, ICustomerDeviceDal
    {
        public EfCustomerDeviceDal(DivisimaDbContext context) : base(context) { }
    }
}
