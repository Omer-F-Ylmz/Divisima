using Divisima.Core.DataAccess;
using Divisima.Entity.Entities;

namespace Divisima.DataAccess.Abstract
{
    // Açıklayıcı yorum: Ürün DAL. Ortak CRUD IEntityRepository'den; filtre/paging özel sorgu (nav property yok,
    // beden filtresi explicit ProductStocks alt sorgusuyla). Cafixo minimal DAL tarzı.
    public interface IProductDal : IEntityRepository<Product>
    {
        Task<(List<Product> items, int totalCount)> GetListWithFilterAsync(
            int? categoryId, int? subCategoryId, List<string> sizes, List<string> colors,
            decimal? minPrice, decimal? maxPrice, bool? onSale, bool? inStock,
            string sort, int page, int size);
    }
}
