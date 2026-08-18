using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfInvoiceDal : EfEntityRepositoryBase<Invoice, DivisimaDbContext>, IInvoiceDal
    {
        public EfInvoiceDal(DivisimaDbContext context) : base(context) { }
    }
}
