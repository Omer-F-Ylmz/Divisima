using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Collection;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Koleksiyon iş servisi.
    public interface ICollectionService
    {
        Task<(HttpStatusCode, Result)> Add(CollectionAddRequestDto dto);
        Task<(HttpStatusCode, Result)> Update(CollectionUpdateRequestDto dto);
        Task<(HttpStatusCode, Result)> Delete(int id);
        Task<(HttpStatusCode, Result)> ChangeStatus(int id);
        Task<(HttpStatusCode, Result)> GetList();               // tüm koleksiyonlar (ana sayfa + elçiler)
        Task<(HttpStatusCode, Result)> GetBySlug(string slug);  // detay + ürünler (frontend showCollection)
    }
}
