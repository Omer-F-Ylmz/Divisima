using Divisima.Bussiness.Abstract;
using Divisima.Entity.Dtos.StockNotification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Stok bildirim controller'ı (thin). "Gelince haber ver" aboneliği - herkese açık.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Stok gelince haber ver")]
    public class StockNotificationController : ControllerBase
    {
        private readonly IStockNotificationService _stockNotificationService;

        public StockNotificationController(IStockNotificationService stockNotificationService)
        {
            _stockNotificationService = stockNotificationService;
        }

        // Açıklayıcı yorum: Stoksuz ürün+beden için e-posta bırak
        [HttpPost("subscribe")]
        [AllowAnonymous]
        // DoS/SPAM FIX (H44): anonim + DB'ye kayit yazan uc -> limitsizdi. stok bildirimi aboneliği (e-posta ile kayıt yaratır).
        // Sinirsiz sahte istek: DB sismesi, stok rezervasyon kilidi ve site uzerinden rastgele adreslere
        // e-posta gonderimi (spam rolesi) mumkundu. Zaten TANIMLI "auth" politikasi (5/dk/IP) uygulandi.
        [EnableRateLimiting("auth")]
        [SwaggerOperation(Summary = "Stok bildirimi aboneliği", Description = "Ürün+beden tekrar stoğa girince e-posta ile haber verilir.")]
        public async Task<IActionResult> Subscribe([FromBody] StockNotificationSubscribeRequestDto dto)
        {
            var result = await _stockNotificationService.Subscribe(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
