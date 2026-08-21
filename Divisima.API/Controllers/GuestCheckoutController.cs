using Divisima.API.Filters;
using Divisima.Bussiness.Abstract;
using Divisima.Entity.Dtos.Guest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/guest-checkout")]
    [ApiController]
    [AllowAnonymous]
    // DoS/SPAM FIX (H44): anonim + DB'ye kayit yazan uc -> limitsizdi. misafir sipariş (müşteri+adres+sipariş kaydı + stok rezervasyonu yaratır).
    // Sinirsiz sahte istek: DB sismesi, stok rezervasyon kilidi ve site uzerinden rastgele adreslere
    // e-posta gonderimi (spam rolesi) mumkundu. Zaten TANIMLI "auth" politikasi (5/dk/IP) uygulandi.
    [EnableRateLimiting("auth")]
    [SwaggerTag("Misafir (hesapsız) sipariş")]
    public class GuestCheckoutController : ControllerBase
    {
        private readonly IGuestCheckoutService _guestCheckoutService;
        public GuestCheckoutController(IGuestCheckoutService guestCheckoutService) { _guestCheckoutService = guestCheckoutService; }

        [Idempotency]
        [HttpPost("place")]
        [SwaggerOperation(Summary = "Misafir sipariş ver", Description = "Hesap oluşturmadan sipariş verir. E-posta kayıtlıysa giriş yapılması istenir.")]
        public async Task<IActionResult> Place([FromBody] GuestCheckoutDto dto)
        { var r = await _guestCheckoutService.PlaceGuestOrder(dto); return StatusCode((int)r.Item1, r.Item2); }
    }
}
