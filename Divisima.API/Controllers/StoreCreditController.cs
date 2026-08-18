using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    [Route("api/store-credit")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Customer)]
    [SwaggerTag("Mağaza kredisi")]
    public class StoreCreditController : SecureControllerBase
    {
        private readonly IStoreCreditService _storeCreditService;
        public StoreCreditController(IStoreCreditService storeCreditService) { _storeCreditService = storeCreditService; }

        [HttpGet("balance")]
        [SwaggerOperation(Summary = "Kredi bakiyesi")]
        public async Task<IActionResult> Balance()
        { var r = await _storeCreditService.GetBalance(CurrentCustomerId); return StatusCode((int)r.Item1, r.Item2); }

        [HttpGet("history")]
        [SwaggerOperation(Summary = "Kredi geçmişi")]
        public async Task<IActionResult> History()
        { var r = await _storeCreditService.GetHistory(CurrentCustomerId); return StatusCode((int)r.Item1, r.Item2); }
    }
}
