using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Kimlik doğrulama controller'ı. Thin. Kayıt/giriş/token yenileme - hepsi public.
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("auth")]
    [SwaggerTag("Müşteri kimlik doğrulama (kayıt, giriş, token)")]
    // Güvenlik: refresh token yalnızca httpOnly cookie ile taşınır (XSS koruması)
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Müşteri kaydı", Description = "Yeni müşteri hesabı oluşturur. Şifre güvenli şekilde hash'lenir.")]
        [ProducesResponseType(typeof(SuccessResult), (int)HttpStatusCode.Created)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Register([FromBody] CustomerRegisterRequestDto dto)
        {
            var result = await _authService.Register(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Müşteri girişi", Description = "E-posta ve şifre ile giriş yapar; JWT token döner.")]
        [ProducesResponseType(typeof(SuccessDataResult<CustomerLoginResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorDataResult<CustomerLoginResponseDto>), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Login([FromBody] CustomerLoginRequestDto dto)
        {
            var result = await _authService.Login(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Token yenile", Description = "Refresh token ile yeni access token alır (oturum uzatma).")]
        [ProducesResponseType(typeof(SuccessDataResult<CustomerLoginResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            var result = await _authService.RefreshToken(dto);
            return StatusCode((int)result.Item1, result.Item2);
        }


        [HttpGet("verify-email")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "E-posta doğrula")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var r = await _authService.VerifyEmail(token);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("resend-verification")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Doğrulama mailini yeniden gönder")]
        public async Task<IActionResult> ResendVerification([FromQuery] string email)
        {
            var r = await _authService.ResendVerification(email);
            return StatusCode((int)r.Item1, r.Item2);
        }




        [HttpDelete("account")]
        [RequireUserType(UserTypeEnum.Customer)]
        [Divisima.Core.Security.Authorization.RequireRecentAuth(10)]
        [SwaggerOperation(Summary = "Hesabı sil (KVKK/GDPR)", Description = "Kişisel veri anonimleştirilir. Son 10 dk içinde giriş gerekir (step-up).")]
        public async Task<IActionResult> DeleteAccount()
        {
            var customerId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var r = await _authService.DeleteAccount(customerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpGet("my-data")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Verilerimi dışa aktar (GDPR)", Description = "Kişisel verinin makine-okunur kopyası.")]
        public async Task<IActionResult> ExportMyData()
        {
            var customerId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var r = await _authService.ExportMyData(customerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Şifre sıfırlama talebi")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
        {
            var r = await _authService.ForgotPassword(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Token ile yeni şifre belirle")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
        {
            var r = await _authService.ResetPassword(dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("logout")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Çıkış (oturum iptali)")]
        public async Task<IActionResult> Logout()
        {
            // Açıklayıcı yorum: refresh token cookie'den okunur; JWT'den kullanıcı id doğrulanır
            var refreshToken = Request.Cookies["refresh_token"];
            var customerId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var r = await _authService.Logout(customerId, refreshToken);
            Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/auth" });
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Açıklayıcı yorum: Refresh token'ı httpOnly+Secure+SameSite cookie olarak yaz.
        // httpOnly -> JS erişemez (XSS'te token çalınamaz), Secure -> yalnız HTTPS, SameSite=Strict -> CSRF azaltma.
        private void SetRefreshTokenCookie(string refreshToken, DateTime expires)
        {
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expires,
                Path = "/api/auth"
            });
        }


        // 2FA doğrulama - login sonrası e-posta OTP'sini doğrular, token verir
        [HttpPost("verify-2fa")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorDto dto)
        {
            var r = await _authService.VerifyTwoFactor(dto.email, dto.code);
            return StatusCode((int)r.Item1, r.Item2);
        }

    }
}
