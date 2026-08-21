using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Vitrin listeleri. Sipariş kalemlerinden agregasyon (N+1 önlemeli - toplu çekim + in-memory grupla).
    public class MerchandisingManager : IMerchandisingService
    {
        private readonly IOrderItemDal _orderItemDal;
        private readonly IOrderDal _orderDal;
        private readonly IProductDal _productDal;
        private readonly ICacheService _cache;

        private const int TrendingWindowDays = 30;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public MerchandisingManager(IOrderItemDal orderItemDal, IOrderDal orderDal, IProductDal productDal, ICacheService cache)
        {
            _orderItemDal = orderItemDal;
            _orderDal = orderDal;
            _productDal = productDal;
            _cache = cache;
        }

        public async Task<(HttpStatusCode, Result)> GetBestSellers(int take)
        {
            if (take <= 0 || take > 50) take = 12;
            // Açıklayıcı yorum: Cache-aside - pahalı agregasyon 10 dk cache'lenir (liste yavaş değişir)
            var cached = await _cache.GetOrSetAsync($"merch:bestsellers:{take}", async () =>
            {
                return await ComputeBestSellers(take);
            }, CacheTtl);
            return (HttpStatusCode.OK, new SuccessDataResult<List<Product>>(cached));
        }

        private async Task<List<Product>> ComputeBestSellers(int take)
        {
            // SIRALAMA MANIPULASYONU FIX (H45b): filtre YOKTU -> odenmemis (Pending) ve IPTAL siparisler + tek tek
            // iptal edilmis kalemler VITRIN "en cok satan" listesini sisiriyordu. Biri odeme yapmadan siparis acarak
            // kendi urununu vitrinin tepesine tasiyabilirdi. Artik SADECE odenmis siparis + iptal-edilmemis kalem sayilir.
            // (H41 satici-geliri / H45 admin-raporu ile ayni kural: order.status VE item.is_cancelled birlikte filtrelenir.)
            var paidOrders = await _orderDal.GetListNoTrackingAsync(o =>
                PaidOrderSpec.PaidStatuses.Contains(o.status));
            var paidIds = paidOrders.Select(o => o.id).ToHashSet();
            var items = await _orderItemDal.GetListNoTrackingAsync(i => !i.is_cancelled);
            var ranked = items.Where(i => paidIds.Contains(i.order_id))
                .GroupBy(i => i.product_id)
                .Select(g => new { product_id = g.Key, qty = g.Sum(x => x.quantity) })
                .OrderByDescending(x => x.qty)
                .Take(take).ToList();
            var products = await LoadProductsAsync(ranked.Select(r => r.product_id).ToList());
            // Açıklayıcı yorum: Satış sırasını koru
            return ranked.Select(r => products.FirstOrDefault(p => p.id == r.product_id)).Where(p => p != null).ToList();
        }

        public async Task<(HttpStatusCode, Result)> GetTrending(int take)
        {
            if (take <= 0 || take > 50) take = 12;
            var cached = await _cache.GetOrSetAsync($"merch:trending:{take}", async () => await ComputeTrending(take), CacheTtl);
            return (HttpStatusCode.OK, new SuccessDataResult<List<Product>>(cached));
        }

        private async Task<List<Product>> ComputeTrending(int take)
        {
            var cutoff = DateTime.Now.AddDays(-TrendingWindowDays);
            // Açıklayıcı yorum: Son penceredeki siparişlerin id'leri -> o siparişlerin kalemleri
            // SIRALAMA MANIPULASYONU FIX (H45b): "trend" listesi kisa pencereli oldugu icin manipulasyona EN ACIK yerdi -
            // odenmemis/iptal siparisler haric tutulur (aksi halde birkac odenmemis siparisle urun trend'e sokulabilirdi).
            var recentOrders = await _orderDal.GetListNoTrackingAsync(o => o.created_at >= cutoff
                && PaidOrderSpec.PaidStatuses.Contains(o.status));
            var orderIds = recentOrders.Select(o => o.id).ToHashSet();
            if (orderIds.Count == 0) return new List<Product>();

            var items = await _orderItemDal.GetListNoTrackingAsync(i => !i.is_cancelled);
            var ranked = items.Where(i => orderIds.Contains(i.order_id))
                .GroupBy(i => i.product_id)
                .Select(g => new { product_id = g.Key, qty = g.Sum(x => x.quantity) })
                .OrderByDescending(x => x.qty)
                .Take(take).ToList();
            var products = await LoadProductsAsync(ranked.Select(r => r.product_id).ToList());
            return ranked.Select(r => products.FirstOrDefault(p => p.id == r.product_id)).Where(p => p != null).ToList();
        }

        public async Task<(HttpStatusCode, Result)> GetNewArrivals(int take)
        {
            if (take <= 0 || take > 50) take = 12;
            var cached = await _cache.GetOrSetAsync($"merch:newarrivals:{take}", async () =>
            {
                var all = await _productDal.GetListNoTrackingAsync(p => p.is_active);
                return all.OrderByDescending(p => p.id).Take(take).ToList();
            }, CacheTtl);
            return (HttpStatusCode.OK, new SuccessDataResult<List<Product>>(cached));
        }

        // Açıklayıcı yorum: Ürünleri tek sorguda topluca çek (N+1 önleme)
        private async Task<List<Product>> LoadProductsAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return new List<Product>();
            var products = await _productDal.GetListNoTrackingAsync(p => ids.Contains(p.id) && p.is_active);
            return products.ToList();
        }
    }
}
