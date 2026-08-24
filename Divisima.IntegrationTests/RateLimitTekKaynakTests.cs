using System.Net;
using System.Net.Http.Json;
using Divisima.DataAccess.Concrete.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // === DALGA D / D5 - IKI RATE LIMIT YOLU AYNI ANDA: CIFTE SAYIM VAR MI? ==============
    //
    // KULLANICININ SORDUGU OLCUM. Once-durum: `app.UseRateLimiter()` YALNIZCA
    // `Redis:Enabled=false` dalinda cagriliyordu; uretimde (bayrak true) yalnizca
    // RedisRateLimitMiddleware kosuyordu. Yani:
    //   * [EnableRateLimiting("auth"/"payment")] oznitelikleri URETIMDE ETKISIZDI,
    //   * `RateLimit:*` ayarlari URETIMDE HIC OKUNMUYORDU,
    //   * auth kovasi uretimde 10 degil KAYNAKTA SABIT 5 idi.
    //
    // DUZELTME: kova tanimlari TEK KAYNAKTAN (RateLimitPolitikasi) ve IKI YOL DA HER ZAMAN
    // devrede. Ortaya cikan soru: iki sayac ayni anda artinca limit YARIYA MI INIYOR?
    //
    // BU SINIF ONU AMPIRIK OLARAK YANITLAR. Beklenen cevap "HAYIR" ve gerekcesi su: iki
    // sayac da AYNI istekte, AYNI bolumleme anahtariyla (RemoteIpAddress) ve AYNI limitle
    // artiyor - yani KILITLI ADIMDA ilerliyorlar. Etkin limit ikisinin MINIMUMU'dur ve
    // ikisi esit oldugu icin beklenen degere esittir.
    //
    // Limit BILEREK 3 secildi: hem kucuk (hizli), hem de 5/10 gibi depoda gecen hicbir
    // varsayilana esit DEGIL - "varsayilan kullanildi" durumu da yakalanir.
    [Trait("Category", "Sql")]
    public class RateLimitTekKaynakTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaRateLimitTekKaynakTest";
        private const int Limit = 3;
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = DbName }.ConnectionString;
            }
        }

        private sealed class LimitFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("RateLimit:AuthPermitLimit", Limit.ToString());
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private LimitFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new LimitFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak rate limit testleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        [Fact]
        public async Task IKI_YOL_AYNI_ANDA_ETKIN_CIFTE_SAYIM_YOK_LIMIT_YAPILANDIRMADAN_GELIR()
        {
            if (Skipped()) return;
            var c = _factory!.CreateClient();

            // Yapilandirilan limit KADAR istek GECMELI. Cifte sayim olsaydi ikinci sayac
            // yuzunden bunlarin bir kismi 429 alirdi (or. limit 3 iken 2. istek).
            for (var i = 1; i <= Limit; i++)
            {
                var y = await c.PostAsJsonAsync("/api/auth/forgot-password", new { email = $"limit{i}@example.com" });
                ((int)y.StatusCode).Should().NotBe(429,
                    $"{i}. istek limitin ({Limit}) ICINDE - cifte sayim OLSAYDI burada 429 gorurduk");
            }

            // Limit+1. istek 429 ALMALI - yani mekanizma GERCEKTEN calisiyor (vakum kirici).
            var asan = await c.PostAsJsonAsync("/api/auth/forgot-password", new { email = "limit-asan@example.com" });
            ((int)asan.StatusCode).Should().Be(429,
                $"{Limit + 1}. istek reddedilmeli - limit YAPILANDIRMADAN ({Limit}) gelmeli");

            // CIFT-ANLAM KIRICI: 429 "her sey kapandi" demek DEGIL - BASKA bir kova hala
            // calisiyor olmali. auth kovasi tukendi ama genel kova (varsayilan 100) acik.
            var baskaKova = await c.GetAsync("/health");
            ((int)baskaKova.StatusCode).Should().NotBe(429,
                "auth kovasinin tukenmesi DIGER kovalari kapatmamali");
        }

        [Fact]
        public async Task LIMIT_YAPILANDIRMASI_OKUNMASAYDI_BU_TEST_GECMEZDI()
        {
            if (Skipped()) return;
            var c = _factory!.CreateClient();

            // KARSIT KONTROL: depodaki varsayilanlar 5 ve 10. Test host'u 3 veriyor.
            // Ayarlar OKUNMASAYDI 4. istek GECERDI (5 ya da 10 limitin altinda kalirdi).
            for (var i = 1; i <= Limit; i++)
                await c.PostAsJsonAsync("/api/auth/forgot-password", new { email = $"karsit{i}@example.com" });

            var dorduncu = await c.PostAsJsonAsync("/api/auth/forgot-password", new { email = "karsit4@example.com" });
            ((int)dorduncu.StatusCode).Should().Be(429,
                "ayar OKUNMASAYDI limit 5 ya da 10 olurdu ve bu istek GECERDI");
        }
    }
}
