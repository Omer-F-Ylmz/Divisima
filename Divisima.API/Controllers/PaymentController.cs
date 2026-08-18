using System.Net;
using System.Security.Claims;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ödeme controller'ı. Sahiplik JWT'den doğrulanır; callback+webhook anonim ama imzalı.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Güvenli Iyzico ödeme")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        public PaymentController(IPaymentService paymentService) { _paymentService = paymentService; }

        // Açıklayıcı yorum: JWT'deki gerçek kullanıcı id (sahiplik kontrolü buradan gelir, client'tan değil)
        private int CurrentCustomerId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpPost("initialize")]
        [EnableRateLimiting("payment")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Ödeme başlat", Description = "Checkout Form başlatır. Kullanıcı yalnızca kendi siparişini ödeyebilir.")]
        public async Task<IActionResult> Initialize([FromBody] PaymentInitRequestDto dto)
        {
            // Açıklayıcı yorum: authenticatedCustomerId JWT'den - IDOR engeli
            var r = await _paymentService.Initialize(dto, CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Açıklayıcı yorum: 3DS callback - Iyzico POST eder (anonim ama imza doğrulanır)
        [HttpPost("callback")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ödeme callback", Description = "Iyzico callback. İmza + sunucu-sunucu doğrulama ile işlenir.")]
        public async Task<IActionResult> Callback([FromForm] PaymentCallbackRequestDto dto)
        {
            var r = await _paymentService.HandleCallback(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Açıklayıcı yorum: Webhook - Iyzico'nun bant-dışı bildirimi (callback kaybolursa yedek teyit).
        // Aynı güvenli HandleCallback mantığını kullanır; idempotent olduğundan çift işlem güvenli.
        [HttpPost("webhook")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ödeme webhook", Description = "Iyzico bant-dışı bildirim (yedek teyit). İmza doğrulanır, idempotent.")]
        public async Task<IActionResult> Webhook([FromBody] PaymentCallbackRequestDto dto)
        {
            var r = await _paymentService.HandleCallback(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
