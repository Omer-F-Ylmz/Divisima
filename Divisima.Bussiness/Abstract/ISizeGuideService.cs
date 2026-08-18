using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.SizeGuide;
namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Beden rehberi - kategori bazlı ölçü tablosu + "senin bedenin" önerisi.
    public interface ISizeGuideService
    {
        Task<(HttpStatusCode, Result)> Upsert(SizeGuideEntryDto dto);          // admin
        Task<(HttpStatusCode, Result)> GetByCategory(int categoryId);
        Task<(HttpStatusCode, Result)> RecommendSize(int categoryId, decimal? bust, decimal? waist, decimal? hip); // öneri
    }
}
