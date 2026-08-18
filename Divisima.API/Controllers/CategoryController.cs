using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Category;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Kategori controller'ı. Thin - StatusCode(tuple). Cafixo kalıbı.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Kategori ve alt kategori yönetimi")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpPost("add")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kategori ekle", Description = "Yeni ana kategori ekler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        public async Task<IActionResult> Add([FromBody] CategoryAddRequestDto dto)
        {
            var result = await _categoryService.Add(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPut("update")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kategori güncelle", Description = "Kategoriyi günceller. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Update([FromBody] CategoryUpdateRequestDto dto)
        {
            var result = await _categoryService.Update(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpDelete("delete/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kategori sil", Description = "Kategoriyi kalıcı siler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _categoryService.Delete(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPatch("status/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kategori durumu değiştir", Description = "Kategoriyi aktif/pasif yapar. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var result = await _categoryService.ChangeStatus(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpGet("get/{id:int:min(1)}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Kategori detayı", Description = "Kategoriyi alt kategorileriyle getirir.")]
        [ProducesResponseType(typeof(SuccessDataResult<CategoryResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _categoryService.GetById(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Menü/filtre için tüm kategoriler + alt kategoriler (herkese açık)
        [HttpGet("getlist")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Kategori listesi", Description = "Tüm aktif kategorileri alt kategorileriyle listeler (menü/filtre).")]
        [ProducesResponseType(typeof(SuccessDataResult<List<CategoryResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetList()
        {
            var result = await _categoryService.GetList();
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
