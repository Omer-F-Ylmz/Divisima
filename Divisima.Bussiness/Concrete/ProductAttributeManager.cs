using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.ProductAttribute;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün özellik + faceted search iş kuralları. Toplu çekim + in-memory grupla (N+1 önleme).
    public class ProductAttributeManager : IProductAttributeService
    {
        private readonly IProductAttributeDal _attrDal;
        private readonly IProductDal _productDal;

        public ProductAttributeManager(IProductAttributeDal attrDal, IProductDal productDal)
        {
            _attrDal = attrDal;
            _productDal = productDal;
        }

        public async Task<(HttpStatusCode, Result)> SetAttributes(SetAttributesDto dto)
        {
            var product = await _productDal.GetAsync(p => p.id == dto.product_id);
            if (product == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Mevcut özellikleri pasifle, gelenleri yeniden yaz (idempotent güncelleme)
            var current = await _attrDal.GetListAsync(a => a.product_id == dto.product_id && a.is_active);
            foreach (var c in current) { c.is_active = false; await _attrDal.UpdateAsync(c); }

            if (dto.attributes != null)
            {
                foreach (var pair in dto.attributes)
                {
                    if (string.IsNullOrWhiteSpace(pair.key) || string.IsNullOrWhiteSpace(pair.value)) continue;
                    await _attrDal.AddAsync(new ProductAttribute
                    {
                        product_id = dto.product_id,
                        attribute_key = pair.key.Trim().ToLowerInvariant(),
                        attribute_value = pair.value.Trim().ToLowerInvariant(),
                        is_active = true,
                        created_at = DateTime.Now
                    });
                }
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.AttributesUpdated));
        }

        public async Task<(HttpStatusCode, Result)> GetAttributes(int productId)
        {
            var list = await _attrDal.GetListNoTrackingAsync(a => a.product_id == productId && a.is_active);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductAttribute>>(list.ToList()));
        }

        public async Task<(HttpStatusCode, Result)> GetFacets()
        {
            // Açıklayıcı yorum: Aktif ürünlerin özelliklerinden facet ağacı - anahtar -> (değer -> ürün sayısı)
            var activeProductIds = (await _productDal.GetListNoTrackingAsync(p => p.is_active)).Select(p => p.id).ToHashSet();
            var attrs = await _attrDal.GetListNoTrackingAsync(a => a.is_active);
            var facets = attrs.Where(a => activeProductIds.Contains(a.product_id))
                .GroupBy(a => a.attribute_key)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.attribute_value)
                          .ToDictionary(vg => vg.Key, vg => vg.Select(x => x.product_id).Distinct().Count()));
            return (HttpStatusCode.OK, new SuccessDataResult<Dictionary<string, Dictionary<string, int>>>(facets));
        }

        public async Task<(HttpStatusCode, Result)> FilterByAttributes(FacetFilterDto dto)
        {
            if (dto.filters == null || dto.filters.Count == 0)
            {
                var all = await _productDal.GetListNoTrackingAsync(p => p.is_active);
                return (HttpStatusCode.OK, new SuccessDataResult<List<Product>>(all.ToList()));
            }

            // Açıklayıcı yorum: Filtreleri anahtar bazında grupla; her anahtar için değer kümesi (aynı anahtarda VEYA, anahtarlar arası VE)
            var byKey = dto.filters.Where(f => !string.IsNullOrWhiteSpace(f.key) && !string.IsNullOrWhiteSpace(f.value))
                .GroupBy(f => f.key.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Select(x => x.value.Trim().ToLowerInvariant()).ToHashSet());

            var attrs = await _attrDal.GetListNoTrackingAsync(a => a.is_active);
            // Açıklayıcı yorum: Her ürün için sahip olduğu (anahtar->değerler)
            var perProduct = attrs.GroupBy(a => a.product_id)
                .ToDictionary(g => g.Key, g => g.GroupBy(x => x.attribute_key)
                    .ToDictionary(kg => kg.Key, kg => kg.Select(x => x.attribute_value).ToHashSet()));

            // Açıklayıcı yorum: Tüm anahtar filtrelerini karşılayan ürünler (her anahtarda en az bir değer eşleşmeli)
            var matchingIds = perProduct.Where(kv =>
                byKey.All(fk => kv.Value.TryGetValue(fk.Key, out var vals) && vals.Overlaps(fk.Value)))
                .Select(kv => kv.Key).ToHashSet();

            var products = await _productDal.GetListNoTrackingAsync(p => p.is_active && matchingIds.Contains(p.id));
            return (HttpStatusCode.OK, new SuccessDataResult<List<Product>>(products.ToList()));
        }
    }
}
