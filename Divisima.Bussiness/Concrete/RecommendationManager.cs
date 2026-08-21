using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Recommendation;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Öneri motoru. Nav property yok - kompozisyonla (OrderItem + Product + Category DAL).
    public class RecommendationManager : IRecommendationService
    {
        private readonly IOrderItemDal _orderItemDal;
        private readonly IProductDal _productDal;
        private readonly ICategoryDal _categoryDal;

        private readonly IOrderDal _orderDal;
        public RecommendationManager(IOrderItemDal orderItemDal, IProductDal productDal, ICategoryDal categoryDal, IOrderDal orderDal)
        {
            _orderItemDal = orderItemDal;
            _orderDal = orderDal;
            _productDal = productDal;
            _categoryDal = categoryDal;
        }

        // Açıklayıcı yorum: "Bunu alanlar şunu da aldı"
        public async Task<(HttpStatusCode, Result)> GetFrequentlyBoughtTogether(int productId, int limit = 8)
        {
            if (limit <= 0 || limit > 20) limit = 8;

            // 1) Bu ürünü içeren sipariş id'leri
            // ONERI MANIPULASYONU FIX (H46): filtre YOKTU -> odenmemis (Pending) ve IPTAL siparisler + iptal kalemler
            // "bunu alanlar sunu da aldi" onerisine giriyordu. Biri odeme yapmadan siparis acip kendi urununu
            // populer bir urunun yanina ilistirebilirdi (herkese acik oneri manipulasyonu) + oneri kalitesi bozuluyordu.
            // (H41 satici-geliri / H45 admin-raporu / H45b vitrin ile AYNI kural: order.status VE item.is_cancelled.)
            var paidOrders = await _orderDal.GetListNoTrackingAsync(o =>
                PaidOrderSpec.PaidStatuses.Contains(o.status));
            var paidIds = paidOrders.Select(o => o.id).ToHashSet();
            var itemsWithProduct = await _orderItemDal.GetListNoTrackingAsync(oi => oi.product_id == productId && !oi.is_cancelled);
            var orderIds = itemsWithProduct.Where(oi => paidIds.Contains(oi.order_id))
                                           .Select(oi => oi.order_id).Distinct().ToList();
            if (orderIds.Count == 0)
                return (HttpStatusCode.OK, new SuccessDataResult<List<RecommendedProductDto>>(new List<RecommendedProductDto>()));

            // 2) O siparişlerdeki DİĞER ürünler (kendisi hariç)
            var coItems = await _orderItemDal.GetListNoTrackingAsync(oi => orderIds.Contains(oi.order_id) && oi.product_id != productId && !oi.is_cancelled);

            // 3) Ürüne göre grupla + sıklığa göre sırala
            var ranked = coItems
                .GroupBy(oi => oi.product_id)
                .Select(g => new { product_id = g.Key, freq = g.Count() })
                .OrderByDescending(x => x.freq)
                .Take(limit)
                .ToList();

            var rankedIds = ranked.Select(r => r.product_id).ToList();
            var dto = await BuildDtoListAsync(rankedIds, rankedIds);
            return (HttpStatusCode.OK, new SuccessDataResult<List<RecommendedProductDto>>(dto));
        }

        // Açıklayıcı yorum: "Benzer ürünler" - aynı kategori
        public async Task<(HttpStatusCode, Result)> GetSimilarProducts(int productId, int limit = 8)
        {
            if (limit <= 0 || limit > 20) limit = 8;

            var product = await _productDal.GetAsync(p => p.id == productId);
            if (product == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

            // Aynı kategorideki diğer aktif ürünler (kendisi hariç)
            var similar = await _productDal.GetListNoTrackingAsync(p =>
                p.category_id == product.category_id && p.id != productId && p.is_active);

            var ids = similar.Take(limit).Select(p => p.id).ToList();
            var dto = await BuildDtoListAsync(ids, ids);
            return (HttpStatusCode.OK, new SuccessDataResult<List<RecommendedProductDto>>(dto));
        }

        // Açıklayıcı yorum: Ürün id listesinden DTO listesi kur (sıra korunur). Kategori adı tek sözlükle doldurulur (N+1 önleme).
        private async Task<List<RecommendedProductDto>> BuildDtoListAsync(List<int> productIds, List<int> orderPreserve)
        {
            if (productIds.Count == 0) return new List<RecommendedProductDto>();

            var products = await _productDal.GetListNoTrackingAsync(p => productIds.Contains(p.id) && p.is_active);

            // Kategori adlarını tek seferde çek (N+1 önleme)
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

            // Sıralamayı koru (sıklık/kategori sırası)
            var result = new List<RecommendedProductDto>();
            foreach (var id in orderPreserve)
                if (map.TryGetValue(id, out var d)) result.Add(d);
            return result;
        }
    }
}
