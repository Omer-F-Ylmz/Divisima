using Divisima.Bussiness.Abstract;
using Divisima.Entity.Dtos.Comparison;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    [SwaggerTag("Ürün karşılaştırma")]
    public class ComparisonController : ControllerBase
    {
        private readonly IProductComparisonService _comparisonService;
        public ComparisonController(IProductComparisonService comparisonService) { _comparisonService = comparisonService; }

        [HttpPost("compare")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürünleri karşılaştır", Description = "2-4 ürünü özellikleriyle yan yana getirir.")]
        public async Task<IActionResult> Compare([FromBody] CompareRequestDto dto)
        { var r = await _comparisonService.Compare(dto); return StatusCode((int)r.Item1, r.Item2); }
    }
}
