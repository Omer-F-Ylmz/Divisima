using System.Net;
using System.Security.Claims;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.Annotations;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Ödeme controller'ı. Sahiplik JWT'den doğrulanır; callback+webhook anonim ama imzalı.
    [Route("api/[controller]")]
    [ApiController]
    [SwaggerTag("Güvenli Iyzico ödeme")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _config;
        public PaymentController(IPaymentService paymentService, IConfiguration config)
        {
            _paymentService = paymentService;
            _config = config;
        }

        // Açıklayıcı yorum: JWT'deki gerçek kullanıcı id (sahiplik kontrolü buradan gelir, client'tan değil)
        private int CurrentCustomerId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpPost("initialize")]
        [EnableRateLimiting("payment")]
        [RequireUserType(UserTypeEnum.Customer)]
        [SwaggerOperation(Summary = "Ödeme başlat", Description = "Checkout Form başlatır. Kullanıcı yalnızca kendi siparişini ödeyebilir.")]
        public async Task<IActionResult> Initialize([FromBody] PaymentInitRequestDto dto)
        {
            // Açıklayıcı yorum: authenticatedCustomerId JWT'den - IDOR engeli
            var r = await _paymentService.Initialize(dto, CurrentCustomerId);
            return StatusCode((int)r.Item1, r.Item2);
        }

        // Açıklayıcı yorum: 3DS callback - Iyzico POST eder (anonim ama imza doğrulanır)
        //
        // E2 - TARAYICI YONLENDIRMESI: bu ucu Iyzico KULLANICININ TARAYICISI uzerinden POST eder.
        // Onceden ham JSON donuyordu; musteri odeme sonunda beyaz bir sayfada {"success":true}
        // gorup akistan dusuyordu. Artik storefront'un sonuc sayfasina 302 ile donuyor.
        //
        // SINIRLAR (bilincli):
        //  - HandleCallback'e DOKUNULMADI: imza + sunucu-sunucu retrieve + atomik gecis + yan
        //    etkiler aynen calisiyor. Yalniz bu action'in YANIT BICIMI degisti.
        //  - Webhook (bant-disi, sunucu-sunucu) JSON donmeye DEVAM EDIYOR - onu bir tarayici
        //    okumuyor, yonlendirme oraya zarar verirdi.
        //  - Storefront:BaseUrl tanimsizsa ESKI davranis (JSON) korunur; boylece yapilandirma
        //    eksik bir ortamda callback sessizce bozulmaz.
        [HttpPost("callback")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ödeme callback", Description = "Iyzico callback. İmza + sunucu-sunucu doğrulama ile işlenir; tarayıcı storefront sonuç sayfasına yönlendirilir.")]
        public async Task<IActionResult> Callback([FromForm] PaymentCallbackRequestDto dto)
        {
            // Siparis id'si islemden ONCE cozulur: basarisiz dalda da sonuc sayfasi siparisi
            // gosterebilsin. Salt-okur cagri, HandleCallback'i etkilemez.
            var orderId = await _paymentService.GetOrderIdByTokenAsync(dto.token);

            // E2b: TARAYICI yolu. Iyzico CF callback POST-unda YALNIZ "token" gonderiyor - imza
            // alani HIC yok (olculdu: Network > callback > Payload > Form Data). Bu yuzden burada
            // imza ZORUNLU DEGIL; otorite sunucu-sunucu retrieve + token zaman asimi + tutar/fraud
            // kontrolleridir. Imza yine de gelirse HandleCallback onu DOGRULAR.
            var r = await _paymentService.HandleCallback(dto, imzaZorunlu: false);

            var storefront = (_config["Storefront:BaseUrl"] ?? "").TrimEnd('/');
            if (string.IsNullOrWhiteSpace(storefront))
                return StatusCode((int)r.Item1, r.Item2);   // yapilandirma yok - eski davranis

            var status = r.Item1 == HttpStatusCode.OK ? "success" : "failed";
            var url = $"{storefront}/index.html#/odeme/sonuc?order={orderId}&status={status}";
            return Redirect(url);
        }

        // Açıklayıcı yorum: Webhook - Iyzico'nun bant-dışı bildirimi (callback kaybolursa yedek teyit).
        // Aynı güvenli HandleCallback mantığını kullanır; idempotent olduğundan çift işlem güvenli.
        [HttpPost("webhook")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Ödeme webhook", Description = "Iyzico bant-dışı bildirim (yedek teyit). İmza doğrulanır, idempotent.")]
        public async Task<IActionResult> Webhook([FromBody] PaymentCallbackRequestDto dto)
        {
            // E2b: WEBHOOK yolu - imza AYNEN ZORUNLU. Bant-disi bildirim sunucu-sunucu gelir ve
            // tarayici tarafindaki olcum (imza yok) BURAYI BAGLAMAZ. Acikca yaziliyor ki gelecekte
            // varsayilana bakip yorum yapilmasin.
            var r = await _paymentService.HandleCallback(dto, imzaZorunlu: true);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
