using System.Net;
using System.Security.Claims;
using Asp.Versioning;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Authorization;
using Divisima.Core.Utilities.Constants;
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
    // ═══ FAZ 0 / K2 - RATE LIMIT SINIF DUZEYINE TASINDI ════════════════════════════════
    // ONCE: [EnableRateLimiting("payment")] UC ACTION'DA AYRI AYRI duruyordu (initialize /
    // callback / webhook). Bugun bosluk YOKTU - ucu de isaretliydi - ama yarin eklenecek bir
    // 4. action isaretlenmezse YERLESIK yolda "global" (100/dk) kovasina duserdi.
    // TASIMANIN DAVRANISI DEGISTIRMEDIGI UC AYAKLA OLCULDU (FAZ 0):
    //   1) Controller'in 3 action'inin 3'u de ZATEN ayni policy'yi tasiyordu - kapsam tam.
    //   2) Depoda [DisableRateLimiting] HIC kullanilmiyor (0 eslesme) - sinif duzeyi hicbir
    //      action'i beklenmedik sekilde kapsamaz.
    //   3) Iki mevcut 429 pini tasimayi bagimsiz dogrular (Callback_PAYMENT_KOVASINDA... ve
    //      Webhook_PAYMENT_KOVASINDA...); initialize icin pin YOKTU, FAZ 0'da eklendi.
    [EnableRateLimiting("payment")]
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
        // SUPHELI #17 KAPANDI (Sprint 8 sonrasi mini dalga): bu action da "payment" kovasina alindi.
        // OLCULDU: [EnableRateLimiting("payment")] yalniz Initialize uzerindeydi; Callback yerlesik
        // limiter yolunda YALNIZ GlobalLimiter'in 100/dk'sindaydi. Redis yolu ise path eslesmesiyle
        // (/payment/) onu ZATEN 10/dk'ya bagliyordu - yani iki yapilandirma AYRISIYORDU. Bu, bilincli
        // bir karar degil kapsam disinda kalmis bir bosluktu; iki yol artik ayni davraniyor.
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
            //
            // KANAL: BrowserCallback. Token YASI SINIRI (30 dk) BU YOLDA AYNEN UYGULANIR - bu
            // istek bir TARAYICIDAN geliyor ve eski bir formun yeniden gonderilmesi gercek bir
            // senaryodur. Webhook'taki gevseme buraya SIZMAZ; ayrimi enum tasiyor.
            var r = await _paymentService.HandleCallback(dto, PaymentNotificationChannel.BrowserCallback);

            var storefront = (_config["Storefront:BaseUrl"] ?? "").TrimEnd('/');
            if (string.IsNullOrWhiteSpace(storefront))
                return StatusCode((int)r.Item1, r.Item2);   // yapilandirma yok - eski davranis

            // ══ GF-6 / F2 (S-1) - UCUNCU DURUM `review` ═══════════════════════════════════
            //
            // OLCULEN ONCE-DURUM: bu satir YALNIZ iki durum uretiyordu ve "para alindi ama
            // siparis IPTALDI" dali 200 dondugu icin `success` oluyordu - musteri iptal kalan
            // siparis icin "odeme basarili" ekrani goruyordu (L3 denetcisi olctu).
            //
            // AYIRT ETME MESAJ KIMLIGIYLE: `HandleCallback` ucuncu durumu 200 + AYRI bir mesaj
            // sabitiyle bildirir (gerekce Messages.PaymentReceivedOrderCancelled'in yaninda).
            // Karsilastirma ORDINAL - mesaj bir KIMLIK dizgesi olarak kullaniliyor, kulturlu
            // casing YASAK (CLAUDE.md 6c). Bu bag PINLI: sabit degisirse pin kirmizi verir.
            var iptalliOdeme = string.Equals(r.Item2?.Message,
                Messages.PaymentReceivedOrderCancelled, StringComparison.Ordinal);
            var status = iptalliOdeme ? "review"
                : (r.Item1 == HttpStatusCode.OK ? "success" : "failed");
            var url = $"{storefront}/index.html#/odeme/sonuc?order={orderId}&status={status}";
            return Redirect(url);
        }

        // Açıklayıcı yorum: Webhook - Iyzico'nun bant-dışı bildirimi (callback kaybolursa yedek teyit).
        // Aynı güvenli HandleCallback mantığını kullanır; idempotent olduğundan çift işlem güvenli.
        //
        // ══ SPRINT 8 MADDE 9 - GERCEK BILDIRIMLE OLCULDU (22 Agustos 2026) ══════════════════
        //
        // Public tunel uzerinden GERCEK Iyzico bildirimi yakalandi (User-Agent Apache-HttpClient,
        // sunucu-sunucu). IKI BAGIMSIZ engel olculdu; ikisi de burada kapatildi. Canli kanit:
        // siparis #33 - para Iyzico'da SUCCESS (iyziPaymentId 37415135), bizde Pending. Yani
        // "callback kayboldu" senaryosunda TEK kurtarma yolu CALISMIYORDU.
        //
        // (1) VERSIYONLAMA. Iyzico her bildirimde "X-Api-Version: V1" yolluyor. Program.cs'teki
        //     HeaderApiVersionReader("X-Api-Version") bu degeri ayristiramiyor ve istek
        //     CONTROLLER'A HIC ULASMADAN bos govdeli 400 ile reddediliyordu (log:
        //     "Request contained the API version 'V1', which is not valid").
        //     DIKKAT - 400'U COZEN SEY ASAGIDAKI [ApiVersionNeutral] DEGILDIR. Uc kez olculdu:
        //     attribute action duzeyinde de controller duzeyinde de 400'u ENGELLEMIYOR, boru
        //     hattinin basina konan bir middleware de gec kaliyor (yonlendirme, dolayisiyla
        //     ApiVersionMatcherPolicy, kullanici middleware'lerinden ONCE kosuyor).
        //     Cozum OKUYUCU duzeyinde: Divisima.API.Versioning.WebhookExemptHeaderApiVersionReader
        //     yalniz BU YOLDA "X-Api-Version" basligini yok sayar (gerekce + elenen alternatifler
        //     o dosyanin basinda). Attribute NIYETI ifade ettigi icin BIRAKILDI - webhook'u bir
        //     ucuncu taraf cagirir, bizim surum sozlesmemize tabi degildir - ama tek basina
        //     yeterli DEGIL; okuyucu muafiyeti silinirse burasi kurtarmaz.
        //     Kapsamin DAR kaldigi pinli: AYNI baslikla baska bir uc HALA 400 verir.
        //
        // (2) IMZA. Gercek bildirimde imza HIC YOK: govdede "signature" alani yok, baslikta
        //     "X-Iyz-Signature" VAR ama DEGERI BOS (olculdu 22 Agu). Bu action imzayi kosulsuz
        //     zorunlu tuttugu icin (imzaZorunlu: true) her GERCEK bildirim 400 yiyordu.
        //     OTORITE ARTIK RETRIEVE: (i) token opak, yalniz bize+Iyzico'ya ait, (ii) sonuc
        //     sunucu-sunucu RetrievePaymentResultAsync ile Iyzico'dan cekilir, (iii) token 30 dk
        //     zaman asimina tabi, (iv) tutar/para birimi/fraud dogrulanir, (v) yalniz Pending
        //     odeme islenir. E2b'de CF callback icin ONAYLANAN modelin AYNISI; sebep de ayni -
        //     saglayici imza gondermiyor.
        //     GEVSEME "imzayi yok say" DEGIL, "imza YOKSA retrieve otoritesiyle isle": imza
        //     gelirse (govdede ya da baslikta) AYNEN dogrulanir ve tutmazsa istek reddedilir.
        //     Panelde bir webhook imza anahtari bulunursa baslik dogrulamasi ZATEN ACIK olur.
        //
        // (3) BEDEL VE SINIRI. Her istek potansiyel olarak bir sunucu-sunucu retrieve demek -
        //     Iyzico'ya dogru bir amplifikasyon kanali. OLCULDU ki bu kanal ZATEN dar:
        //     HandleCallback retrieve'e gelmeden once token'i BIZIM tablomuzda ariyor; bizim
        //     olmayan bir token 404 ile duser ve DISARI HIC CIKILMAZ. Retrieve'e ancak (a) bizim
        //     urettigimiz, (b) hala Pending, (c) 30 dk'dan yeni bir token ulasir.
        //     Yine de sayisal bir tavan gerekiyordu: [EnableRateLimiting("payment")] eklendi.
        //     Bu YENI bir sayi DEGIL - Redis yolu (RedisRateLimitMiddleware) bu ucu path
        //     eslesmesiyle (/payment/) ZATEN 10/dk'ya baglıyordu; yerlesik limiter yolunda ise
        //     webhook yalniz GlobalLimiter'in 100/dk'sindaydi. Iki yolun ayni davranmasi
        //     Program.cs'teki policy tanimin ACIK NIYETI ("Redis middleware'indeki payment
        //     scope (10/dk) ile tutarli"); buradaki ayrisma o niyetin bosluguydu.
        [HttpPost("webhook")]
        [AllowAnonymous]
        [ApiVersionNeutral]
        [SwaggerOperation(Summary = "Ödeme webhook", Description = "Iyzico bant-dışı bildirim (yedek teyit). İmza gelirse doğrulanır; otorite sunucu-sunucu sorgudur. İdempotent.")]
        public async Task<IActionResult> Webhook([FromBody] PaymentCallbackRequestDto dto)
        {
            // Imza govdede YOKSA baslikta aranir. Baslik adi OLCULDU: "X-Iyz-Signature".
            //
            // BILEREK YAPILMADI: dokumanlarda gecen "X-IYZ-SIGNATURE-V3" basligi bu dogrulayiciya
            // BAGLANMADI. Bizim VerifyCallbackSignature'imiz HMAC-SHA256(secretKey, token)
            // hesaplar; V3 imzasi FARKLI bir govde uzerinden uretilir. Olculmemis bir esleme
            // yazmak, o baslik dolmaya basladigi gun HER GERCEK bildirimi reddederdi - bugun
            // duzelttigimiz kesintinin BIREBIR aynisi. Format olculunce buraya eklenir.
            if (string.IsNullOrWhiteSpace(dto.signature))
            {
                var basliktakiImza = Request.Headers["X-Iyz-Signature"].ToString();
                if (!string.IsNullOrWhiteSpace(basliktakiImza))
                    dto.signature = basliktakiImza.Trim();
            }

            // KANAL: ProviderWebhook. Imzanin yani sira TOKEN YASI SINIRI da gevser (SUPHELI #15).
            // Saglayici bildirimi geciktirebilir ya da saatler sonra yeniden deneyebilir; 30 dk
            // siniri burada uygulaninca GECIKMIS ama GERCEK bir bildirim, parasi ALINMIS bir
            // odemeyi "Failed" diye defterliyordu. Gerekce ve sinirlar enum'da.
            var r = await _paymentService.HandleCallback(dto, PaymentNotificationChannel.ProviderWebhook);
            return StatusCode((int)r.Item1, r.Item2);
        }
    }
}
