using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfInvoiceItemDal : EfEntityRepositoryBase<InvoiceItem, DivisimaDbContext>, IInvoiceItemDal
    {
        public EfInvoiceItemDal(DivisimaDbContext context) : base(context) { }
    }
}
