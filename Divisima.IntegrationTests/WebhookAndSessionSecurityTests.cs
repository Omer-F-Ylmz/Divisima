using System.Net;
using System.Net.Http.Json;
using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Auth;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 5 - WEBHOOK IP KAPISI + OTURUM (REFRESH) GUVENLIGI
    //
    // 1) WebhookIpAllowlistMiddleware: "Webhook:AllowedIps" BOSSA kontrol tamamen ATLANIR
    //    (dev kolayligi). Dolu ise yalniz listedeki IP gecer. Bu iki dal da pinleniyor -
    //    ozellikle BOS dal, cunku launch'ta doldurulmazsa webhook yalniz imzayla korunur.
    // 2) Refresh token ROTASYONU: yenileme sonrasi ESKI token gecersiz olmali (replay engeli),
    //    ve pasiflestirilmis hesabin refresh'i reddedilmeli.
    [Trait("Category", "Sql")]
    public class WebhookAndSessionSecurityTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaWebhookSessionTest";
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

        // Allowlist'i host duzeyinde ayarlanabilen fabrika - iki dal da ayni sinifta surulur.
        private sealed class WebhookFactory : WebApplicationFactory<Program>
        {
            private readonly string? _allowedIp;
            public WebhookFactory(string? allowedIp = null) => _allowedIp = allowedIp;

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                if (_allowedIp != null)
                    builder.UseSetting("Webhook:AllowedIps:0", _allowedIp);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private WebhookFactory? _host;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _host = new WebhookFactory();
                _ = _host.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak webhook/oturum testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_host != null) await _host.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static object SahteWebhook() => new { token = "yok-boyle-bir-token", signature = "00" };

        // ── WEBHOOK: BOS LISTE = KONTROL ATLANIR (pinlenir) ──────────────────────────────
        // Istek middleware'e TAKILMAZ; uca ulasir ve orada imza/kayit dogrulamasina duser.
        // Yani 403 DEGIL, is mantigindan gelen bir yanit alir.
        [Fact]
        public async Task WebhookAllowlist_BOS_Liste_KontrolATLANIR_PINLENIR()
        {
            if (Skipped()) return;
            var client = _host!.CreateClient();

            var resp = await client.PostAsJsonAsync("/api/payment/webhook", SahteWebhook());

            resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
                "allowlist bos oldugunda IP kontrolu ATLANIR - istek uca ulasmali");
            // Uca ulastiginin kaniti: imza dogrulamasindan gelen is mantigi yaniti.
            ((int)resp.StatusCode).Should().BeInRange(400, 404,
                $"uc kendi dogrulamasini yapmali (imza gecersiz): {(int)resp.StatusCode}");
        }

        // ── WEBHOOK: DOLU LISTE + YABANCI IP = 403 ───────────────────────────────────────
        [Fact]
        public async Task WebhookAllowlist_DOLU_Liste_YabanciIP_Reddedilir()
        {
            if (Skipped()) return;
            // Test sunucusunda RemoteIpAddress null -> "" olarak degerlendirilir ve listede
            // olmadigi icin YABANCI sayilir. Gercek dagitimda bu, listede olmayan her IP demektir.
            await using var factory = new WebhookFactory(allowedIp: "203.0.113.10");
            var client = factory.CreateClient();

            var resp = await client.PostAsJsonAsync("/api/payment/webhook", SahteWebhook());

            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "allowlist doluyken listede olmayan IP reddedilmeli");
            (await resp.Content.ReadAsStringAsync()).Should().Contain("Erişim reddedildi",
                "middleware'in kendi mesaji donmeli (uca hic ulasmadan)");
        }

        // CIFT-ANLAM KIRICI: allowlist YALNIZ webhook yolunu denetler - diger uclar etkilenmez.
        [Fact]
        public async Task WebhookAllowlist_DOLUYKEN_DigerUclar_Etkilenmez()
        {
            if (Skipped()) return;
            await using var factory = new WebhookFactory(allowedIp: "203.0.113.10");
            var client = factory.CreateClient();

            (await client.GetAsync("/health/live")).StatusCode
                .Should().Be(HttpStatusCode.OK, "saglik kontrolu allowlist'ten etkilenmemeli");
            (await client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "kimliksiz istek 401 - 403 DEGIL");
        }

        // ── REFRESH ROTASYONU ────────────────────────────────────────────────────────────
        [Fact]
        public async Task Refresh_YeniCiftUretir_ESKI_RefreshToken_REDDEDILIR()
        {
            if (Skipped()) return;
            var a = await TestAuthHelper.CreateCustomerClientAsync(_host!);

            // GF-1b / K3 UYARLAMASI: kolon artik SHA-256 OZET tutuyor, DB'den okunan deger
            // jeton olarak KULLANILAMAZ. Test BILINEN bir duz jeton belirleyip ozetini
            // yaziyor - iki tarafi da kendi kontrol ediyor. NIYET DEGISMEDI: rotasyonun eski
            // jetonu kapattigi ve yeni cift urettigi olculuyor.
            var eskiRefresh = "gf1b-rot-" + Guid.NewGuid().ToString("N");
            await using (var ctx = NewContext())
            {
                var oturum = await ctx.Set<UserSession>()
                    .SingleAsync(s => s.customer_id == a.CustomerId && s.is_active);
                oturum.refresh_token = Divisima.Core.Security.Tokens.JetonOzeti.Hesapla(eskiRefresh);
                await ctx.SaveChangesAsync();
            }

            // POZITIF OLAY: gecerli refresh yeni bir cift uretiyor.
            var ilk = await WithScopeAsync(sp => sp.GetRequiredService<IAuthService>()
                .RefreshToken(new RefreshTokenRequestDto { refresh_token = eskiRefresh }));
            ilk.Item2.Success.Should().BeTrue($"gecerli refresh calismali: {ilk.Item2.Message}");

            await using (var ctx = NewContext())
            {
                (await ctx.Set<UserSession>().AsNoTracking()
                    .SingleAsync(s => s.refresh_token == Divisima.Core.Security.Tokens.JetonOzeti.Hesapla(eskiRefresh))).is_active
                    .Should().BeFalse("ESKI oturum kapatilmali (rotasyon)");
                (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == a.CustomerId && s.is_active))
                    .Should().Be(1, "yerine YENI bir aktif oturum gelmeli");
            }

            // ASIL SINAV: ayni eski token TEKRAR kullanilamaz (replay engeli).
            var tekrar = await WithScopeAsync(sp => sp.GetRequiredService<IAuthService>()
                .RefreshToken(new RefreshTokenRequestDto { refresh_token = eskiRefresh }));
            tekrar.Item1.Should().Be(HttpStatusCode.Unauthorized, "kullanilmis refresh token REDDEDILMELI");
            tekrar.Item2.Success.Should().BeFalse();
        }

        [Fact]
        public async Task PasifHesabin_RefreshToken_i_Reddedilir()
        {
            if (Skipped()) return;
            var a = await TestAuthHelper.CreateCustomerClientAsync(_host!);

            // GF-1b / K3 UYARLAMASI (gerekce ustteki pinle AYNI): DB OZET tutuyor, test
            // BILINEN duz jetonun ozetini yaziyor. NIYET DEGISMEDI - olculen sey HESAP DURUMU.
            var refresh = "gf1b-pasif-" + Guid.NewGuid().ToString("N");
            await using (var ctx = NewContext())
            {
                var oturum = await ctx.Set<UserSession>()
                    .SingleAsync(s => s.customer_id == a.CustomerId && s.is_active);
                oturum.refresh_token = Divisima.Core.Security.Tokens.JetonOzeti.Hesapla(refresh);
                await ctx.SaveChangesAsync();
            }

            // Hesabi pasiflestir (oturum satiri AKTIF kaliyor - kontrol edilen sey hesap durumu).
            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == a.CustomerId);
                c.is_active = false;
                await ctx.SaveChangesAsync();
            }

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IAuthService>()
                .RefreshToken(new RefreshTokenRequestDto { refresh_token = refresh }));

            r.Item1.Should().Be(HttpStatusCode.Unauthorized, "pasif hesabin refresh'i reddedilmeli");
            r.Item2.Success.Should().BeFalse();

            await using (var ctx = NewContext())
                (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == a.CustomerId && s.is_active))
                    .Should().Be(1, "reddedilen refresh YENI oturum ACMAMALI (eski aktif kalir)");
        }

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _host!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }
    }
}
