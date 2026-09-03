using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Coupon;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Kupon controller'ı. Thin. Admin CRUD + public ValidateCoupon.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Kupon yönetimi ve doğrulama")]
    public class CouponController : ControllerBase
    {
        private readonly ICouponService _couponService;

        private readonly ICurrentUserService _currentUser;
        public CouponController(ICouponService couponService, ICurrentUserService currentUser)
        {
            _couponService = couponService;
            _currentUser = currentUser;
        }

        [HttpPost("add")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kupon ekle", Description = "Yeni kupon ekler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        public async Task<IActionResult> Add([FromBody] CouponAddRequestDto dto)
        {
            var result = await _couponService.Add(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPut("update")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kupon güncelle", Description = "Kuponu günceller. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Update([FromBody] CouponUpdateRequestDto dto)
        {
            var result = await _couponService.Update(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpDelete("delete/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kupon sil", Description = "Kuponu kalıcı siler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _couponService.Delete(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPatch("status/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kupon durumu değiştir", Description = "Kuponu aktif/pasif yapar. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var result = await _couponService.ChangeStatus(id);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpGet("getlist")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Kupon listesi", Description = "Tüm aktif kuponları listeler. Admin yetkisi gerekir.")]
        [ProducesResponseType(typeof(SuccessDataResult<List<CouponListResponseDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetList()
        {
            var result = await _couponService.GetList();
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Kupon doğrula (herkese açık - sepet ekranı). Frontend applyCoupon.
        [HttpPost("validate")]
        [AllowAnonymous]
        // GF-3/K9 (AV-1: F-1) - F-1'in EN AGIR ucu. Yanit gecerli ve gecersiz kodu AYIRT
        // EDIYOR ve o yanit metni MFIX-B/K2 karariyla DOKUNULMAZ; yani enumerasyon kanali
        // yanit tarafindan kapatilamaz. Tek care LIMIT: 100/dk -> 20/dk (IP basina).
        [EnableRateLimiting(Divisima.Core.Security.RateLimiting.RateLimitPolitikasi.HassasKapsami)]
        [SwaggerOperation(Summary = "Kupon doğrula", Description = "Kupon kodunu ve sepet tutarını alır; geçerliyse indirim tutarını döner.")]
        [ProducesResponseType(typeof(SuccessDataResult<CouponValidateResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorDataResult<CouponValidateResponseDto>), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Validate([FromBody] CouponValidateRequestDto dto)
        {
            // Açıklayıcı yorum: İlk-sipariş kontrolü için müşteri (girişsizse 0)
            dto.customer_id = _currentUser.UserId ?? 0;
            var result = await _couponService.ValidateCoupon(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
