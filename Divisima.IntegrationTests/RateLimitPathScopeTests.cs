using Divisima.API.Middlewares;
using Divisima.Core.Security.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

        private static async Task<(string kapsam, int limit)> KapsamOlcAsync(string yol)
        {
            var limiter = new YakalayanLimiter();
            var mw = new RedisRateLimitMiddleware(_ => Task.CompletedTask, limiter);
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
            limit.Should().Be(5, "auth kovasinin limiti 5/dk");
        }

        // VAKUM KIRICI: kucuk harfli yol da ayni kovaya dusmeli - yani duzeltme, calisan
        // davranisi bozmadan yalnizca eksik olani kapatti.
        [Fact]
        public async Task KucukHarfli_AuthYolu_AYNI_KOVAYA_Duser()
        {
            var (kapsam, limit) = await KapsamOlcAsync("/api/auth/login");
            kapsam.Should().Be("auth");
            limit.Should().Be(5);
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
    }
}
