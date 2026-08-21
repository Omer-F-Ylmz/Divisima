using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
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
        private readonly ICurrentUserService _currentUser;
        public PriceDropController(IPriceDropService priceDropService, ICurrentUserService currentUser)
        { _priceDropService = priceDropService; _currentUser = currentUser; }

        [HttpPost("subscribe")]
        [AllowAnonymous]
        // DoS/SPAM FIX (H44): anonim + DB'ye kayit yazan uc -> limitsizdi. fiyat-düşüş aboneliği (e-posta ile kayıt yaratır).
        // Sinirsiz sahte istek: DB sismesi, stok rezervasyon kilidi ve site uzerinden rastgele adreslere
        // e-posta gonderimi (spam rolesi) mumkundu. Zaten TANIMLI "auth" politikasi (5/dk/IP) uygulandi.
        [EnableRateLimiting("auth")]
        [SwaggerOperation(Summary = "Fiyat düşüş aboneliği", Description = "Ürün fiyatı düşünce e-posta ile haber verilir.")]
        public async Task<IActionResult> Subscribe([FromBody] PriceDropSubscribeDto dto)
        { var r = await _priceDropService.Subscribe(dto); return StatusCode((int)r.Item1, r.Item2); }

        // ── SPRINT 8 MADDE 10 - ABONELIK YONETIMI ──────────────────────────────────
        //
        // Onceden bu controller'da YALNIZ "subscribe" vardi: kullanici kurdugu bildirimi ne
        // gorebiliyor ne kapatabiliyordu. Uc kalem birlikte geldi (liste / sil / jetonla cik).

        [HttpGet("my")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Fiyat uyarısı aboneliklerim")]
        public async Task<IActionResult> My()
        {
            var r = await _priceDropService.GetMine(_currentUser.GetRequiredEmail());
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpDelete("{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Aboneliği kaldır", Description = "Yalnız kendi aboneliğini kaldırabilirsin.")]
        public async Task<IActionResult> Remove(int id)
        {
            var r = await _priceDropService.RemoveMine(id, _currentUser.GetRequiredEmail());
            return StatusCode((int)r.Item1, r.Item2);
        }

        // ANONIM ve JETONLA: abonelik uye olmadan kurulabildigi icin cikma yolu da kimlik
        // dogrulamasi isteyemez. Jeton e-postadaki baglantida gelir ve tahmin edilemez.
        // Rate limit: jeton deneme-yanilma yuzeyini kapatir.
        [HttpGet("unsubscribe")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [SwaggerOperation(Summary = "Abonelikten çık (e-posta bağlantısı)")]
        public async Task<IActionResult> Unsubscribe([FromQuery] string token)
        {
            var r = await _priceDropService.UnsubscribeByToken(token);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
