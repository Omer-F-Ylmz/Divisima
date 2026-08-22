using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Shipping;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Shipping;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Kargo yöneticisi. Admin kargo oluşturur (takip no); müşteri takip ettiğinde firma API'sinden
    // güncel durum çekilir ve kayıt normalize edilerek güncellenir. Teslim durumunda sipariş de Delivered olur.
    public class ShipmentManager : IShipmentService
    {
        private readonly IShipmentDal _shipmentDal;
        private readonly IOrderDal _orderDal;
        private readonly ICarrierProvider _carrierProvider;
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly IOrderNotificationService _orderNotificationService;

        public ShipmentManager(IShipmentDal shipmentDal, IOrderDal orderDal, ICarrierProvider carrierProvider,
            IOrderStatusHistoryService statusHistory, IOrderNotificationService orderNotificationService)
        {
            _shipmentDal = shipmentDal;
            _orderDal = orderDal;
            _carrierProvider = carrierProvider;
            _statusHistory = statusHistory;
            _orderNotificationService = orderNotificationService;
        }

        public async Task<(HttpStatusCode, Result)> CreateShipment(ShipmentCreateDto dto)
        {
            var order = await _orderDal.GetAsync(o => o.id == dto.order_id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            if (string.IsNullOrWhiteSpace(dto.tracking_number))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ShipmentTrackingRequired));

            // Açıklayıcı yorum: Sipariş başına tek kargo (idempotent)
            var existing = await _shipmentDal.GetAsync(s => s.order_id == dto.order_id);
            if (existing != null)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.ShipmentAlreadyExists));

            // STATUS GEÇİŞ DOĞRULAMA (OrderStatusMachine - ChangeOrderStatus ile AYNI kural): yalnızca geçerli durumdan
            // Shipped'e geçilebilir (makineye göre Preparing->Shipped). Aksi halde CreateShipment doğrudan status=Shipped
            // yazdığından: Pending (ÖDENMEMİŞ) sipariş kargolanır ya da Cancelled (iade edilmiş) sipariş Shipped'e CANLANIR -> mali kayıp.
            if (!OrderStatusMachine.IsValidTransition((OrderStatusEnum)order.status, OrderStatusEnum.Shipped))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidStatusTransition));

            await _shipmentDal.AddAsync(new Shipment
            {
                order_id = dto.order_id,
                carrier = dto.carrier,
                tracking_number = dto.tracking_number,
                status = (byte)ShipmentStatusEnum.Preparing,
                shipped_at = DateTime.Now,
                estimated_delivery = dto.estimated_delivery,
                created_at = DateTime.Now
            });

            // Açıklayıcı yorum: Sipariş durumunu Kargoda (Shipped) yap (bildirim tetikleyicisi OrderManager'da)
            order.status = (byte)OrderStatusEnum.Shipped;
            await _orderDal.UpdateAsync(order);
            // TUTARLILIK: kargo-kaynaklı durum değişikliği de zaman çizelgesine kaydedilir (ChangeOrderStatus ile aynı).
            // Yoksa müşteri sipariş takibinde "Kargoya verildi" adımını göremezdi (timeline eksik kalırdı).
            await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Shipped, "Kargoya verildi");
            // TUTARLILIK: müşteriye "kargoya verildi" bildirimi (merkezi servis - ChangeOrderStatus ile aynı; onceden ATLANIYORDU).
            await _orderNotificationService.NotifyStatusChangeAsync(order, OrderStatusEnum.Shipped);

            return (HttpStatusCode.OK, new SuccessResult(Messages.ShipmentCreated));
        }

        public async Task<(HttpStatusCode, Result)> TrackByOrder(int orderId, int customerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            // ══ GUVENLIK-FIX (G6) - SAHIPLIK IHLALI DE "BULUNAMADI" ═══════════════════════
            // OLCULEN ONCE-DURUM: baskasinin siparisinin kargosu -> 403 "Bu kargo size ait degil."
            // ama OLMAYAN bir siparisin kargosu -> 404. Iki farkli yanit, kaydin VAR oldugunu
            // dogruluyordu (siparis id'leri ardisik oldugu icin taranabilir de).
            // Deponun KENDI yazili sozlesmesi bunun tersi (OrderManager: "sahiplik ihlali de
            // 'bulunamadi' - varlik sizdirilmaz") ve bu uc o sozlesmenin DISINDA kalmis tek yerdi.
            // Artik iki dal AYNI yaniti veriyor: 404 + Messages.OrderNotFound.
            if (order.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            var shipment = await _shipmentDal.GetAsync(s => s.order_id == orderId);
            if (shipment == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<ShipmentResponseDto>(Messages.ShipmentNotFound));

            // Açıklayıcı yorum: Firma API'sinden güncel durum çek + kaydı güncelle (kısa cache: 15 dk aralıkla)
            if (shipment.last_checked_at == null || shipment.last_checked_at.Value.AddMinutes(15) < DateTime.Now)
            {
                var tracking = await _carrierProvider.TrackAsync(shipment.carrier, shipment.tracking_number);
                if (tracking.Success)
                {
                    shipment.status = tracking.NormalizedStatus;
                    shipment.last_status_text = tracking.RawStatusText;
                    if (tracking.EstimatedDelivery.HasValue) shipment.estimated_delivery = tracking.EstimatedDelivery;
                    if (tracking.DeliveredAt.HasValue) shipment.delivered_at = tracking.DeliveredAt;
                    shipment.last_checked_at = DateTime.Now;

                    // Açıklayıcı yorum: Teslim edildiyse siparişi de güncelle - AMA yalnızca geçerli geçişse (Shipped->Delivered).
                    // Kargo-takip verisi tutarsız/eski olursa (ör. iptal edilmiş siparişte Delivered dönerse) sipariş Delivered'e
                    // YANLIŞ geçmesin diye OrderStatusMachine ile doğrulanır (CreateShipment guard'ıyla tutarlı).
                    if (shipment.status == (byte)ShipmentStatusEnum.Delivered && order.status != (byte)OrderStatusEnum.Delivered
                        && OrderStatusMachine.IsValidTransition((OrderStatusEnum)order.status, OrderStatusEnum.Delivered))
                    {
                        order.status = (byte)OrderStatusEnum.Delivered;
                        // Açıklayıcı yorum: TUTARLILIK - teslim zamanını burada da kaydet (ChangeOrderStatus ile aynı).
                        // Kargo-takipli teslimatta da iade penceresi teslim tarihinden sayılsın (yoksa created_at'e düşerdi).
                        if (!order.delivered_at.HasValue) order.delivered_at = DateTime.Now;
                        await _orderDal.UpdateAsync(order);
                        // TUTARLILIK: kargo-takipli teslimat da zaman çizelgesine kaydedilir (yoksa müşteri "Teslim edildi" adımını göremezdi).
                        await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Delivered, "Kargo teslim edildi");
                        // TUTARLILIK: müşteriye "teslim edildi" bildirimi (merkezi servis; onceden kargo-teslimatta ATLANIYORDU).
                        await _orderNotificationService.NotifyStatusChangeAsync(order, OrderStatusEnum.Delivered);
                    }
                    await _shipmentDal.UpdateAsync(shipment);
                }
            }

            return (HttpStatusCode.OK, new SuccessDataResult<ShipmentResponseDto>(Map(shipment)));
        }

        public async Task<(HttpStatusCode, Result)> GetByOrderForAdmin(int orderId)
        {
            var shipment = await _shipmentDal.GetAsync(s => s.order_id == orderId);
            if (shipment == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<ShipmentResponseDto>(Messages.ShipmentNotFound));
            return (HttpStatusCode.OK, new SuccessDataResult<ShipmentResponseDto>(Map(shipment)));
        }

        private static ShipmentResponseDto Map(Shipment s) => new()
        {
            id = s.id,
            order_id = s.order_id,
            carrier = s.carrier,
            carrier_name = ((CarrierEnum)s.carrier).ToString(),
            tracking_number = s.tracking_number,
            status = s.status,
            status_name = ((ShipmentStatusEnum)s.status).ToString(),
            last_status_text = s.last_status_text,
            shipped_at = s.shipped_at,
            estimated_delivery = s.estimated_delivery,
            delivered_at = s.delivered_at
        };
    }
}
