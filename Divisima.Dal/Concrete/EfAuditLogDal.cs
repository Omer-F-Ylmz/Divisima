using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfAuditLogDal : EfEntityRepositoryBase<AuditLog, DivisimaDbContext>, IAuditLogDal
    {
        public EfAuditLogDal(DivisimaDbContext context) : base(context) { }
    }
}
