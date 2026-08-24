using Divisima.API.Middlewares;
using Divisima.Core.Security.RateLimiting;
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

        private static readonly RateLimitPolitikasi VarsayilanPolitika = new(authLimiti: 10, odemeLimiti: 10, genelLimit: 100);

        private static async Task<(string kapsam, int limit)> KapsamOlcAsync(string yol, RateLimitPolitikasi? politika = null)
        {
            var limiter = new YakalayanLimiter();
            var mw = new RedisRateLimitMiddleware(_ => Task.CompletedTask, limiter, politika ?? VarsayilanPolitika);
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = yol;
            ctx.Response.Body = new MemoryStream();

            await mw.InvokeAsync(ctx);

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
    }
}
