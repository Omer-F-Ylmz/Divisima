using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Device;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Cihaz/push kaydı uçları. Müşteri kendi cihazını kaydeder (push almak için).
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Cihaz / push kaydı")]
    public class DeviceController : SecureControllerBase
    {
        private readonly ICustomerDeviceService _deviceService;
        public DeviceController(ICustomerDeviceService deviceService) { _deviceService = deviceService; }

        [HttpPost("register")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Cihaz kaydet (push token)")]
        public async Task<IActionResult> Register([FromBody] DeviceRegisterDto dto)
        {
            dto.customer_id = CurrentCustomerId;
            var r = await _deviceService.RegisterDevice(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("unregister")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Cihaz kaydını sil (logout/çıkış)")]
        public async Task<IActionResult> Unregister([FromBody] DeviceRegisterDto dto)
        {
            var r = await _deviceService.UnregisterDevice(dto.device_token, CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
