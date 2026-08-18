using Divisima.Bussiness.Abstract;
using Divisima.Entity.Dtos.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ürün arama controller'ı (herkese açık - katalog arama).
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Ürün arama")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        public SearchController(ISearchService searchService) { _searchService = searchService; }

        [HttpGet("products")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürün ara", Description = "Metin + fiyat + kategori filtreleri, sıralama, sayfalama.")]
        public async Task<IActionResult> SearchProducts([FromQuery] ProductSearchRequestDto dto)
        {
            var r = await _searchService.SearchProducts(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
