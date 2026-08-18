using Divisima.Bussiness.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Öneri controller'ı (thin). Ürün detay/sepet sayfalarında kişiselleştirme.
    // Herkese açık (AllowAnonymous) - öneriler oturum gerektirmez.
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    [SwaggerTag("Ürün önerileri - birliktelik ve benzerlik")]
    public class RecommendationController : ControllerBase
    {
        private readonly IRecommendationService _recommendationService;

        public RecommendationController(IRecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        // Açıklayıcı yorum: "Bunu alanlar şunu da aldı"
        [HttpGet("frequently-bought/{productId}")]
        [SwaggerOperation(Summary = "Birlikte alınanlar", Description = "Bu ürünle aynı siparişlerde geçen diğer ürünleri sıklığa göre döner.")]
        public async Task<IActionResult> FrequentlyBought(int productId, [FromQuery] int limit = 8)
        {
            var result = await _recommendationService.GetFrequentlyBoughtTogether(productId, limit);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: "Benzer ürünler"
        [HttpGet("similar/{productId}")]
        [SwaggerOperation(Summary = "Benzer ürünler", Description = "Aynı kategorideki diğer aktif ürünleri döner.")]
        public async Task<IActionResult> Similar(int productId, [FromQuery] int limit = 8)
        {
            var result = await _recommendationService.GetSimilarProducts(productId, limit);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
