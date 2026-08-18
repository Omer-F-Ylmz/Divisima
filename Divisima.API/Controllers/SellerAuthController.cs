using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Seller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Satıcı kimlik doğrulama - MÜŞTERİDEN AYRI endpoint (api/seller/auth). Public kayıt/giriş.
    [Route("api/seller/auth")]
    [ApiController]
    [EnableRateLimiting("auth")]
    public class SellerAuthController : ControllerBase
    {
        private readonly ISellerAuthService _sellerAuth;

        public SellerAuthController(ISellerAuthService sellerAuth)
        {
            _sellerAuth = sellerAuth;
        }

        // Açıklayıcı yorum: Satıcı başvurusu (kayıt sonrası Pending - admin onayı bekler).
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Register([FromBody] SellerRegisterRequestDto dto)
        {
            var result = await _sellerAuth.Register(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        // Açıklayıcı yorum: Satıcı girişi - JWT (user_type=Seller) döner.
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(SuccessDataResult<SellerLoginResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Login([FromBody] SellerLoginRequestDto dto)
        {
            var result = await _sellerAuth.Login(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }
    }
}
