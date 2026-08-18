using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.API.Filters;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Customer)]
    [SwaggerTag("Sadakat puanı")]
    public class LoyaltyController : SecureControllerBase
    {
        private readonly ILoyaltyService _loyaltyService;
        public LoyaltyController(ILoyaltyService loyaltyService) { _loyaltyService = loyaltyService; }

        [HttpGet("balance")]
        [SwaggerOperation(Summary = "Puan bakiyesi")]
        public async Task<IActionResult> Balance()
        { var r = await _loyaltyService.GetBalance(CurrentCustomerId); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("history")]
        [SwaggerOperation(Summary = "Puan geçmişi")]
        public async Task<IActionResult> History()
        { var r = await _loyaltyService.GetHistory(CurrentCustomerId); return StatusCode((int)r.Item1, r.Item2); }

        [Idempotency]
        [HttpPost("redeem/{points:int:min(1)}")]
        [SwaggerOperation(Summary = "Puanı krediye çevir", Description = "En az 100 puan; 100 puan = 10 TL mağaza kredisi.")]
        public async Task<IActionResult> Redeem(int points)
        { var r = await _loyaltyService.RedeemForCredit(CurrentCustomerId, points); return StatusCode((int)r.Item1, r.Item2); }

        // Sadakat seviyesi (rozet + ilerleme)
        [HttpGet("tier")]
        public async Task<IActionResult> Tier()
        { var r = await _loyaltyService.GetTier(CurrentCustomerId); return StatusCode((int)r.Item1, r.Item2); }

    }
}
