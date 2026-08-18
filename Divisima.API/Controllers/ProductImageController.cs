using Divisima.Bussiness.Abstract;
using Microsoft.AspNetCore.Authorization;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ürün görsel yönetimi (admin). Multipart yükleme + listeleme/silme/birincil.
    [Route("api/product-image")]
    [ApiController]
    [SwaggerTag("Ürün görselleri")]
    public class ProductImageController : ControllerBase
    {
        private readonly IProductImageService _service;
        public ProductImageController(IProductImageService service) { _service = service; }

        // Açıklayıcı yorum: Görseli herkes görebilir (ürün detayı); yükleme/silme admin
        [HttpGet("product/{productId:int:min(1)}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ürünün görselleri")]
        public async Task<IActionResult> ByProduct(int productId)
        {
            var r = await _service.GetByProduct(productId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("upload")]
        [RequireUserType(UserTypeEnum.Admin)]
        [RequestSizeLimit(6 * 1024 * 1024)] // 6 MB üst sınır (5 MB dosya + overhead)
        [SwaggerOperation(Summary = "Görsel yükle (admin, multipart)")]
        public async Task<IActionResult> Upload([FromForm] int productId, [FromForm] IFormFile file, [FromForm] bool isPrimary = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Dosya gerekli." });

            // Açıklayıcı yorum: Baytları oku, servise ver (doğrulama serviste)
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var r = await _service.Upload(productId, ms.ToArray(), file.FileName, file.ContentType, isPrimary);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpDelete("{imageId:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Görsel sil (admin)")]
        public async Task<IActionResult> Delete(int imageId)
        {
            var r = await _service.Delete(imageId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("{imageId:int:min(1)}/primary")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Birincil görsel belirle (admin)")]
        public async Task<IActionResult> SetPrimary(int imageId)
        {
            var r = await _service.SetPrimary(imageId);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
