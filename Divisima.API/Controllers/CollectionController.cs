using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Collection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Koleksiyon controller'ı. Thin. Admin CRUD + public liste/detay.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Koleksiyon ve stil elçisi yönetimi")]
    public class CollectionController : ControllerBase
    {
        private readonly ICollectionService _collectionService;

        public CollectionController(ICollectionService collectionService)
        {
            _collectionService = collectionService;
        }

        [HttpPost("add")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Koleksiyon ekle", Description = "Yeni koleksiyon (sezon veya stil elçisi) ekler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        public async Task<IActionResult> Add([FromBody] CollectionAddRequestDto dto)
        {
            var result = await _collectionService.Add(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPut("update")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Koleksiyon güncelle", Description = "Koleksiyonu ve ürün seçkisini günceller. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Update([FromBody] CollectionUpdateRequestDto dto)
        {
            var result = await _collectionService.Update(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpDelete("delete/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Koleksiyon sil", Description = "Koleksiyonu kalıcı siler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _collectionService.Delete(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPatch("status/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Koleksiyon durumu değiştir", Description = "Koleksiyonu aktif/pasif yapar. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var result = await _collectionService.ChangeStatus(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Tüm koleksiyonlar (herkese açık - ana sayfa + elçiler)
        [HttpGet("getlist")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Koleksiyon listesi", Description = "Tüm aktif koleksiyonları listeler (ana sayfa + stil elçileri).")]
        [ProducesResponseType(typeof(SuccessDataResult<List<CollectionListResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetList()
        {
            var result = await _collectionService.GetList();
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Slug ile koleksiyon detayı + ürünler (herkese açık - frontend showCollection)
        [HttpGet("get/{slug}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Koleksiyon detayı", Description = "Slug ile koleksiyonu içindeki ürünlerle getirir.")]
        [ProducesResponseType(typeof(SuccessDataResult<CollectionDetailResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorDataResult<CollectionDetailResponseDto>), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _collectionService.GetBySlug(slug);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
