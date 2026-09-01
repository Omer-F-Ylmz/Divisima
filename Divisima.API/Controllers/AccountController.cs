using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Account;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Hesap yönetimi controller'ı (thin). Tümü müşteri-kapsamlı (kendi hesabı - IDOR yok).
    [Route("api/[controller]")]
    [ApiController]
    [RequireUserType(UserTypeEnum.Customer)]
    [SwaggerTag("Hesap yönetimi - profil, şifre, tercihler")]
    public class AccountController : SecureControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet("summary")]
        [SwaggerOperation(Summary = "Hesap özeti", Description = "Profil + bakiye + tercihler (hassas alan yok).")]
        public async Task<IActionResult> Summary()
        {
            var r = await _accountService.GetSummary(CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPut("profile")]
        [SwaggerOperation(Summary = "Profil güncelle", Description = "Ad/telefon/doğum günü günceller.")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto dto)
        {
            var r = await _accountService.UpdateProfile(CurrentCustomerId, dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPost("change-password")]
        [SwaggerOperation(Summary = "Şifre değiştir", Description = "Mevcut şifre doğrulaması ile yeni şifre.")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
        {
            // GF-1 / K2: sunulan access token'in kimligi/bitisi de gecirilir - sifre degisimi
            // artik ELDEKI JETONU da iptal ediyor (bkz. AccountManager.ChangePassword).
            var r = await _accountService.ChangePassword(CurrentCustomerId, dto, CurrentJti, CurrentTokenExpiry);
            return StatusCode((int)r.Item1, r.Item2);
        }

        [HttpPut("notification-preferences")]
        [SwaggerOperation(Summary = "Bildirim tercihleri", Description = "E-posta/SMS/push bildirim opt-in/out.")]
        public async Task<IActionResult> NotificationPreferences([FromBody] NotificationPreferencesDto dto)
        {
            var r = await _accountService.UpdateNotificationPreferences(CurrentCustomerId, dto);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // FIX-1A / F1: pencere 30 -> 10 dk. Ayni isi yapan `/api/auth/account` ucu ZATEN 10
        // istiyordu; iki rota tek uygulamada birlestigine gore kapi da tek olmali - yoksa
        // saldirgan gevsek olani secer. Yeni deger uydurulmadi, iki sozlesmenin SIKI olani alindi.
        [HttpDelete("delete")]
        [RequireRecentAuth(10)]   // KVKK hesap silme GERİ ALINAMAZ -> son 10 dk içinde giriş yapılmış olmalı (step-up auth)
        [SwaggerOperation(Summary = "Hesabı sil (GDPR)", Description = "Kişisel verileri anonimleştirir + hesabı kapatır. Geri alınamaz. Yakın zamanda giriş gerektirir.")]
        public async Task<IActionResult> Delete()
        {
            var r = await _accountService.DeleteAccount(CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
