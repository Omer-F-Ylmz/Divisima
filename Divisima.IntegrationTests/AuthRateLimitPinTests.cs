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
    // D5-9 - AUTH RATE LIMIT: TEK KOVA (PARTITIONSIZ) - AMPIRIK PIN
    //
    // Program.cs:216
    //     options.AddFixedWindowLimiter("auth", opt => { opt.PermitLimit = 5; opt.Window = 1 dk; });
    // Global limiter PartitionedRateLimiter.Create ile IP ye gore bolunmus; "auth" policy si ise
    // BOLUNMEMIS - AddFixedWindowLimiter tek bir limiter ornegi uretir. AuthController sinif
    // duzeyinde [EnableRateLimiting("auth")] tasiyor, yani register/login/verify-email AYNI
    // kovadan yer.
    //
    // BU SINIFA AYRI HOST: alti istek kovayi tamamen bitiriyor. Diger test siniflari kendi
    // WebApplicationFactory ornekleriyle kostugu icin onlarin TestAuthHelper login leri
    // etkilenmez (D4 teki iki-host deseninin ayni gerekcesi).
    //
    // KISIT: test sunucusunda RemoteIpAddress null; X-Forwarded-For de guvenilmeyen kaynaktan
    // geldigi icin yok sayilir. Bu yuzden "FARKLI istemci kimligi ayni kovadan yer" iddiasi
    // dogrudan gosterilemedi. Gosterilen sey: kova ENDPOINT ler arasinda paylasiliyor
    // (login de register de ayni sayaci tuketiyor) ve limit 5 te doluyor.
    [Trait("Category", "Sql")]
    public class AuthRateLimitPinTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaAuthRateLimitTest";
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

        private sealed class RateLimitFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private RateLimitFactory? _factory;
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
                _factory = new RateLimitFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak rate limit testi ortami hazirlanamadi - ATLANMAMALI.", ex);
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
        public async Task AuthPolicy_BesIstekten_SonrakiIstek_429_AlirVeKovaEndpointlerArasiPaylasilir()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();

            // Ilk BES istek: kimlik bilgileri kasten yanlis - amac dogrulama degil, sayaci tuketmek.
            // Bunlarin 429 ALMAMASI gerekir (uygulamaya ulasip is mantigindan cevap almalilar).
            var kodlar = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                var resp = await client.PostAsJsonAsync("/api/auth/login",
                    new { email = $"yok-{Guid.NewGuid():N}@divisima.test", password = "YanlisParola123" });
                kodlar.Add((int)resp.StatusCode);
            }

            kodlar.Should().NotContain((int)HttpStatusCode.TooManyRequests,
                $"ilk bes istek limite takilmamali. Kodlar: {string.Join(",", kodlar)}");

            // ALTINCI istek AYNI uctan -> kova bitti.
            var altinci = await client.PostAsJsonAsync("/api/auth/login",
                new { email = $"yok-{Guid.NewGuid():N}@divisima.test", password = "YanlisParola123" });
            ((int)altinci.StatusCode).Should().Be(429,
                "PermitLimit=5 - altinci istek reddedilmeli");

            // FARKLI bir auth ucu da AYNI kovadan yiyor: policy endpoint basina degil,
            // AuthController un tamami icin tek sayac tutuyor.
            var farkliUc = await client.PostAsJsonAsync("/api/auth/register", new
            {
                name = "Kota Testi",
                email = $"kota-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password = "TestPass123",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });
            ((int)farkliUc.StatusCode).Should().Be(429,
                "register de ayni 'auth' kovasindan yiyor - kova endpoint basina DEGIL");

            // VAKUM KIRICI: kayit gercekten olusmadi (429 istegi is mantigina hic ulasmadi).
            await using var ctx = NewContext();
            (await ctx.Set<Divisima.Entity.Entities.Customer>().IgnoreQueryFilters()
                .CountAsync(c => c.name == "Kota Testi"))
                .Should().Be(0, "limite takilan kayit istegi musteri OLUSTURMAMALI");
        }
    }
}
