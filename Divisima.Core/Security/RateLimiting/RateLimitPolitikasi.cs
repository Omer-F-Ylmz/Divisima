using System;
using Microsoft.Extensions.Configuration;

namespace Divisima.Core.Security.RateLimiting
{
    // === DALGA D / D5 - RATE LIMIT KOVALARININ TEK KAYNAGI ==============================
    //
    // OLCULEN ONCE-DURUM: ayni politika IKI YERDE, FARKLI degerlerle tanimliydi.
    //
    //   kova     | YERLESIK yol (Program.cs AddRateLimiter)      | REDIS yolu (middleware)
    //   auth     | 10/dk, RateLimit:AuthPermitLimit'ten OKUNUR   | 5/dk, KAYNAKTA SABIT
    //   payment  | 10/dk, RateLimit:PaymentPermitLimit'ten       | 10/dk, sabit
    //   global   | 100/dk                                        | 100/dk, sabit
    //
    // Ustelik `app.UseRateLimiter()` YALNIZCA `Redis:Enabled=false` dalinda cagriliyordu.
    // Uretimde bayrak TRUE oldugu icin:
    //   * `[EnableRateLimiting("auth"/"payment")]` oznitelikleri URETIMDE ETKISIZDI,
    //   * `RateLimit:AuthPermitLimit` ayari URETIMDE HIC OKUNMUYORDU,
    //   * auth kovasi uretimde 10 degil 5 idi.
    // `ops/deployment-checklist.md`'deki "rate limit esikleri prod trafigine gore ayarlandi"
    // maddesi bu yuzden URETIMDE KARSILIKSIZDI.
    //
    // Ayrisma YALNIZ auth kovasindaydi (payment ve global iki yolda da ayniydi) - yani bilincli
    // bir tasarim tercihi degil, GOZDEN KACMIS bir sapmaydi.
    //
    // Bu sinif kova tanimlarini TEK YERE indirir: hem `AddRateLimiter` hem
    // `RedisRateLimitMiddleware` buradan okur. Yeni bir kova ya da esik eklendiginde iki yol
    // otomatik olarak ayni degeri gorur; ayrisma YAPISAL olarak imkansiz hale gelir.
    public sealed class RateLimitPolitikasi
    {
        public const string AuthKapsami = "auth";
        public const string OdemeKapsami = "payment";
        public const string GenelKapsam = "global";

        // Pencere iki yolda da 1 dakikaydi; tek yerde sabitlenir.
        public int PencereSaniye { get; }
        public int AuthLimiti { get; }
        public int OdemeLimiti { get; }
        public int GenelLimit { get; }

        public RateLimitPolitikasi(int authLimiti, int odemeLimiti, int genelLimit, int pencereSaniye = 60)
        {
            AuthLimiti = authLimiti;
            OdemeLimiti = odemeLimiti;
            GenelLimit = genelLimit;
            PencereSaniye = pencereSaniye;
        }

        // Varsayilanlar ONCEDEN YERLESIK YOLDA olan degerlerdir (10/10/100). Redis yolundaki
        // 5 BILINCLI OLARAK TERK EDILDI: iki yoldan biri secilecekse, YAPILANDIRILABILIR ve
        // belgelenmis olani kazanmalidir - aksi halde checklist'teki ayar yine yalan olurdu.
        public static RateLimitPolitikasi Olustur(IConfiguration cfg)
        {
            int Oku(string anahtar, int varsayilan) =>
                int.TryParse(cfg?[anahtar], out var v) && v > 0 ? v : varsayilan;

            return new RateLimitPolitikasi(
                authLimiti: Oku("RateLimit:AuthPermitLimit", 10),
                odemeLimiti: Oku("RateLimit:PaymentPermitLimit", 10),
                genelLimit: Oku("RateLimit:GlobalPermitLimit", 100));
        }

        // ═══ FAZ 0 / K7 - KOVA SECIMININ TEK SAF FONKSIYONU ════════════════════════════════
        //
        // OLCULEN ONCE-DURUM: limitler D5'te tek kaynaga indirilmisti ama YOL -> KOVA ESLESMESI
        // IKI AYRI EL YAZMASIYDI: (a) asagidaki `KapsamSec` dort alt-dizgesi (Redis yolu),
        // (b) controller'lardaki [EnableRateLimiting] oznitelikleri (yerlesik yol).
        // Ayrisan uclar (oznitelikte "auth", KapsamSec'te "global"): guest-checkout/place,
        // price-drop/subscribe|unsubscribe, stocknotification/subscribe|unsubscribe,
        // seller/auth/login|register, auth/reset-password|resend-verification|verify-2fa|
        // logout|refresh. Etkin limit min(10,100)=10 oldugu icin GOZLEMLENEBILIR SONUC AYNIYDI;
        // ayrisan sey kovanin PAYLASIMIYDI.
        //
        // ══ GF-1b / K9 (GF1-B12) - BU YORUMUN SAYISI BAYATLAMISTI ═════════════════════════
        // Yukarida "6 dosya 9 yer" yaziyordu; o gun DOGRUYDU ama SAYI SABITLENDIGI icin
        // kodla birlikte YASLANDI. Bugun OLCULDU (`grep -rn "^[[:space:]]*\[EnableRateLimiting("
        // Divisima.API/`): **7 dosya, 9 yer**. Ikisi birden degisti - PaymentController'daki
        // UC action-duzeyi oznitelik SINIF duzeyinde TEKE indirildi (-2) ve GF-1b/K2
        // AccountController'a `account/change-password` icin BIR tane ekledi (+1), yani dosya
        // sayisi 6'dan 7'ye cikti, yer sayisi 9'da KALDI - toplam ayni oldugu icin fark
        // "toplama bakan" bir okuyucuya GORUNMEZDI.
        // `account/change-password` da yukaridaki AYRISAN UCLAR listesine girer: oznitelikte
        // "auth", `KapsamSec`te "global" (asagidaki dort alt-dizgeden hicbiri eslesmiyor).
        // DERS: yoruma SAYI yazilacaksa URETEN IFADESIYLE yazilir (MK-3); aksi halde yorum,
        // kodun bugun YAPMADIGI seyi anlatir hale gelir.
        //
        // COZUM: OZNITELIK TEK KAYNAK. Middleware kovayi ONCE endpoint metadata'sindaki
        // EnableRateLimitingAttribute.PolicyName'den alir; metadata YOKSA `KapsamSec` YEDEK
        // kalir. Bu fonksiyon o cozumlemenin SAF halidir - HttpContext almaz, dolayisiyla
        // birim olarak pinlenebilir (p-k7a/b/c).
        //
        // ADIM 0'DA IKI PARCA OLCULDU (FAZ 0):
        //   (i)  EnableRateLimitingAttribute.PolicyName public okunabiliyor -> DERLEYICI KANITI
        //        (gecici tani ile derlendi: 0 error CS).
        //   (ii) Gercek boru hattinda, RedisRateLimit middleware KONUMUNDA endpoint COZULMUS:
        //          /api/auth/login           endpointNull=False  policy=auth
        //          /api/guest-checkout/place endpointNull=False  policy=auth
        //          /api/payment/webhook      endpointNull=False  policy=payment
        //          /api/product/get/1        endpointNull=False  policy=-
        //          /api/olmayan-yol          endpointNull=TRUE   policy=-     <- YEDEK SART
        //        Sebep olculdu: uygulama `app.UseRouting()`i ACIKCA cagirmiyor, yonlendirme
        //        boru hattinin BASINA ekleniyor (Sprint 8 madde 9 bulgusu). Ayni desen
        //        IdempotencyMiddleware'de ZATEN kullaniliyor.
        //
        // BILINCLI DAVRANIS DEGISIKLIGI: yukarida sayilan uclar dagitik tarafta artik "global"
        // degil "auth" kovasini PAYLASIR. Etkin limit zaten 10 idi (min); degisen sey paylasimin
        // SIKILASMASI - ve bu, oznitelik tarafinin ZATEN yaptigi sey.
        public (string kapsam, int limit) KovaSec(string? policyAdi, string yol)
        {
            // 1) OZNITELIK (tek kaynak) - endpoint metadata'sindan geldiyse o kazanir.
            if (!string.IsNullOrWhiteSpace(policyAdi))
            {
                if (string.Equals(policyAdi, AuthKapsami, StringComparison.Ordinal))
                    return (AuthKapsami, AuthLimiti);
                if (string.Equals(policyAdi, OdemeKapsami, StringComparison.Ordinal))
                    return (OdemeKapsami, OdemeLimiti);
                // TANINMAYAN policy adi: sessizce yutulmaz da uydurulmaz da - yedege duser.
                // (Yerlesik limiter zaten kendi policy'sini uygular; burada yalnizca DAGITIK
                //  sayacin hangi kovaya yazacagini seciyoruz.)
            }

            // 2) YEDEK - endpoint cozulmemis (404 vb.) ya da oznitelik yok.
            return KapsamSec(yol);
        }

        // YOL -> KAPSAM/LIMIT secimi. YEDEK yol (bkz. KovaSec). Endpoint metadata'si olmayan
        // istekler (rota eslesmeyen 404'ler) ve oznitelik tasimayan uclar buradan gecer.
        //
        // KULTURSUZ ESLESME (KALITE SUPURMESI B3 - bedeli odendi): yol bir MAKINE dizgesidir.
        // `ToLower()` KULLANILMAZ - uygulama tr-TR'ye pinli oldugu icin 'I' -> 'ı' (U+0131)
        // olur ve '/API/AUTH/LOGIN' auth kovasindan KACARDI. OrdinalIgnoreCase zorunludur.
        public (string kapsam, int limit) KapsamSec(string yol)
        {
            yol ??= "";
            if (yol.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
                || yol.Contains("/auth/register", StringComparison.OrdinalIgnoreCase)
                || yol.Contains("/auth/forgot", StringComparison.OrdinalIgnoreCase))
                return (AuthKapsami, AuthLimiti);

            if (yol.Contains("/payment/", StringComparison.OrdinalIgnoreCase))
                return (OdemeKapsami, OdemeLimiti);

            return (GenelKapsam, GenelLimit);
        }
    }
}
