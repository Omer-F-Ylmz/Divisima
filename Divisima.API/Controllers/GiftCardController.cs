using Divisima.API.Filters;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.GiftCard;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/gift-card")]
    [ApiController]
    [SwaggerTag("Hediye kartı")]
    public class GiftCardController : SecureControllerBase
    {
        private readonly IGiftCardService _giftCardService;
        public GiftCardController(IGiftCardService giftCardService) { _giftCardService = giftCardService; }

        [HttpPost("create")]
        [RequireUserType(UserTypeEnum.Admin)]
        [SwaggerOperation(Summary = "Hediye kartı üret (admin)")]
        public async Task<IActionResult> Create([FromBody] GiftCardCreateDto dto)
        { var r = await _giftCardService.Create(dto.amount); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("balance/{code}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Kart bakiyesi sorgula")]
        public async Task<IActionResult> Balance(string code)
        { var r = await _giftCardService.CheckBalance(code); return StatusCode((int)r.Item1, r.Item2); }

        [Idempotency]
        [HttpPost("redeem/{code}")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Kartı bozdur", Description = "Bakiye mağaza kredisine aktarılır.")]
        public async Task<IActionResult> Redeem(string code)
        { var r = await _giftCardService.Redeem(CurrentCustomerId, code); return StatusCode((int)r.Item1, r.Item2); }
    }
}
