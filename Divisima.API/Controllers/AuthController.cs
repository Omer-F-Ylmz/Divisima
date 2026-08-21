using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    //
    // SPRINT 8 MADDE 6 - BU YORUM ARTIK DOGRU. Onceden YALANDI: E1'de olculdu ki
    // SetRefreshTokenCookie TANIMLI ama HIC CAGRILMIYOR; refresh token login yanitinin
    // GOVDESINDE donuyor, /api/auth/refresh de govdede bekliyordu ve Logout hic yazilmayan
    // bir cookie'yi okuyordu. Yani "httpOnly cookie" modeli YARIMDI - yazma yolu OLUYDU ve
    // token JS'in erisebildigi yerde (localStorage) duruyordu.
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        // COOKIE ADLARI TEK YERDE: AntiforgeryMiddleware da ayni adlari okuyor
        // (refresh_token / csrf_token). Ad degisirse iki taraf birlikte degismeli.
        private const string RefreshCookie = "refresh_token";
        private const string CsrfCookie = "csrf_token";

        // COOKIE KAPSAMLARI - IKISI FARKLI, SEBEBI OLCULDU (tarayicida):
        //  refresh_token -> "/api/auth". Tarayici onu yalniz kimlik uclarina gonderir; katalog,
        //    sepet, odeme gibi yuzlerce istekte gereksiz yere tasinmaz ve AntiforgeryMiddleware
        //    de (yalniz bu cookie VARSA devreye giriyor) dar kalir.
        //  csrf_token -> "/". ZORUNLU: `document.cookie` yalnizca GECERLI SAYFA YOLUYLA eslesen
        //    cerezleri dondurur. "/api/auth" ile yazildiginda storefront sayfasi (/index.html)
        //    cerezi HIC GORMUYOR - olculdu: giristen sonra document.cookie BOS dondu. Istemci
        //    degeri okuyamayinca X-CSRF-Token'i dolduramaz ve yenileme kalici 403 olur.
        //    Double-submit'in tum mantigi bu degerin okunabilmesine dayanir.
        private const string RefreshPath = "/api/auth";
        private const string CsrfPath = "/";

        public AuthController(IAuthService authService, IWebHostEnvironment env, IConfiguration config)
        {
            _authService = authService;
            _env = env;
            _config = config;
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
            OturumCerezleriniYaz(result.Item2);
            return StatusCode((int)result.Item1, result.Item2);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Token yenile", Description = "Refresh token ile yeni access token alır (oturum uzatma).")]
        [ProducesResponseType(typeof(SuccessDataResult<CustomerLoginResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ErrorResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Refresh()
        {
            // SPRINT 8 MADDE 6: token artik GOVDEDEN DEGIL httpOnly cookie'den okunur.
            // Govde parametresi KALDIRILDI - kalsaydi bir istemci token'i govdede gondermeye
            // devam edip cookie modelini SESSIZCE bypass edebilirdi (ve o token yine JS'in
            // erisebildigi bir yerde durmak zorunda kalirdi). Tek yol vardir.
            var cookieToken = Request.Cookies[RefreshCookie];
            if (string.IsNullOrWhiteSpace(cookieToken))
                return StatusCode((int)HttpStatusCode.Unauthorized,
                    new ErrorResult("Oturum bulunamadı. Lütfen tekrar giriş yapın."));

            var result = await _authService.RefreshToken(new RefreshTokenRequestDto { refresh_token = cookieToken });
            OturumCerezleriniYaz(result.Item2);
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
            // SPRINT 8 MADDE 6: bu satir ARTIK GERCEKTEN CALISIYOR - onceden okudugu cookie hic
            // yazilmiyordu, yani Logout oturumu sunucu tarafinda IPTAL EDEMIYORDU (E1'de olculdu).
            var refreshToken = Request.Cookies[RefreshCookie];
            var customerId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var r = await _authService.Logout(customerId, refreshToken);
            OturumCerezleriniSil();
            return StatusCode((int)r.Item1, r.Item2);
        }

        // ── SPRINT 8 MADDE 6: OTURUM CEREZLERI ────────────────────────────────────────────
        //
        // Basarili bir Login / Refresh / VerifyTwoFactor sonucunda UC IS birlikte yapilir:
        //   1) refresh_token httpOnly cookie'ye yazilir,
        //   2) csrf_token cookie'si yazilir (double-submit deseninin ikinci yarisi),
        //   3) refresh token YANIT GOVDESINDEN SILINIR.
        // Ucu de AYNI yerde: biri eksik kalirsa model yarim kalir ve sessizce bozulur -
        // E1/E3'te tam olarak bu yasandi (yazma yolu oluydu, kimse fark etmedi).
        private void OturumCerezleriniYaz(Result result)
        {
            if (result is not SuccessDataResult<CustomerLoginResponseDto> ok) return;
            var data = ok.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.refresh_token)) return;

            // Oturum omru: access token'in "expiration"i DEGIL - refresh token daha uzun yasar.
            // UserSession kaydinin omruyle hizali olsun diye 30 gun (AuthManager'daki uretimle ayni
            // mantik; cookie erken silinirse kullanici gereksiz yere cikis yapmis olur).
            var expires = DateTime.UtcNow.AddDays(30);

            Response.Cookies.Append(RefreshCookie, data.refresh_token, CerezSecenekleri(httpOnly: true, expires, RefreshPath));

            // CSRF TOKEN'I JS-OKUNUR OLMAK ZORUNDA (HttpOnly = FALSE) - bu bir eksiklik degil,
            // DOUBLE-SUBMIT DESENININ GEREGI: istemci ayni degeri X-CSRF-Token basliginda geri
            // gondermek zorunda ve bunu ancak okuyabilirse yapar. Guvenlik su varsayima dayanir:
            // baska bir origin'deki saldirgan kurbanin tarayicisina istek YAPTIRABILIR ama o
            // cookie'yi OKUYAMAZ (same-origin policy) ve dolayisiyla basligi DOLDURAMAZ.
            // Deger tahmin edilemez olmali - kriptografik rastgele uretiliyor.
            //
            // HEX, base64 DEGIL (OLCULDU): base64 ciktisi "+", "/" ve dolgu "=" karakterleri
            // icerir; bunlar Cookie basliginda ayrac/kacis sorunlari cikariyor ve deger istemciyle
            // sunucu arasinda BOZULABILIYOR - double-submit karsilastirmasi da sessizce basarisiz
            // olur (403). Hex yalniz [0-9A-F] uretir, hicbir ayracla cakismaz.
            var csrf = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            Response.Cookies.Append(CsrfCookie, csrf, CerezSecenekleri(httpOnly: false, expires, CsrfPath));

            // GOVDEDEN SIL: modelin BUTUN amaci token'in JS'in erisebildigi yerde durmamasi.
            // Govdede birakilsaydi istemci onu yine localStorage'a koyabilir ve httpOnly hicbir
            // sey kazandirmazdi.
            data.refresh_token = null!;
        }

        private void OturumCerezleriniSil()
        {
            // Silme secenekleri YAZMA secenekleriyle AYNI olmak zorunda (Path/Domain/Secure/
            // SameSite). Aksi halde tarayici "baska bir cookie" sanar ve ESKISI SILINMEZ.
            Response.Cookies.Delete(RefreshCookie, CerezSecenekleri(httpOnly: true, null, RefreshPath));
            Response.Cookies.Delete(CsrfCookie, CerezSecenekleri(httpOnly: false, null, CsrfPath));
        }

        private CookieOptions CerezSecenekleri(bool httpOnly, DateTime? expires, string path)
        {
            var o = new CookieOptions
            {
                HttpOnly = httpOnly,

                // SECURE: HER ORTAMDA ACIK - ortam guard'i YOK.
                // "Development'ta kapatmak gerekir mi?" sorusu TARAYICIDA OLCULDU, tahmin
                // edilmedi: yerel akis duz HTTP uzerinde kosuyor (storefront :5173 -> API :5000)
                // ve Secure bir cookie'nin orada saklanmayacagindan suphelenilmisti. Olcum
                // tersini gosterdi - tarayicilar "localhost"u guvenilir origin sayiyor:
                //   giris -> document.cookie csrf_token'i GORDU,
                //   access token bilerek bozuldu -> sessiz yenileme BASARILI (yani httpOnly
                //   Secure refresh cookie de saklanmis VE geri gonderilmis).
                // Ortam guard'i eklenmedi: eklenseydi kod, hicbir sey kazandirmadan uretim
                // disinda Secure'u kapatan bir yol tasiyacakti.
                Secure = true,

                // SAMESITE=STRICT: cookie yalnizca AYNI SITE'dan gelen isteklerde gonderilir.
                // Dev'de storefront ve API ayni host (localhost, farkli PORT) - portlar
                // same-site hesabina GIRMEZ, dolayisiyla calisir. Uretimde divisima.com ve
                // api.divisima.com ayni kayitli alan adini paylastigi icin yine same-site.
                SameSite = SameSiteMode.Strict,

                // PATH: cagiran belirler - iki cookie'nin kapsami FARKLI (bkz. RefreshPath /
                // CsrfPath sabitlerindeki olcum notu).
                Path = path
            };
            if (expires.HasValue) o.Expires = expires.Value;

            // DOMAIN: dev'de BOS (host-only). Uretimde storefront ve API AYRI ALT ALAN ADLARINDA
            // (divisima.com / api.divisima.com); host-only bir cookie'yi storefront'taki JS
            // OKUYAMAZ ve double-submit yarim kalir. Bu yuzden ust alan adi ("
            // .divisima.com") yapilandirmadan verilir. OLCULEN kisit - varsayim degil.
            var domain = _config["Cookies:Domain"];
            if (!string.IsNullOrWhiteSpace(domain)) o.Domain = domain;
            return o;
        }


        // 2FA doğrulama - login sonrası e-posta OTP'sini doğrular, token verir
        [HttpPost("verify-2fa")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorDto dto)
        {
            var r = await _authService.VerifyTwoFactor(dto.email, dto.code);
            // 2FA de bir GIRIS yoludur - ayni oturum cerezleri burada da yazilmali. Atlanirsa
            // 2FA acik kullanicilar cookie'siz kalir ve ilk yenilemede oturumu duser.
            OturumCerezleriniYaz(r.Item2);
            return StatusCode((int)r.Item1, r.Item2);
        }

    }
}
