using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Category;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Kategori iş servisi. Tuple imza kalıbı.
    public interface ICategoryService
    {
        Task<(HttpStatusCode, Result)> Add(CategoryAddRequestDto dto);
        Task<(HttpStatusCode, Result)> Update(CategoryUpdateRequestDto dto);
        Task<(HttpStatusCode, Result)> Delete(int id);
        Task<(HttpStatusCode, Result)> ChangeStatus(int id);
        Task<(HttpStatusCode, Result)> GetById(int id);
        Task<(HttpStatusCode, Result)> GetList();   // tüm kategoriler + alt kategoriler (menü)
    }
}
