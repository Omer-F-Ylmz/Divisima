using Divisima.Bussiness.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    [SwaggerTag("Vitrin listeleri - çok satanlar, trend, yeni gelenler")]
    public class MerchandisingController : ControllerBase
    {
        private readonly IMerchandisingService _merchandisingService;
        public MerchandisingController(IMerchandisingService merchandisingService) { _merchandisingService = merchandisingService; }

        [HttpGet("best-sellers")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Çok satanlar")]
        public async Task<IActionResult> BestSellers([FromQuery] int take = 12)
        { var r = await _merchandisingService.GetBestSellers(take); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("trending")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Trend ürünler (son 30 gün)")]
        public async Task<IActionResult> Trending([FromQuery] int take = 12)
        { var r = await _merchandisingService.GetTrending(take); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("new-arrivals")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Yeni gelenler")]
        public async Task<IActionResult> NewArrivals([FromQuery] int take = 12)
        { var r = await _merchandisingService.GetNewArrivals(take); return StatusCode((int)r.Item1, r.Item2); }
    }
}
