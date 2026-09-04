using Divisima.API.Middlewares;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.RateLimiting;
using Divisima.Core.Utilities.Caching;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ KALITE SUPURMESI B3 - URL YOLU KIMLIK DIZGESIDIR ═══════════════════════════════════
    //
    // OLCULEN ZARAR: RedisRateLimitMiddleware yolu `Path.Value?.ToLower()` ile kucultuyordu.
    // Uygulama tr-TR'ye pinli oldugu icin (Sprint 8 madde 13) 'I' -> 'ı' (U+0131):
    //     '/API/AUTH/LOGIN'.ToLower()  ->  '/apı/auth/logın'
    //     .Contains("/auth/login")     ->  FALSE     (invariant'ta TRUE)
    // ASP.NET rotalama BUYUK/KUCUK HARF DUYARSIZ oldugundan /API/AUTH/LOGIN gecerli bir
    // istektir. Yani saldirgan YALNIZCA URL'yi buyuk harfle yazarak 5/dk'lik KABA KUVVET
    // savunmasindan kacip 100/dk'lik "global" kovaya dusuyordu.
    //
    // NEDEN MIDDLEWARE DUZEYINDE: bu yol yalniz `Redis:Enabled=true` iken pipeline'a giriyor
    // (varsayilan false). Uctan uca bir HTTP testi gercek bir Redis sunucusu isterdi - dis
    // bagimlilik. Middleware'i DOGRUDAN kosturmak, olculen seyi (yol -> kapsam/limit secimi)
    // dis bagimlilik olmadan ve TAM olarak pinler.
    public class RateLimitPathScopeTests
    {
        // Limiter'i cagiran ANAHTARI ve LIMITI yakalar - middleware'in hangi kovayi sectigi
        // ancak bu iki degerden okunur.
        private sealed class YakalayanLimiter : IDistributedRateLimiter
        {
            public string? SonAnahtar { get; private set; }
            public int SonLimit { get; private set; }

            public Task<RateLimitResult> CheckAsync(string key, int limit, int windowSeconds)
            {
                SonAnahtar = key;
                SonLimit = limit;
                return Task.FromResult(new RateLimitResult { Allowed = true, Remaining = limit - 1 });
            }
        }

        // ══ GF-5 / K2 (D6) - 429 RED DALI ARTIK OLAY YAZIYOR: YAKALAYICILAR ════════════════
        //
        // Middleware imzasi `InvokeAsync(HttpContext, ISecurityEventService, ICacheService)`
        // oldu (METOT enjeksiyonu - captive dependency'den kacinmak icin; gerekce
        // middleware'in kendisinde). Bu sinif middleware'i DOGRUDAN kosturdugu icin iki
        // yakalayici eklendi; boylece 429 izi DIS BAGIMLILIK OLMADAN pinlenebiliyor.
        private sealed class YakalayanOlay : ISecurityEventService
        {
            public List<(string tip, string siddet, int? musteri, string? ip, string? detay)> Kayitlar { get; } = new();

            public Task LogAsync(string eventType, string severity, int? customerId, string? ip, string? userAgent, string? detail)
            {
                Kayitlar.Add((eventType, severity, customerId, ip, detail));
                return Task.CompletedTask;
            }

            public Task SahiplikIhlaliAsync(string kaynak, int kaynakId, int? istekSahibi) =>
                LogAsync("IdorAttempt", "Warning", istekSahibi, null, null, $"{kaynak}:{kaynakId}");
        }

        // Gercek `MemoryCacheService`in set-if-not-exists semantigini TASIYAN en kucuk sahte:
        // ayni anahtar ikinci kez true DONMEZ. Ornekleme pini bunun uzerine kuruluyor.
        private sealed class SayanCache : ICacheService
        {
            private readonly HashSet<string> _eklenen = new();
            public List<string> TryAddCagrilari { get; } = new();

            public Task<bool> TryAddAsync(string key, TimeSpan ttl)
            {
                TryAddCagrilari.Add(key);
                return Task.FromResult(_eklenen.Add(key));
            }

            public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null) => factory();
            public Task<bool> ExistsAsync(string key) => Task.FromResult(_eklenen.Contains(key));
            public Task SetAsync<T>(string key, T value, TimeSpan ttl) => Task.CompletedTask;
            public Task<T?> GetAsync<T>(string key) => Task.FromResult(default(T));
            public void Remove(string key) { }
            public void RemoveByPrefix(string prefix) { }
        }

        private static readonly RateLimitPolitikasi VarsayilanPolitika = new(authLimiti: 10, odemeLimiti: 10, genelLimit: 100);

        private static async Task<(string kapsam, int limit)> KapsamOlcAsync(string yol, RateLimitPolitikasi? politika = null)
        {
            var limiter = new YakalayanLimiter();
            var mw = new RedisRateLimitMiddleware(_ => Task.CompletedTask, limiter, politika ?? VarsayilanPolitika);
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = yol;
            ctx.Response.Body = new MemoryStream();

            await mw.InvokeAsync(ctx, new YakalayanOlay(), new SayanCache());

            var anahtar = limiter.SonAnahtar ?? "";
            var kapsam = anahtar.Contains(':') ? anahtar.Substring(0, anahtar.IndexOf(':')) : anahtar;
            return (kapsam, limiter.SonLimit);
        }

        [Fact]
        public async Task BuyukHarfli_AuthYolu_AUTH_KOVASINA_Duser_GlobalKovaya_KACAMAZ()
        {
            var (kapsam, limit) = await KapsamOlcAsync("/API/AUTH/LOGIN");

            kapsam.Should().Be("auth",
                "buyuk harfli yazim kaba kuvvet savunmasindan KACAMAZ - eskiden 'global' kovaya dusuyordu");
            limit.Should().Be(10, "auth kovasi ARTIK yerlesik yolla AYNI degeri kullanir (varsayilan 10)");
        }

        // VAKUM KIRICI: kucuk harfli yol da ayni kovaya dusmeli - yani duzeltme, calisan
        // davranisi bozmadan yalnizca eksik olani kapatti.
        [Fact]
        public async Task KucukHarfli_AuthYolu_AYNI_KOVAYA_Duser()
        {
            var (kapsam, limit) = await KapsamOlcAsync("/api/auth/login");
            kapsam.Should().Be("auth");
            limit.Should().Be(10);
        }

        // CIFT-ANLAM KIRICI: "her yol auth kovasina dusuyor" olsaydi ilk iki test de gecerdi.
        [Theory]
        [InlineData("/API/PAYMENT/INITIALIZE", "payment", 10)]
        [InlineData("/api/payment/initialize", "payment", 10)]
        [InlineData("/API/PRODUCT/FILTER", "global", 100)]
        [InlineData("/api/product/filter", "global", 100)]
        public async Task DigerYollar_DOGRU_KOVAYA_Duser_ve_BuyukHarf_FARK_ETMEZ(
            string yol, string beklenenKapsam, int beklenenLimit)
        {
            var (kapsam, limit) = await KapsamOlcAsync(yol);
            kapsam.Should().Be(beklenenKapsam);
            limit.Should().Be(beklenenLimit);
        }
        // === DALGA D / D5 - AUTH LIMITI IKI YOLDA DA AYNI ve YAPILANDIRMADAN GELIR ========
        //
        // BILINCLI KIRILAN PIN: SUPHELI_AUTH_LIMITI_REDIS_YOLUNDA_5_YERLESIK_YOLDA_10_PINLENIR.
        // O pin OLCULEN AYRISMAYI sabitliyordu (Redis yolu 5 SABIT, yerlesik yol 10 config'ten)
        // ve kullanici karariyla ayrisma DUZELTILDI - pin artik YANLIS bir sozlesmeyi savunur
        // hale gelirdi. Yerine ayrismanin KAPANDIGINI olcen bu pin geldi.
        [Fact]
        public async Task AUTH_LIMITI_YAPILANDIRMADAN_GELIR_KAYNAKTA_SABIT_DEGIL()
        {
            // Ayirt edici bir deger: 5 de 10 de DEGIL - boylece "eski sabit geri geldi" ya da
            // "varsayilan kullanildi" durumlarinin IKISI DE yakalanir.
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:AuthPermitLimit"] = "37",
                ["RateLimit:PaymentPermitLimit"] = "41",
                ["RateLimit:GlobalPermitLimit"] = "43"
            }).Build();
            var politika = RateLimitPolitikasi.Olustur(cfg);

            (await KapsamOlcAsync("/api/auth/login", politika)).limit.Should().Be(37,
                "auth limiti YAPILANDIRMADAN gelmeli - kaynakta sabit 5 DEGIL");
            (await KapsamOlcAsync("/api/payment/initialize", politika)).limit.Should().Be(41);
            (await KapsamOlcAsync("/api/product/filter", politika)).limit.Should().Be(43);

            // CIFT-ANLAM KIRICI: yapilandirma VERILMEDIGINDE yerlesik yolun varsayilanlari
            // gecerli olmali (10/10/100) - "her zaman config" degil, "config VARSA config".
            var bos = RateLimitPolitikasi.Olustur(new ConfigurationBuilder().Build());
            bos.AuthLimiti.Should().Be(10, "varsayilan YERLESIK yolun degeridir (5 DEGIL)");
            bos.OdemeLimiti.Should().Be(10);
            bos.GenelLimit.Should().Be(100);
        }

        // ══ GF-5 / K2 (D6) - 429 RED DALININ IZI ═══════════════════════════════════════════
        // Her zaman REDDEDEN limiter: red dali ancak boyle kosturulabilir.
        private sealed class ReddedenLimiter : IDistributedRateLimiter
        {
            public Task<RateLimitResult> CheckAsync(string key, int limit, int windowSeconds) =>
                Task.FromResult(new RateLimitResult { Allowed = false, Remaining = 0, RetryAfterSeconds = 60 });
        }

        private static async Task<(YakalayanOlay olay, SayanCache cache)> RedOlcAsync(
            string yol, int kacKez = 1, string ip = "203.0.113.7")
        {
            var olay = new YakalayanOlay();
            var cache = new SayanCache();
            var mw = new RedisRateLimitMiddleware(_ => Task.CompletedTask, new ReddedenLimiter(), VarsayilanPolitika);

            for (var i = 0; i < kacKez; i++)
            {
                var ctx = new DefaultHttpContext();
                ctx.Request.Path = yol;
                ctx.Response.Body = new MemoryStream();
                ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
                await mw.InvokeAsync(ctx, olay, cache);
                ctx.Response.StatusCode.Should().Be(429, "bu yardimci YALNIZ red dalini olcer");
            }
            return (olay, cache);
        }

        [Fact]
        public async Task RateLimit_429_GUVENLIK_OLAYI_YAZAR_ip_kova_ve_yol_ile()
        {
            var (olay, _) = await RedOlcAsync("/api/auth/login");

            // POZITIF OLAY KOSULU (vakum yasagi): satir GERCEKTEN olusmali.
            olay.Kayitlar.Should().HaveCount(1, "red dali TAM BOSLUKTU - AV-2 / S-C matrisi");

            var k = olay.Kayitlar[0];
            k.tip.Should().Be("RateLimitExceeded");
            k.siddet.Should().Be("Warning", "ON KOSUL kimliksiz-uzak ama tetik ADMIN degil - Critical DEGIL");
            // ALAN BAZLI (MK-6 dersi): "null degil" YETMEZ, GONDERILEN deger yazilmali.
            k.ip.Should().Be("203.0.113.7", "429 olayinin ATIF ekseni IP'dir");
            k.detay.Should().Contain("kova=auth", "hangi kovanin yandigi olaydan okunabilmeli");
            k.detay.Should().Contain("/api/auth/login");
            // ATIF SINIRI - BILINCLI VE PINLI: middleware UseAuthentication'DAN ONCE kosar,
            // dolayisiyla musteri kimligi ELDE DEGILDIR. Bu assert o KABUL EDILMIS SINIRI
            // pinler; bir gun middleware asagi tasinirsa BU PIN KIRILIR ve karar yeniden
            // gorusulur (sessizce degismesin diye).
            k.musteri.Should().BeNull("429 aninda HttpContext.User HENUZ BOS - atif yarisi acik");
        }

        [Fact]
        public async Task RateLimit_429_ORNEKLEME_ayni_ip_ve_kova_icin_TEK_satir_yazar()
        {
            var (olay, cache) = await RedOlcAsync("/api/auth/login", kacKez: 5);

            // AYIRT EDICI: bes RED kosuldu (yukaridaki yardimci her turda 429 dogruluyor)
            // ama defterde TEK satir olmali - aksi halde 429 seli, tam da DB'nin zorlandigi
            // anda yazma yuku uretirdi.
            cache.TryAddCagrilari.Should().HaveCount(5, "ornekleme kapisi HER redde YOKLANIR");
            olay.Kayitlar.Should().HaveCount(1, "60 sn penceresinde ayni (kova, IP) icin TEK satir");
            cache.TryAddCagrilari[0].Should().Be("sec-olay:429:auth:203.0.113.7",
                "ornekleme anahtari KOVA ve IP ile daralir - global tek anahtar DEGIL");
        }

        [Fact]
        public async Task RateLimit_429_ORNEKLEME_FARKLI_ip_AYRI_satir_yazar()
        {
            // VAKUM KIRICI: bir onceki pin "hep tek satir yazar" mutasyonuyla da yesil kalirdi.
            // Ornekleme anahtari IP TASIDIGI icin baska bir IP AYRI satir uretmeli.
            //
            // KRITIK: cache ve olay defteri IKI IP ARASINDA PAYLASILIR. Ayri ayri kurulsalardi
            // ("her cagri kendi cache'ini alsin") bu pin anahtarin IP tasiyip tasimadigini
            // OLCMEZDI - iki ayri cache'te her anahtar zaten ilk kez eklenir ve pin, IP'yi
            // anahtardan CIKARAN bir mutasyonda bile YESIL kalirdi. Ilk yazimda tam bu kusur
            // vardi ve MK-6 mutasyon turu oncesi yakalandi (kayit).
            var olay = new YakalayanOlay();
            var cache = new SayanCache();
            var mw = new RedisRateLimitMiddleware(_ => Task.CompletedTask, new ReddedenLimiter(), VarsayilanPolitika);

            foreach (var ip in new[] { "203.0.113.7", "198.51.100.4" })
            {
                var ctx = new DefaultHttpContext();
                ctx.Request.Path = "/api/auth/login";
                ctx.Response.Body = new MemoryStream();
                ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
                await mw.InvokeAsync(ctx, olay, cache);
                ctx.Response.StatusCode.Should().Be(429);
            }

            olay.Kayitlar.Should().HaveCount(2,
                "AYNI cache uzerinde iki FARKLI IP iki AYRI satir yazmali - anahtar IP tasimazsa "
                + "ikincisi ornekleme kapisina takilir ve bu assert TEK satir gorur");
            olay.Kayitlar.Select(k => k.ip).Distinct().Should().HaveCount(2);
            cache.TryAddCagrilari.Distinct().Should().HaveCount(2,
                "ornekleme anahtarlari da AYRISMALI");
        }

        [Fact]
        public async Task RateLimit_429_YOL_LOG_SATIRI_BOLEMEZ_CRLF_ayiklanir()
        {
            // `Request.Path.Value` COZULMUS yoldur: URL'deki %0D%0A GERCEK CRLF olarak iner.
            // `detail` Serilog sablonuna giriyor ve Serilog kontrol karakteri AYIKLAMAZ
            // (GF-3/A-3) - maskesiz birakilirsa saldirgan SAHTE bir "SECURITY ..." satiri
            // uydurabilirdi.
            var (olay, _) = await RedOlcAsync("/api/auth/login\r\nSECURITY sahte satir");

            olay.Kayitlar.Should().HaveCount(1);
            var detay = olay.Kayitlar[0].detay ?? "";
            detay.Should().NotContain("\r", "CR defterdeki satiri bolerdi");
            detay.Should().NotContain("\n", "LF defterdeki satiri bolerdi");
            // POZITIF: icerik KAYBOLMADI, yalnizca kontrol karakteri katlandi - teshis degeri korunur.
            detay.Should().Contain("SECURITY sahte satir",
                "ayiklama metni SILMEZ; yalnizca satir bolmeyi engeller");
        }
    }
}
