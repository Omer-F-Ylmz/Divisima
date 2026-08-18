using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: KVKK rıza kaydı DAL implementasyonu.
    public class EfConsentRecordDal : EfEntityRepositoryBase<ConsentRecord, DivisimaDbContext>, IConsentRecordDal
    {
        public EfConsentRecordDal(DivisimaDbContext context) : base(context) { }
    }
}
