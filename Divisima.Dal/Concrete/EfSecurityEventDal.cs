using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfSecurityEventDal : EfEntityRepositoryBase<SecurityEvent, DivisimaDbContext>, ISecurityEventDal
    {
        public EfSecurityEventDal(DivisimaDbContext context) : base(context) { }
    }
}
