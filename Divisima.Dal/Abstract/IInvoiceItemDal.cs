using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;
namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Fatura kalemleri DAL. Ortak CRUD yeterli - kalemler her zaman
    // invoice_id üzerinden okunur (indeks o kolonda).
    public interface IInvoiceItemDal : IEntityRepository<InvoiceItem> { }
}
