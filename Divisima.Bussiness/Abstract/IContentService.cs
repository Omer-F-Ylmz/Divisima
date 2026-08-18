using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Content;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: İçerik servisi. Slug ile getir (public) + güncelle (admin).
    public interface IContentService
    {
        Task<(HttpStatusCode, Result)> GetBySlug(string slug);
        Task<(HttpStatusCode, Result)> GetList();
        Task<(HttpStatusCode, Result)> Update(ContentUpdateRequestDto dto);
    }
}
