using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Comparison;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Ürün karşılaştırma - 2-4 ürünü özellikleriyle yan yana. Toplu çekim (N+1 önleme).
    public class ProductComparisonManager : IProductComparisonService
    {
        private readonly IProductDal _productDal;
        private readonly IProductAttributeDal _attrDal;

        public ProductComparisonManager(IProductDal productDal, IProductAttributeDal attrDal)
        {
            _productDal = productDal;
            _attrDal = attrDal;
        }

        public async Task<(HttpStatusCode, Result)> Compare(CompareRequestDto dto)
        {
            // Açıklayıcı yorum: 2-4 ürün sınırı (karşılaştırma UI'si için makul)
            if (dto.product_ids == null || dto.product_ids.Count < 2 || dto.product_ids.Count > 4)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CompareInvalidCount));

            var ids = dto.product_ids.Distinct().ToList();
            var products = (await _productDal.GetListNoTrackingAsync(p => ids.Contains(p.id) && p.is_active)).ToList();
            if (products.Count < 2)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CompareNotEnoughProducts));

            // Açıklayıcı yorum: Tüm özellikleri tek sorguda çek, ürün bazında sözlükle
            var attrs = await _attrDal.GetListNoTrackingAsync(a => ids.Contains(a.product_id) && a.is_active);
            var byProduct = products.ToDictionary(
                p => p.id,
                p => attrs.Where(a => a.product_id == p.id)
                          .GroupBy(a => a.attribute_key)
                          .ToDictionary(g => g.Key, g => g.First().attribute_value));

            // Açıklayıcı yorum: Tablo satırları için tüm anahtarların birleşimi
            var allKeys = attrs.Select(a => a.attribute_key).Distinct().OrderBy(k => k).ToList();

            var result = new ComparisonResultDto
            {
                products = products,
                attributes = byProduct,
                attribute_keys = allKeys
            };
            return (HttpStatusCode.OK, new SuccessDataResult<ComparisonResultDto>(result));
        }
    }
}
