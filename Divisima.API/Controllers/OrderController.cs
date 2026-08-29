using System.Net;
using Divisima.API.Filters;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Order;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Sipariş controller'ı. Thin. Müşteri sipariş verir/görür, admin durum değiştirir.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Sipariş oluşturma ve yönetimi")]
    public class OrderController : SecureControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ICurrentUserService _currentUser;
        private readonly IOrderStatusHistoryService _statusHistory;

        public OrderController(IOrderService orderService, ICurrentUserService currentUser, IOrderStatusHistoryService statusHistory)
        {
            _orderService = orderService;
            _currentUser = currentUser;
            _statusHistory = statusHistory;
        }

        // Açıklayıcı yorum: Sipariş oluştur (müşteri - checkout)
        [Idempotency]
        [HttpPost("place")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Sipariş oluştur", Description = "Sepeti siparişe çevirir: stok kontrol, indirim, kargo, snapshot. Müşteri yetkisi gerekir.")]
        // MFIX-B / K3: yanit artik { id, order_number } tasiyor - istemci gercek siparis numarasini
        // EK BIR CAGRI YAPMADAN gosterebilir (misafir yolunda /api/order/get anonime KAPALI oldugu
        // icin numara hic alinamiyordu). `id` KALDI: payment/initialize ve order/get onu kullanir.
        [ProducesResponseType(typeof(SuccessDataResult<OrderPlaceResponseDto>), (int)HttpStatusCode.Created)]
        // MFIX-B / K2: gecersiz kupon artik SESSIZCE yok sayilmaz, sebebiyle 400 doner.
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Place([FromBody] OrderCreateRequestDto dto)
        {
            // Açıklayıcı yorum: Sipariş sahibi token'dan (istemci başkası adına sipariş veremez)
            dto.customer_id = _currentUser.GetRequiredUserId();
            var result = await _orderService.PlaceOrder(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Sipariş durumu değiştir (admin)
        [HttpPatch("status")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Sipariş durumu değiştir", Description = "Siparişi onayla/kargola/teslim et. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ChangeStatus([FromBody] OrderStatusChangeRequestDto dto)
        {
            var result = await _orderService.ChangeOrderStatus(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Sipariş detayı (müşteri)
        [HttpGet("get/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Sipariş detayı", Description = "Siparişi kalemleriyle getirir.")]
        [ProducesResponseType(typeof(SuccessDataResult<OrderDetailResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _orderService.GetById(id, _currentUser.GetRequiredUserId());
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Müşterinin siparişleri (hesabım - siparişlerim)
        [HttpGet("my-orders")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Müşteri siparişleri", Description = "Müşterinin tüm siparişlerini listeler.")]
        [ProducesResponseType(typeof(SuccessDataResult<List<OrderListResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetByCustomer()
        {
            var result = await _orderService.GetByCustomer(_currentUser.GetRequiredUserId());
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Siparişin durum zaman çizelgesi (müşteri takip - IDOR korumalı)
        [HttpGet("timeline/{orderId:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Sipariş zaman çizelgesi", Description = "Siparişin durum geçmişini (Beklemede->Onaylandı->Kargoda->Teslim) sırayla döner.")]
        public async Task<IActionResult> Timeline(int orderId)
        {
            var result = await _statusHistory.GetTimeline(orderId, _currentUser.GetRequiredUserId());
            return StatusCode((int)result.Item1, result.Item2);
        }
        [HttpPost("admin/list")]
        [RequireUserType(UserTypeEnum.Admin)]
        [Swashbuckle.AspNetCore.Annotations.SwaggerOperation(Summary = "Tüm siparişler (admin, filtre+sayfalama)")]
        public async Task<IActionResult> AdminList([FromBody] Divisima.Entity.Dtos.Order.AdminOrderFilterDto filter)
        {
            var result = await _orderService.GetAllForAdmin(filter);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPost("{orderId:int:min(1)}/cancel-item/{orderItemId:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Kalem iptali (kısmi)", Description = "Siparişten tek kalemi iptal eder; stok iade + tutar kadar mağaza kredisi.")]
        public async Task<IActionResult> CancelItem(int orderId, int orderItemId)
        {
            var r = await _orderService.CancelItem(orderId, orderItemId, _currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("{orderId:int:min(1)}/estimated-delivery")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Tahmini teslim tarihi")]
        public async Task<IActionResult> EstimatedDelivery(int orderId)
        {
            var r = await _orderService.GetEstimatedDelivery(orderId, _currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }


        // Havale/EFT ödemesi onayı (admin) - para hesaba geçince
        [HttpPost("confirm-manual-payment/{orderId}")]
        [RequireUserType(UserTypeEnum.Admin)]
        public async Task<IActionResult> ConfirmManualPayment(int orderId)
        {
            var r = await _orderService.ConfirmManualPayment(orderId);
            return StatusCode((int)r.Item1, r.Item2);
        }


        // FATURA GORUNUMU (MANTIK-FIX-2R / K2).
        // Onceden bu uc SUNUCUDA URETILMIS HTML donduruyordu ve belgeyi SIPARIS verisinden
        // YENIDEN HESAPLIYORDU (sabit /1.20 matrah, sabit "KDV (%20)" etiketi, sunucuda
        // bicimlenmis para). Artik KAYITTAN (invoices + invoice_items) yapilandirilmis HAM
        // veri doner; bicimleme ve etiketler ISTEMCIDE (sozluk + dvsLocale).
        // ROTA BILEREK DEGISTIRILMEDI: uc YERINDE evrildi, paralel ikinci fatura ucu
        // ACILMADI ve istemci TEK uca bagli kaldi. (Yol adindaki "-html" artik yaniti
        // tarif etmiyor; adlandirma ayri ve kozmetik bir karardir, raporda not dusuldu.)
        [HttpGet("{orderId}/invoice-html")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> InvoiceHtml(int orderId)
        {
            var r = await _orderService.GetInvoiceView(orderId, _currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }

    }
}
