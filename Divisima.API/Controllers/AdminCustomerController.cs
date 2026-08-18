using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Admin;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Admin müşteri yönetimi uçları. Listeleme + askıya alma/aktifleştirme.
    [Route("api/admin/customer")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Admin)]
    [SwaggerTag("Admin müşteri yönetimi")]
    public class AdminCustomerController : ControllerBase
    {
        private readonly IAdminCustomerService _service;
        public AdminCustomerController(IAdminCustomerService service) { _service = service; }

        [HttpPost("list")]
        [SwaggerOperation(Summary = "Müşterileri listele (arama+sayfalama)")]
        public async Task<IActionResult> List([FromBody] AdminCustomerFilterDto filter)
        {
            var r = await _service.ListCustomers(filter);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("status")]
        [SwaggerOperation(Summary = "Müşteri askıya al / aktifleştir")]
        public async Task<IActionResult> Status([FromBody] AdminCustomerStatusDto dto)
        {
            var r = await _service.SetActive(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("set-type")]
        [SwaggerOperation(Summary = "Kullanıcı tipini değiştir (admin yap / müşteriye indir)")]
        public async Task<IActionResult> SetType([FromBody] AdminSetUserTypeDto dto)
        {
            var r = await _service.SetUserType(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
