using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Recommendation;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Son görüntülenen ürünler iş kuralları. Ürün başına tek satır (upsert), son N ile sınırlı.
    public class RecentlyViewedManager : IRecentlyViewedService
    {
        private readonly IRecentlyViewedProductDal _recentDal;
        private readonly IProductDal _productDal;
        private readonly ICategoryDal _categoryDal;

        // Açıklayıcı yorum: Müşteri başına saklanan azami görüntüleme (eskiler budanır)
        private const int MaxPerCustomer = 30;

        public RecentlyViewedManager(IRecentlyViewedProductDal recentDal, IProductDal productDal, ICategoryDal categoryDal)
        {
            _recentDal = recentDal;
            _productDal = productDal;
            _categoryDal = categoryDal;
        }

        public async Task<(HttpStatusCode, Result)> RecordView(int customerId, int productId)
        {
            var product = await _productDal.GetAsync(p => p.id == productId && p.is_active);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Açıklayıcı yorum: Upsert - zaten varsa viewed_at güncelle, yoksa ekle
            var existing = await _recentDal.GetAsync(r => r.customer_id == customerId && r.product_id == productId);
            if (existing != null)
            {
                existing.viewed_at = DateTime.Now;
                await _recentDal.UpdateAsync(existing);
            }
            else
            {
                // Açıklayıcı yorum: Eşzamanlı iki istek aynı anda ekleme yapabilir - unique index (customer_id, product_id)
                // ihlalini yakala; yarışı kaybeden taraf ekleme yerine güncellemeye düşer (race-safe upsert).
                try
                {
                    await _recentDal.AddAsync(new RecentlyViewedProduct
                    {
                        customer_id = customerId,
                        product_id = productId,
                        viewed_at = DateTime.Now
                    });
                }
                catch
                {
                    var raced = await _recentDal.GetAsync(r => r.customer_id == customerId && r.product_id == productId);
                    if (raced != null)
                    {
                        raced.viewed_at = DateTime.Now;
                        await _recentDal.UpdateAsync(raced);
                    }
                    return (HttpStatusCode.OK, new SuccessResult(Messages.RecentlyViewedRecorded));
                }

                // Açıklayıcı yorum: Azami sınır aşıldıysa en eskileri buda (liste şişmesin)
                var all = await _recentDal.GetListAsync(r => r.customer_id == customerId);
                if (all.Count > MaxPerCustomer)
                {
                    var toRemove = all.OrderByDescending(r => r.viewed_at).Skip(MaxPerCustomer).ToList();
                    foreach (var old in toRemove)
                        await _recentDal.DeleteAsync(old);
                }
            }

            return (HttpStatusCode.OK, new SuccessResult(Messages.RecentlyViewedRecorded));
        }

        public async Task<(HttpStatusCode, Result)> GetRecentlyViewed(int customerId, int limit = 10)
        {
            if (limit <= 0 || limit > 30) limit = 10;

            var recent = await _recentDal.GetListNoTrackingAsync(r => r.customer_id == customerId);
            var orderedIds = recent
                .OrderByDescending(r => r.viewed_at)
                .Take(limit)
                .Select(r => r.product_id)
                .ToList();

            if (orderedIds.Count == 0)
                return (HttpStatusCode.OK, new SuccessDataResult<List<RecommendedProductDto>>(new List<RecommendedProductDto>()));

            // Açıklayıcı yorum: Ürünleri + kategori adlarını toplu çek (N+1 önleme), görüntüleme sırasını koru
            var products = await _productDal.GetListNoTrackingAsync(p => orderedIds.Contains(p.id) && p.is_active);
            var catIds = products.Select(p => p.category_id).Distinct().ToList();
            var cats = await _categoryDal.GetListNoTrackingAsync(c => catIds.Contains(c.id));
            var catNames = cats.ToDictionary(c => c.id, c => c.name);

            var map = products.ToDictionary(p => p.id, p => new RecommendedProductDto
            {
                id = p.id,
                name = p.name,
                price = p.price,
                old_price = p.old_price,
                image_url = p.image_url,
                category_name = catNames.TryGetValue(p.category_id, out var cn) ? cn : null
            });

            var result = new List<RecommendedProductDto>();
            foreach (var id in orderedIds)
                if (map.TryGetValue(id, out var d)) result.Add(d);

            return (HttpStatusCode.OK, new SuccessDataResult<List<RecommendedProductDto>>(result));
        }
    }
}
