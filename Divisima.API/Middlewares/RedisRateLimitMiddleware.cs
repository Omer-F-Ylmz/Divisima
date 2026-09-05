using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.RateLimiting;
using Divisima.Core.Utilities.Caching;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Yol bazlı dağıtık rate limit middleware (auth/ödeme sıkı, genel gevşek).
    //
    // DALGA D / D5 - IKI DEGISIKLIK:
    //  (1) Kova tanimlari artik `RateLimitPolitikasi`den geliyor; auth limiti KAYNAKTA SABIT 5
    //      DEGIL, yapilandirmadan okunuyor ve YERLESIK yolla AYNI degeri goruyor.
    //  (2) Middleware artik HER ZAMAN pipeline'da (eskiden yalniz `Redis:Enabled=true` iken).
    //      Gerekce: `IDistributedRateLimiter` her iki dalda da kayitli (Redis ya da in-memory),
    //      yani middleware'in Redis'e bagimliligi YOK - yalnizca ARKA DEPOSU degisiyor.
    //      Boylece dev/test ve URETIM AYNI BORU HATTINI kosuyor; onceden uretimin gercek
    //      rate limit yolu hicbir testte kosmuyordu (olculdu) ve ayrisma bu yuzden gorunmedi.
    public class RedisRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedRateLimiter _limiter;
        private readonly RateLimitPolitikasi _politika;

        public RedisRateLimitMiddleware(RequestDelegate next, IDistributedRateLimiter limiter, RateLimitPolitikasi politika)
        {
            _next = next;
            _limiter = limiter;
            _politika = politika;
        }

        // ══ GF-5 / K2 (D6) - 429 ARTIK GUVENLIK OLAYI YAZIYOR ══════════════════════════════
        //
        // SERVISLER METOT ENJEKSIYONUYLA ALINIR, CTOR'DAN DEGIL - CAPTIVE DEPENDENCY:
        // middleware TEK ORNEKTIR (pipeline'da bir kez kurulur), `ISecurityEventService` ise
        // SCOPED (`AutofacBusinessModule.cs` InstancePerLifetimeScope). Ctor'a almak scoped
        // servisi - ve onun `DbContext`ini - TUM UYGULAMA OMRUNE hapsederdi. Depodaki dogru
        // kalip ZATEN var ve aynen izleniyor: `IdempotencyMiddleware.cs`
        // (`InvokeAsync(HttpContext, ICacheService)`) ve `TokenBlacklistMiddleware.cs`.
        public async Task InvokeAsync(HttpContext context, ISecurityEventService securityEvents, ICacheService cache)
        {
            // ══ KALITE SUPURMESI B3 - URL YOLU KIMLIK DIZGESIDIR, KULTURSUZ ESLESIR ═════════
            // ONCEKI HALI: `Path.Value?.ToLower()` + kultur duyarli `Contains`.
            // Uygulama tr-TR'ye pinli oldugu icin 'I' -> 'ı' oluyordu. OLCULDU:
            //   '/API/AUTH/LOGIN'.ToLower()  ->  '/apı/auth/logın'
            //   .Contains("/auth/login")     ->  FALSE     (invariant'ta TRUE)
            // ASP.NET rotalama BUYUK/KUCUK HARF DUYARSIZ oldugundan /API/AUTH/LOGIN gecerli
            // bir istektir; yani saldirgan yalnizca URL'yi buyuk harfle yazarak 5/dk'lik
            // KABA KUVVET savunmasindan kacip 100/dk'lik genel kovaya dusuyordu.
            // Yol bir MAKINE dizgesidir: OrdinalIgnoreCase ile eslenir, ToLower KULLANILMAZ.
            var path = context.Request.Path.Value ?? "";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // ═══ FAZ 0 / K7 - KOVA SECIMI OZNITELIKTEN TURER, YOL LISTESI YEDEKTIR ═════════
            // ONCE: yalnizca `KapsamSec(path)` - yani yol->kova eslesmesi controller'lardaki
            // [EnableRateLimiting] oznitelikleriyle AYRI BIR EL YAZMASIYDI ve ayrisiyordu.
            // SONRA: policy adi ENDPOINT METADATA'SINDAN okunur (oznitelik = TEK KAYNAK);
            // metadata yoksa (rota eslesmeyen 404 vb.) `KapsamSec` YEDEK kalir.
            // ADIM 0'da olculdu: bu middleware KONUMUNDA endpoint COZULMUS oluyor - uygulama
            // `app.UseRouting()`i acikca cagirmadigi icin yonlendirme boru hattinin BASINDA.
            // Ayni desenin depodaki precedent'i: IdempotencyMiddleware (GetEndpoint().Metadata).
            // Karar mantiginin TAMAMI saf fonksiyonda (RateLimitPolitikasi.KovaSec) - burada
            // kopyasi TUTULMAZ; gerekce ve olcum ciktilari orada.
            var policyAdi = context.GetEndpoint()?.Metadata
                ?.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
                ?.PolicyName;
            var (scope, limit) = _politika.KovaSec(policyAdi, path);
            var window = _politika.PencereSaniye;

            var result = await _limiter.CheckAsync($"{scope}:{ip}", limit, window);

            // Açıklayıcı yorum: Standart rate limit başlıkları
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();

            if (!result.Allowed)
            {
                // ══ GF-5 / K2 (D6) - RED DALININ IZI ═══════════════════════════════════════
                //
                // ORNEKLEME ZORUNLU: bu dal bir SEL anidir - saniyede yuzlerce kez kosabilir ve
                // her red icin satir yazmak, tam da DB'nin zorlandigi anda yazma yuku EKLERDI
                // (ustelik `DataRetentionJob.cs` non-Critical satirlari BIR YIL tutuyor).
                // `TryAddAsync` ATOMIK set-if-not-exists'tir (Redis SETNX / in-memory lock):
                // ayni (kova + IP) icin 60 saniyede YALNIZ ILK cagri true doner. Check-then-act
                // yarisi YOK - bu yuzden `ExistsAsync` + `Set` ikilisi KULLANILMADI.
                //
                // `customer_id` NULL - KABUL EDILMIS SINIR (merkez karari D6): bu middleware
                // `app.UseAuthentication()`DAN ONCE kosuyor (`Program.cs`te bu middleware UseAuthentication cagrisindan ONCE kayitli), yani
                // `context.User` HENUZ BOS. A09'un "ATIF" yarisi bu satirda KAPANMIYOR ve bu
                // bilincli bir kayittir; kapanan yari "GORUNURLUK"tur. Middleware'i
                // UseAuthentication SONRASINA almak rate limit'i kimlik dogrulamanin ARDINA
                // koyardi - yani kaba kuvvet savunmasi, korumak istedigi isin PESINE duserdi.
                //
                // OLAY YAZIMI ISTEGI DUSURMEZ: `ExceptionMiddleware` (`Program.cs`) bu
                // middleware'den ONCE kayitli, dolayisiyla buradan cikan bir istisna 429'u
                // 500'e cevirirdi. Yazma bu yuzden kendi try/catch'inde - iz KAYBOLABILIR ama
                // musterinin gordugu yanit ASLA degismez.
                try
                {
                    if (await cache.TryAddAsync($"sec-olay:429:{scope}:{ip}", TimeSpan.FromSeconds(60)))
                        // `path` KULLANICI KONTROLLUDUR ve `SatirGuvenli`den GECIRILIR:
                        // `Request.Path.Value` COZULMUS yoldur, yani URL'deki `%0D%0A`
                        // GERCEK CRLF olarak buraya iner. `detail` alani
                        // `SecurityEventManager.cs`teki Serilog sablonuna giriyor ve Serilog
                        // kontrol karakterlerini AYIKLAMAZ (GF-3/A-3 kaydi) - yani maskesiz
                        // birakmak saldirgana LOG SATIRI BOLDURURDU (sahte "SECURITY ..."
                        // satiri uydurmak dahil). `scope` ve `limit` uretim tarafindan
                        // belirlenir, kullanici giremez.
                        await securityEvents.LogAsync("RateLimitExceeded", "Warning", null, ip, null,
                            $"kova={scope} limit={limit} yol={Divisima.Core.Utilities.Text.KanitMaskesi.SatirGuvenli(path)}");
                }
                catch
                {
                    // Bilincli yutma: iz yazimi yanit sozlesmesini BOZAMAZ.
                }

                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
                await context.Response.WriteAsJsonAsync(new { success = false, message = "Çok fazla istek. Lütfen biraz sonra tekrar deneyin." });
                return;
            }

            await _next(context);
        }
    }
}
