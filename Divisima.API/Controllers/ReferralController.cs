using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Customer)]
    [SwaggerTag("Referans programı")]
    public class ReferralController : SecureControllerBase
    {
        private readonly IReferralService _referralService;
        public ReferralController(IReferralService referralService) { _referralService = referralService; }

        [HttpGet("my-code")]
        [SwaggerOperation(Summary = "Referans kodum", Description = "Kişisel referans kodunu döner (yoksa üretir).")]
        public async Task<IActionResult> MyCode()
        { var r = await _referralService.GetOrCreateMyCode(CurrentCustomerId); return StatusCode((int)r.Item1, r.Item2); }
    }
}
