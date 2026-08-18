using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Cart;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Sepet controller'ı. customer_id TOKEN'dan alınır (istemci gönderemez - IDOR engeli).
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Müşteri sepeti")]
    public class CartController : SecureControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ICurrentUserService _currentUser;
        public CartController(ICartService cartService, ICurrentUserService currentUser)
        {
            _cartService = cartService;
            _currentUser = currentUser;
        }

        [HttpPost("add")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Add([FromBody] CartItemRequestDto dto)
        {
            // Açıklayıcı yorum: Body'deki customer_id yok sayılır, token'daki kimlik kullanılır
            dto.customer_id = _currentUser.GetRequiredUserId();
            var r = await _cartService.AddItem(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpDelete("remove")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Remove(int productId, string size)
        {
            var r = await _cartService.RemoveItem(_currentUser.GetRequiredUserId(), productId, size);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Get()
        {
            var r = await _cartService.GetCart(_currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpDelete("clear")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Clear()
        {
            var r = await _cartService.ClearCart(_currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }
        [HttpPost("save-for-later")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Kaydet-sonra-al", Description = "Sepet kalemini favorilere taşır.")]
        public async Task<IActionResult> SaveForLater([FromQuery] int productId, [FromQuery] string size)
        {
            var r = await _cartService.SaveForLater(_currentUser.GetRequiredUserId(), productId, size);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("move-to-cart")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Sepete geri al", Description = "Favorilerden sepete taşır (stok kontrollü).")]
        public async Task<IActionResult> MoveToCart([FromQuery] int productId, [FromQuery] string size)
        {
            var r = await _cartService.MoveToCart(_currentUser.GetRequiredUserId(), productId, size);
            return StatusCode((int)r.Item1, r.Item2);
        }

    }
}
