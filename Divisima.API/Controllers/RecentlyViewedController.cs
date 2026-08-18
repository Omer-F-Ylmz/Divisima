using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Son görüntülenen ürünler controller'ı (thin). Müşteri-kapsamlı (kendi görüntülemeleri).
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Son görüntülenen ürünler")]
    public class RecentlyViewedController : SecureControllerBase
    {
        private readonly IRecentlyViewedService _recentlyViewedService;

        public RecentlyViewedController(IRecentlyViewedService recentlyViewedService)
        {
            _recentlyViewedService = recentlyViewedService;
        }

        // Açıklayıcı yorum: Ürün görüntülemesini kaydet
        [HttpPost("record/{productId:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Görüntüleme kaydet", Description = "Müşterinin ürünü görüntülediğini kaydeder (upsert).")]
        public async Task<IActionResult> Record(int productId)
        {
            var result = await _recentlyViewedService.RecordView(CurrentCustomerId, productId);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Son görüntülenen ürünler
        [HttpGet]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Son görüntülenenler", Description = "Müşterinin son görüntülediği ürünleri (en yeniden eskiye) döner.")]
        public async Task<IActionResult> Get([FromQuery] int limit = 10)
        {
            var result = await _recentlyViewedService.GetRecentlyViewed(CurrentCustomerId, limit);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
