using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Address;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Adres defteri controller'ı. customer_id token'dan; silme/güncellemede sahiplik doğrulanır.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Müşteri adres defteri")]
    public class AddressController : SecureControllerBase
    {
        private readonly IAddressService _addressService;
        private readonly ICurrentUserService _currentUser;
        public AddressController(IAddressService addressService, ICurrentUserService currentUser)
        {
            _addressService = addressService;
            _currentUser = currentUser;
        }

        [HttpPost("upsert")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Upsert([FromBody] AddressRequestDto dto)
        {
            // Açıklayıcı yorum: customer_id token'dan (istemci başkasının adına ekleyemez)
            dto.customer_id = _currentUser.GetRequiredUserId();
            var r = await _addressService.Upsert(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpDelete("delete/{id:int:min(1)}")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Delete(int id)
        {
            // Açıklayıcı yorum: Sahiplik doğrulaması servis katmanında (customer_id ile)
            var r = await _addressService.Delete(id, _currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> GetMine()
        {
            var r = await _addressService.GetByCustomer(_currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
