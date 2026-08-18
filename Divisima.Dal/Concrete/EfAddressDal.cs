using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    public class EfAddressDal : EfEntityRepositoryBase<Address, DivisimaDbContext>, IAddressDal
    {
        public EfAddressDal(DivisimaDbContext context) : base(context) { }
    }
}
