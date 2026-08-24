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

        // YOL -> KAPSAM/LIMIT secimi. Tek yerde durur ki iki yol ayni yolu ayni kovaya atsin.
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
