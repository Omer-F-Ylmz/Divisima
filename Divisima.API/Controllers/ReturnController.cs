using System.Security.Claims;
using Divisima.API.Filters;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Return;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: İade/değişim uçları. Müşteri kendi talebini açar/görür; admin işler.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("İade/değişim")]
    public class ReturnController : SecureControllerBase
    {
        private readonly IReturnService _returnService;
        public ReturnController(IReturnService returnService) { _returnService = returnService; }

        [HttpPost("create")]
        [RequireUserType(UserTypeEnum.Customer)]
        // ══ GF-6 / F3 (T4-F1) - CIFT IADE TALEBI ═══════════════════════════════════════════
        // Bu uc bir PARA ucudur: her talep bir iade tutari tasir. Ag tekrari ya da cift-tik
        // ikinci bir talep uretmemeli. `Idempotency-Key` GONDERILDIGINDE ilk yanit AYNEN
        // doner (filtrenin kendi sozlesmesi); anahtar GONDERILMEZSE filtre kenara cekilir -
        // o senaryonun korumasi `ReturnManager`daki KALEM BASINA dagitik kilit + aktif-iade
        // kontrolüdür. Iki katman FARKLI seyi korur ve ikisi de gereklidir.
        [Idempotency]
        [SwaggerOperation(Summary = "İade talebi oluştur")]
        public async Task<IActionResult> Create([FromBody] ReturnCreateRequestDto dto)
        {
            dto.customer_id = CurrentCustomerId;   // JWT'den - başkası adına iade engeli
            var r = await _returnService.CreateReturn(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("my")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "İade taleplerim")]
        public async Task<IActionResult> MyReturns()
        {
            var r = await _returnService.GetMyReturns(CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("pending")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Bekleyen iadeler (admin)")]
        public async Task<IActionResult> Pending()
        {
            var r = await _returnService.GetPendingReturns();
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("process")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "İade işle - onay/ret (admin)")]
        public async Task<IActionResult> Process([FromBody] ReturnProcessRequestDto dto)
        {
            var r = await _returnService.ProcessReturn(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
