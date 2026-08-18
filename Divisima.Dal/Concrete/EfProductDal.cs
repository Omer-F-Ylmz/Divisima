using Divisima.Core.DataAccess.EntityFramework;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Divisima.DataAccess.Concrete.EntityFramework
{
    // Açıklayıcı yorum: Ürün DAL implementasyonu. Nav property olmadığı için beden/stok filtresi
    // ProductStocks DbSet'i üzerinden explicit alt sorgu ile yapılır.
    public class EfProductDal : EfEntityRepositoryBase<Product, DivisimaDbContext>, IProductDal
    {
        public EfProductDal(DivisimaDbContext context) : base(context)
        {
        }

        public async Task<(List<Product> items, int totalCount)> GetListWithFilterAsync(
            int? categoryId, int? subCategoryId, List<string> sizes, List<string> colors,
            decimal? minPrice, decimal? maxPrice, bool? onSale, bool? inStock,
            string sort, int page, int size)
        {
            // Açıklayıcı yorum: Sadece aktif ürünler
            var query = Context.Set<Product>().Where(p => p.is_active).AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(p => p.category_id == categoryId.Value);
            if (subCategoryId.HasValue && subCategoryId.Value > 0)
                query = query.Where(p => p.sub_category_id == subCategoryId.Value);
            if (colors != null && colors.Count > 0)
                query = query.Where(p => colors.Contains(p.color_hex));
            if (minPrice.HasValue)
                query = query.Where(p => p.price >= minPrice.Value);
            if (maxPrice.HasValue)
                query = query.Where(p => p.price <= maxPrice.Value);
            if (onSale.HasValue && onSale.Value)
                query = query.Where(p => p.old_price != null);

            // Açıklayıcı yorum: Beden filtresi - explicit ProductStocks alt sorgusu (nav property yok)
            if (sizes != null && sizes.Count > 0)
            {
                var stockQ = Context.Set<ProductStock>()
                    .Where(s => s.is_active && s.stock_quantity > 0 && sizes.Contains(s.size))
                    .Select(s => s.product_id);
                query = query.Where(p => stockQ.Contains(p.id));
            }

            // Açıklayıcı yorum: Stokta olanlar - herhangi bir bedende stok>0 (explicit alt sorgu)
            if (inStock.HasValue && inStock.Value)
            {
                var inStockQ = Context.Set<ProductStock>()
                    .Where(s => s.is_active && s.stock_quantity > 0)
                    .Select(s => s.product_id);
                query = query.Where(p => inStockQ.Contains(p.id));
            }

            // Açıklayıcı yorum: Sıralama (frontend price-asc/price-desc/new/old)
            query = sort switch
            {
                "price-asc" => query.OrderBy(p => p.price),
                "price-desc" => query.OrderByDescending(p => p.price),
                "old" => query.OrderBy(p => p.id),
                _ => query.OrderByDescending(p => p.id)
            };

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
            return (items, totalCount);
        }
    }
}
