using Divisima.Bussiness.Abstract;
using Divisima.Entity.Dtos.PriceDrop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/price-drop")]
    [ApiController]
    [SwaggerTag("Fiyat düşünce haber ver")]
    public class PriceDropController : ControllerBase
    {
        private readonly IPriceDropService _priceDropService;
        public PriceDropController(IPriceDropService priceDropService) { _priceDropService = priceDropService; }

        [HttpPost("subscribe")]
        [AllowAnonymous]
        // DoS/SPAM FIX (H44): anonim + DB'ye kayit yazan uc -> limitsizdi. fiyat-düşüş aboneliği (e-posta ile kayıt yaratır).
        // Sinirsiz sahte istek: DB sismesi, stok rezervasyon kilidi ve site uzerinden rastgele adreslere
        // e-posta gonderimi (spam rolesi) mumkundu. Zaten TANIMLI "auth" politikasi (5/dk/IP) uygulandi.
        [EnableRateLimiting("auth")]
        [SwaggerOperation(Summary = "Fiyat düşüş aboneliği", Description = "Ürün fiyatı düşünce e-posta ile haber verilir.")]
        public async Task<IActionResult> Subscribe([FromBody] PriceDropSubscribeDto dto)
        { var r = await _priceDropService.Subscribe(dto); return StatusCode((int)r.Item1, r.Item2); }
    }
}
