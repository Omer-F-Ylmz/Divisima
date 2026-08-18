using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.ProductAttribute;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/product-attribute")]
    [ApiController]
    [SwaggerTag("Ürün özellikleri + faceted search")]
    public class ProductAttributeController : ControllerBase
    {
        private readonly IProductAttributeService _attrService;
        public ProductAttributeController(IProductAttributeService attrService) { _attrService = attrService; }

        [HttpPost("set")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Özellik ata (admin)", Description = "Ürüne materyal/sezon/stil vb. özellik atar.")]
        public async Task<IActionResult> Set([FromBody] SetAttributesDto dto)
        { var r = await _attrService.SetAttributes(dto); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("product/{productId:int:min(1)}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürün özellikleri")]
        public async Task<IActionResult> ByProduct(int productId)
        { var r = await _attrService.GetAttributes(productId); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("facets")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Facet ağacı", Description = "Filtre için anahtar/değer + ürün sayaçları.")]
        public async Task<IActionResult> Facets()
        { var r = await _attrService.GetFacets(); return StatusCode((int)r.Item1, r.Item2); }

        [HttpPost("filter")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Faceted filtre", Description = "Seçili özelliklere göre ürünleri filtreler.")]
        public async Task<IActionResult> Filter([FromBody] FacetFilterDto dto)
        { var r = await _attrService.FilterByAttributes(dto); return StatusCode((int)r.Item1, r.Item2); }
    }
}
