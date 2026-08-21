using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Security.Identity;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Wishlist;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Favoriler controller'ı. customer_id token'dan (IDOR engeli).
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Favoriler")]
    public class WishlistController : SecureControllerBase
    {
        private readonly IWishlistService _wishlistService;
        private readonly ICurrentUserService _currentUser;
        public WishlistController(IWishlistService wishlistService, ICurrentUserService currentUser)
        {
            _wishlistService = wishlistService;
            _currentUser = currentUser;
        }

        [HttpPost("toggle")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Toggle(int productId)
        {
            var r = await _wishlistService.Toggle(_currentUser.GetRequiredUserId(), productId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> Get()
        {
            var r = await _wishlistService.GetByCustomer(_currentUser.GetRequiredUserId());
            return StatusCode((int)r.Item1, r.Item2);
        }

        // İstek listesinden sepete taşı
        [HttpPost("move-to-cart")]
        [RequireUserType(UserTypeEnum.Customer)]
        public async Task<IActionResult> MoveToCart([FromBody] WishlistMoveToCartDto dto)
        { var r = await _wishlistService.MoveToCart(CurrentCustomerId, dto.product_id, dto.size, dto.quantity); return StatusCode((int)r.Item1, r.Item2); }

    }
}
