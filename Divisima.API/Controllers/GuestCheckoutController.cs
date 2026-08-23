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
    // e-posta gonderimi (spam rolesi) mumkundu. Zaten TANIMLI "auth" politikasi uygulandi.
    //
    // A3 OLCUMU - YORUM DUZELTMESI: burada "5/dk/IP" yaziyordu, YANLISTI. Program.cs'te
    // authPermitLimit varsayilani 10 ve appsettings.Development.example.json da 10 diyor;
    // hicbir yerde 5'e cekilmiyor. Sayi yorumda TEKRARLANMAZ hale getirildi - iki yerde
    // duran bir sayi kaciniImaz olarak ayrisiyor (bu satir ayristi).
    //
    // MISAFIRIN HESABINI SAHIPLENME ZINCIRI BU KOVAYA SIGAR - CANLI OLCULDU:
    //   guest-checkout/place -> verify-email -> forgot-password -> reset-password = 4 istek
    // Dordu de "auth" kovasinda; limit 10/dk/IP. Zincir 200/200/200/200 ile tamamlandi ve
    // 429 GORULMEDI. Kova GEVSETILMEDI.
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
