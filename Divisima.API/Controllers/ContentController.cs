using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: İçerik controller'ı (legal sayfalar). Public okuma + admin güncelleme.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("İçerik sayfaları (mesafeli satış, KVKK, iade...)")]
    public class ContentController : ControllerBase
    {
        private readonly IContentService _contentService;

        public ContentController(IContentService contentService)
        {
            _contentService = contentService;
        }

        [HttpGet("get/{slug}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "İçerik getir", Description = "Slug ile içerik sayfasını getirir (TR/EN).")]
        [ProducesResponseType(typeof(SuccessDataResult<ContentResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _contentService.GetBySlug(slug);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpGet("getlist")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "İçerik listesi", Description = "Tüm aktif içerik sayfalarını listeler (footer).")]
        [ProducesResponseType(typeof(SuccessDataResult<List<ContentResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetList()
        {
            var result = await _contentService.GetList();
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPut("update")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "İçerik güncelle", Description = "İçerik sayfasını günceller. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Update([FromBody] ContentUpdateRequestDto dto)
        {
            var result = await _contentService.Update(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
