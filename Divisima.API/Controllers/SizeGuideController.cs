using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.SizeGuide;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/size-guide")]
    [ApiController]
    [SwaggerTag("Beden rehberi + öneri")]
    public class SizeGuideController : ControllerBase
    {
        private readonly ISizeGuideService _sizeGuideService;
        public SizeGuideController(ISizeGuideService sizeGuideService) { _sizeGuideService = sizeGuideService; }

        [HttpPost("upsert")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Beden satırı ekle/güncelle (admin)")]
        public async Task<IActionResult> Upsert([FromBody] SizeGuideEntryDto dto)
        { var r = await _sizeGuideService.Upsert(dto); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("category/{categoryId:int:min(1)}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Kategori beden tablosu")]
        public async Task<IActionResult> ByCategory(int categoryId)
        { var r = await _sizeGuideService.GetByCategory(categoryId); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("recommend")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Beden önerisi", Description = "Ölçülere en yakın bedeni önerir.")]
        public async Task<IActionResult> Recommend([FromQuery] int categoryId, [FromQuery] decimal? bust, [FromQuery] decimal? waist, [FromQuery] decimal? hip)
        { var r = await _sizeGuideService.RecommendSize(categoryId, bust, waist, hip); return StatusCode((int)r.Item1, r.Item2); }
    }
}
