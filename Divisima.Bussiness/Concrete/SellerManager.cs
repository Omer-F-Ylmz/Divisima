using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Orders;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Seller;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Satıcı paneli iş katmanı. KRİTİK İZOLASYON: her sorgu seller_id == sellerId ile filtrelenir;
    // sellerId controller'da JWT'den (CurrentSellerId) gelir, ASLA client'tan. Bir satıcı başkasının ürün/satış/gelirini göremez.
    // KRİTİK GELİR BÜTÜNLÜĞÜ (H41): gelir YALNIZ ÖDENMİŞ siparişlerden sayılır (Confirmed/Preparing/Shipped/Delivered).
    // Pending=ödenmemiş (online ödeme callback beklemede) ve Cancelled=iptal edilen siparişler gelire GİRMEZ.
    // Not: tam sipariş iptali OrderItem.is_cancelled'ı set etmez (o bayrak kısmi item-iptali içindir) -> sipariş
    // DURUMUNA göre filtrelemek şarttır, yoksa iptal+ödenmemiş siparişler geliri şişirir.
    public class SellerManager : ISellerService
    {
        // Ödenmiş/gerçekleşmiş sayılan sipariş durumları (gelir bunlardan hesaplanır)
        // H47: yerel kopya KALDIRILDI - kural artik tek yerde: PaidOrderSpec (Core/Utilities/Orders).
private readonly ISellerDal _sellerDal;
        private readonly IProductDal _productDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IOrderDal _orderDal;

        public SellerManager(ISellerDal sellerDal, IProductDal productDal, IOrderItemDal orderItemDal, IOrderDal orderDal)
        {
            _sellerDal = sellerDal;
            _productDal = productDal;
            _orderItemDal = orderItemDal;
            _orderDal = orderDal;
        }

        // Açıklayıcı yorum: Satıcı paneli özeti - "neyi nasıl satıyorum" tek bakışta (yalnız oturumdaki satıcı, yalnız ödenmiş siparişler).
        public async Task<(HttpStatusCode, Result)> GetDashboardAsync(int sellerId)
        {
            var seller = await _sellerDal.GetAsync(s => s.id == sellerId);
            if (seller == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.SellerNotFound));
            // GÜVENLİK: askıya alınmış/pasif satıcı erişemez (login sonrası suspend edilse token hâlâ geçerli olabilir - her istekte DB doğrula)
            if (!seller.is_active || seller.status == (byte)SellerStatusEnum.Suspended)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.SellerSuspended));

            var products = await _productDal.GetListNoTrackingAsync(p => p.seller_id == sellerId);
            var allItems = await _orderItemDal.GetListNoTrackingAsync(oi => oi.seller_id == sellerId);

            // Sipariş durumlarını çek + ÖDENMİŞ sipariş id kümesi (gelir yalnız bunlardan)
            var orderIds = allItems.Select(i => i.order_id).Distinct().ToList();
            var orders = await _orderDal.GetListNoTrackingAsync(o => orderIds.Contains(o.id));
            var paidOrderIds = orders.Where(o => PaidOrderSpec.PaidStatuses.Contains(o.status)).Select(o => o.id).ToHashSet();

            // Gelir: sipariş ÖDENMİŞ + kalem iptal-edilmemiş (kısmi iptal hariç)
            var revenueItems = allItems.Where(i => !i.is_cancelled && paidOrderIds.Contains(i.order_id)).ToList();
            var gross = revenueItems.Sum(i => i.unit_price * i.quantity);
            var commission = Math.Round(gross * seller.commission_rate / 100m, 2, MidpointRounding.AwayFromZero);

            // Kargolanmayı bekleyen: ödenmiş ama henüz kargolanmamış (Confirmed/Preparing)
            var pendingShipment = orders.Count(o => o.status == (byte)OrderStatusEnum.Confirmed
                                                 || o.status == (byte)OrderStatusEnum.Preparing);

            var dto = new SellerDashboardResponseDto
            {
                total_products = products.Count,
                active_products = products.Count(p => p.is_active),
                total_orders = revenueItems.Select(i => i.order_id).Distinct().Count(), // yalnız ödenmiş/gerçekleşmiş sipariş
                total_units_sold = revenueItems.Sum(i => i.quantity),
                gross_revenue = gross,
                commission_total = commission,
                net_revenue = gross - commission,
                pending_shipment_count = pendingShipment
            };
            return (HttpStatusCode.OK, new SuccessDataResult<SellerDashboardResponseDto>(dto, Messages.SellerDashboardListed));
        }

        // Açıklayıcı yorum: Satıcının ürünleri + her ürünün satış performansı (yalnız ödenmiş siparişlerden gelir).
        public async Task<(HttpStatusCode, Result)> GetMyProductsAsync(int sellerId)
        {
            var seller = await _sellerDal.GetAsync(s => s.id == sellerId);
            if (seller == null || !seller.is_active || seller.status == (byte)SellerStatusEnum.Suspended)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.SellerSuspended));

            var products = await _productDal.GetListNoTrackingAsync(p => p.seller_id == sellerId);
            var allItems = await _orderItemDal.GetListNoTrackingAsync(oi => oi.seller_id == sellerId && !oi.is_cancelled);

            // Sipariş durumu filtresi: ürün başına satış YALNIZ ödenmiş siparişlerden
            var orderIds = allItems.Select(i => i.order_id).Distinct().ToList();
            var orders = await _orderDal.GetListNoTrackingAsync(o => orderIds.Contains(o.id));
            var paidOrderIds = orders.Where(o => PaidOrderSpec.PaidStatuses.Contains(o.status)).Select(o => o.id).ToHashSet();
            var items = allItems.Where(i => paidOrderIds.Contains(i.order_id)).ToList();

            // Ürün başına satış: bellekte grupla (satıcı ölçeğinde makul; büyük ölçekte DAL'da GROUP BY'a taşınabilir)
            var soldByProduct = items.GroupBy(i => i.product_id)
                .ToDictionary(g => g.Key, g => (units: g.Sum(x => x.quantity), rev: g.Sum(x => x.unit_price * x.quantity)));

            var list = products.Select(p =>
            {
                soldByProduct.TryGetValue(p.id, out var s);
                return new SellerProductResponseDto
                {
                    id = p.id,
                    name = p.name,
                    price = p.price,
                    is_active = p.is_active,
                    units_sold = s.units,
                    revenue = s.rev
                };
            }).ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<SellerProductResponseDto>>(list, Messages.ProductListed));
        }

        // Açıklayıcı yorum: Satıcının satış kalemleri (yalnız GERÇEKLEŞEN satışlar = ödenmiş siparişler, en yeni önce).
        public async Task<(HttpStatusCode, Result)> GetMySalesAsync(int sellerId)
        {
            var seller = await _sellerDal.GetAsync(s => s.id == sellerId);
            if (seller == null || !seller.is_active || seller.status == (byte)SellerStatusEnum.Suspended)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.SellerSuspended));

            var allItems = await _orderItemDal.GetListNoTrackingAsync(oi => oi.seller_id == sellerId);
            // YALNIZ ödenmiş siparişlerin kalemleri (Pending/Cancelled satış değildir)
            var orderIds = allItems.Select(i => i.order_id).Distinct().ToList();
            var orders = await _orderDal.GetListNoTrackingAsync(o => orderIds.Contains(o.id));
            var paidOrderIds = orders.Where(o => PaidOrderSpec.PaidStatuses.Contains(o.status)).Select(o => o.id).ToHashSet();

            var list = allItems
                .Where(i => paidOrderIds.Contains(i.order_id))
                .OrderByDescending(i => i.created_at)
                .Select(i => new SellerSaleItemResponseDto
                {
                    order_id = i.order_id,
                    product_id = i.product_id,
                    size = i.size,
                    quantity = i.quantity,
                    unit_price = i.unit_price,
                    line_total = i.unit_price * i.quantity,
                    is_cancelled = i.is_cancelled,
                    created_at = i.created_at
                }).ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<List<SellerSaleItemResponseDto>>(list, Messages.SellerDashboardListed));
        }
    }
}
