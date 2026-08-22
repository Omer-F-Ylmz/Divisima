using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Dashboard;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Dashboard/rapor yöneticisi. Sipariş + kalem + ürün + stok verilerini kompoze ederek
    // (Cafixo kalıbı: kompozisyon serviste) admin paneli için istatistik üretir. Ciro = tamamlanmış siparişler
    // (Cancelled hariç); iptal edilen siparişler ciroya dahil edilmez.
    public class DashboardManager : IDashboardService
    {
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IProductDal _productDal;
        private readonly IProductStockDal _productStockDal;
        private readonly ICustomerDal _customerDal;

        private readonly ICategoryDal _categoryDal;
        public DashboardManager(IOrderDal orderDal, IOrderItemDal orderItemDal, IProductDal productDal,
            IProductStockDal productStockDal, ICustomerDal customerDal, ICategoryDal categoryDal)
        {
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _productDal = productDal;
            _productStockDal = productStockDal;
            _customerDal = customerDal;
            _categoryDal = categoryDal;
        }

        // ══ DALGA-2-FIX (B14) - CIRO KURALI ARTIK KOPYALANMIYOR, MERKEZDEN GELIYOR ═══════════
        //
        // ONCEKI HALI: `private static bool IsRevenueOrder(byte status) =>
        //                  status != Cancelled && status != Pending;`
        // Bu, `PaidOrderSpec`in ("TEK DOGRULUK KAYNAGI" - kendi dokumaninda boyle taniyor)
        // KOPYASIYDI ve DISLAMA ile yaziliyordu. OLCULDU: bugun IKISI DE AYNI kumeyi veriyor
        // (Confirmed/Preparing/Shipped/Delivered) - yani BUGUN zarar YOK.
        //
        // GIZLI RISK: kural DISLAMA ile yazildigi icin enum'a eklenecek HER YENI durum ciroya
        // OTOMATIK GIRER. Ornegin bir `Refunded` durumu eklendiginde `PaidOrderSpec` onu dislar
        // (o liste EKLEME ile yazilmis), bu satir ise iceri alirdi - ciro sessizce sisiyordu.
        // Ayni sinifin diger UC sorgusu (69/90/175. satirlar) zaten `PaidOrderSpec` kullaniyordu;
        // yalniz OZET sorgusu ayrisiyordu. Ayrisma bilincli bir karar degil, kapsam disi kalmis
        // bir bosluktu.
        public async Task<(HttpStatusCode, Result)> GetSummary()
        {
            var orders = await _orderDal.GetListAsync(o => true);
            var revenueOrders = orders.Where(o => PaidOrderSpec.IsPaidStatus(o.status)).ToList();

            var totalRevenue = revenueOrders.Sum(o => o.total_price);
            var totalOrders = orders.Count;
            var pendingOrders = orders.Count(o => o.status == (byte)OrderStatusEnum.Pending);
            var avgOrderValue = revenueOrders.Count > 0 ? totalRevenue / revenueOrders.Count : 0m;
            var totalCustomers = (await _customerDal.GetListAsync(c => true)).Count;
            var lowStock = (await _productStockDal.GetListAsync(s => s.stock_quantity <= 5)).Count;

            var dto = new DashboardSummaryDto
            {
                total_revenue = totalRevenue,
                total_orders = totalOrders,
                pending_orders = pendingOrders,
                average_order_value = MoneyHelper.Round(avgOrderValue),
                total_customers = totalCustomers,
                low_stock_count = lowStock
            };
            return (HttpStatusCode.OK, new SuccessDataResult<DashboardSummaryDto>(dto));
        }

        public async Task<(HttpStatusCode, Result)> GetDailySales(DateTime startDate, DateTime endDate)
        {
            // Açıklayıcı yorum: Tarih aralığındaki ciro siparişlerini güne göre grupla
            var orders = await _orderDal.GetListAsync(o =>
                o.created_at >= startDate && o.created_at <= endDate && PaidOrderSpec.PaidStatuses.Contains(o.status));

            var daily = orders
                .GroupBy(o => o.created_at.Date)
                .Select(g => new DailySalesDto
                {
                    date = g.Key,
                    revenue = g.Sum(o => o.total_price),
                    order_count = g.Count()
                })
                .OrderBy(d => d.date)
                .ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<DailySalesDto>>(daily));
        }

        public async Task<(HttpStatusCode, Result)> GetTopProducts(int top)
        {
            if (top <= 0 || top > 100) top = 10;

            // Açıklayıcı yorum: İptal olmayan siparişlerin kalemlerini ürüne göre topla
            var validOrders = await _orderDal.GetListAsync(o => PaidOrderSpec.PaidStatuses.Contains(o.status));
            var validOrderIds = validOrders.Select(o => o.id).ToHashSet();

            // RAPOR FIX (H44): kalem bazında İPTAL EDİLMİŞ satırlar hariç. Sipariş DURUMU filtreleniyordu (satır üstü)
            // ama CancelItem ile tek tek iptal edilen kalemler (sipariş hâlâ aktif/teslim) adet+ciroya SIZIYORDU
            // -> "en çok satan" raporu şişer, yanlış stok/satın-alma kararı verilir. (H41 satıcı-geliri bug'ının aynası.)
            // Ayrıca "i => true" TÜM order_items tablosunu belleğe çekiyordu -> filtre DB tarafına alındı.
            var allItems = await _orderItemDal.GetListNoTrackingAsync(i => !i.is_cancelled);
            var items = allItems.Where(i => validOrderIds.Contains(i.order_id)).ToList();

            var grouped = items
                .GroupBy(i => i.product_id)
                .Select(g => new
                {
                    product_id = g.Key,
                    qty = g.Sum(i => i.quantity),
                    revenue = g.Sum(i => i.unit_price * i.quantity)
                })
                .OrderByDescending(x => x.qty)
                .Take(top)
                .ToList();

            // Açıklayıcı yorum: Ürün adlarını ekle (kompozisyon serviste)
            // N+1 duzeltmesi: tum urun adlarini tek sorguda getir
            var topIds = grouped.Select(x => x.product_id).Distinct().ToList();
            var topProducts = (await _productDal.GetListAsync(p => topIds.Contains(p.id))).ToDictionary(p => p.id, p => p.name);
            var result = new List<TopProductDto>();
            foreach (var x in grouped)
            {
                var product = topProducts.TryGetValue(x.product_id, out var tn) ? tn : null;
                result.Add(new TopProductDto
                {
                    product_id = x.product_id,
                    product_name = product ?? "Bilinmeyen ürün",
                    total_quantity = x.qty,
                    total_revenue = x.revenue
                });
            }
            return (HttpStatusCode.OK, new SuccessDataResult<List<TopProductDto>>(result));
        }

        public async Task<(HttpStatusCode, Result)> GetOrderStatusBreakdown()
        {
            var orders = await _orderDal.GetListAsync(o => true);
            var breakdown = orders
                .GroupBy(o => o.status)
                .Select(g => new OrderStatusBreakdownDto
                {
                    status = g.Key,
                    status_name = ((OrderStatusEnum)g.Key).ToString(),
                    count = g.Count()
                })
                .OrderBy(b => b.status)
                .ToList();
            return (HttpStatusCode.OK, new SuccessDataResult<List<OrderStatusBreakdownDto>>(breakdown));
        }

        public async Task<(HttpStatusCode, Result)> GetLowStock(int threshold)
        {
            if (threshold <= 0) threshold = 5;
            var lowStocks = await _productStockDal.GetListAsync(s => s.stock_quantity <= threshold);

            // N+1 duzeltmesi: tum urun adlarini tek sorguda getir
            var lowIds = lowStocks.Select(s2 => s2.product_id).Distinct().ToList();
            var lowProducts = (await _productDal.GetListAsync(p => lowIds.Contains(p.id))).ToDictionary(p => p.id, p => p.name);
            var result = new List<LowStockDto>();
            foreach (var stock in lowStocks)
            {
                var product = lowProducts.TryGetValue(stock.product_id, out var ln) ? ln : null;
                result.Add(new LowStockDto
                {
                    product_id = stock.product_id,
                    product_name = product ?? "Bilinmeyen ürün",
                    size = stock.size,
                    quantity = stock.stock_quantity
                });
            }
            return (HttpStatusCode.OK, new SuccessDataResult<List<LowStockDto>>(result));
        }

        // Aciklayici yorum: KATEGORI BAZLI SATIS raporu (admin analiz). Iptal olmayan siparislerin kalemlerini
        // urun -> kategori uzerinden gruplar, ciro + adet toplar. N+1 yok: tum urun/kategori tek sorguda.
        public async Task<(HttpStatusCode, Result)> GetSalesByCategory(DateTime startDate, DateTime endDate)
        {
            var orders = await _orderDal.GetListNoTrackingAsync(o =>
                o.created_at >= startDate && o.created_at <= endDate && PaidOrderSpec.PaidStatuses.Contains(o.status));
            var orderIds = orders.Select(o => o.id).ToList();
            if (orderIds.Count == 0)
                return (HttpStatusCode.OK, new SuccessDataResult<List<CategorySalesDto>>(new List<CategorySalesDto>()));

            var items = await _orderItemDal.GetListNoTrackingAsync(i => orderIds.Contains(i.order_id) && !i.is_cancelled);
            var productIds = items.Select(i => i.product_id).Distinct().ToList();
            var products = await _productDal.GetListAsync(p => productIds.Contains(p.id));
            var productCategory = products.ToDictionary(p => p.id, p => p.category_id);

            var categoryIds = products.Select(p => p.category_id).Distinct().ToList();
            var categories = await _categoryDal.GetListAsync(c => categoryIds.Contains(c.id));
            var categoryNames = categories.ToDictionary(c => c.id, c => c.name);

            var report = items
                .Where(i => productCategory.ContainsKey(i.product_id))
                .GroupBy(i => productCategory[i.product_id])
                .Select(g => new CategorySalesDto
                {
                    category_id = g.Key,
                    category_name = categoryNames.TryGetValue(g.Key, out var cn) ? cn : "Bilinmeyen",
                    revenue = g.Sum(i => i.unit_price * i.quantity),
                    units_sold = g.Sum(i => i.quantity)
                })
                .OrderByDescending(c => c.revenue)
                .ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<CategorySalesDto>>(report));
        }

    }
}
