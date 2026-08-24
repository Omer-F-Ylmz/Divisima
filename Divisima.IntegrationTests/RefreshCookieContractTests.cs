using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.DataAccess.Concrete.Context;
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
    // SPRINT 8 MADDE 6 - REFRESH TOKEN httpOnly COOKIE'YE TASINDI (UC DUZEYI SOZLESME)
    //
    // ONCEKI DURUM (E1'de olculdu): `AuthController.SetRefreshTokenCookie` TANIMLI ama HIC
    // CAGRILMIYORDU; login refresh token'i GOVDEDE donuyor, /api/auth/refresh [FromBody]
    // bekliyor, `Logout` ise HIC YAZILMAYAN bir cookie'yi okuyordu. Yani "access localStorage
    // + refresh httpOnly cookie" modeli YARIMDI: yazma yolu OLUYDU ve refresh token JS'in
    // erisebildigi yerde (localStorage) duruyordu - XSS'te calinabilir.
    //
    // BU DALGADA UC PARCA BIRLIKTE TASINDI (ayrilamazlar):
    //   1) refresh_token httpOnly cookie'ye yazilir ve YANIT GOVDESINDEN SILINIR,
    //   2) csrf_token cookie'si yazilir (double-submit; JS okuyabilsin diye HttpOnly DEGIL),
    //   3) istemci ayni degeri X-CSRF-Token basliginda geri gonderir.
    // Neden birlikte: `AntiforgeryMiddleware` yalniz "refresh_token cookie'si VAR + Bearer YOK"
    // durumunda devreye giriyor. Cookie hic yazilmadigi icin middleware BUGUNE KADAR HIC
    // CALISMADI. Cookie yazilmaya baslandigi anda /api/auth/refresh (Bearer yok, cookie var)
    // CSRF denetimine takilir; csrf_token'i yazan bir yer OLMASAYDI uc kalici 403 verirdi.
    //
    // KIRILAN PIN YOK - DURUST KAYIT: HTTP duzeyinde /api/auth/refresh pini HIC YOKTU (tum
    // test dosyalari tarandi). Var olan iki refresh pini (`Refresh_YeniCiftUretir_ESKI_
    // RefreshToken_REDDEDILIR`, `PasifHesabin_RefreshToken_i_Reddedilir`) `IAuthService`'i
    // DOGRUDAN cagiriyor; servis imzasi degismedigi icin ikisi de SAG KALDI.
    [Trait("Category", "Sql")]
    public class RefreshCookieContractTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaRefreshCookieTest";
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

        private sealed class CookieFactory : WebApplicationFactory<Program>
        {
            private readonly string? _environment;
            public CookieFactory(string? environment = null) { _environment = environment; }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                if (_environment != null)
                {
                    builder.UseEnvironment(_environment);
                    // Uretim ortaminda Program.cs FAIL-FAST blogu kritik ayarlari zorunlu kilar.
                    // Testin amaci o blogu olcmek DEGIL, cookie'nin Secure bayragini olcmek;
                    // bu yuzden gecerli degerler VERILIR (host acilabilsin).
                    builder.UseSetting("MailSettings:Host", "smtp.test.local");
                    builder.UseSetting("Encryption:Key", Convert.ToBase64String(new byte[32]));
                    // appsettings.Development.json Production'da YUKLENMEZ; imzalama anahtari
                    // aksi halde bos kalir ve fail-fast host'u acmaz. Deger 32 bayttan uzun ve
                    // yer-tutucu listesindeki hicbir dizgeyi icermiyor (CHANGE_ME, TODO, ...).
                    builder.UseSetting("TokenOptions:SecurityKey",
                        "divisima-uretim-ortami-pini-icin-uretilmis-uzun-imzalama-anahtari-0123456789");
                    // SPRINT 8 MADDE 7: uretimde Iyzico:CallbackUrl de fail-fast listesinde.
                    // Bu testin konusu o degil, host'un ACILMASI; gecerli bir deger veriliyor.
                    builder.UseSetting("Iyzico:CallbackUrl", "https://api.divisima.test/api/payment/callback");
                }
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private CookieFactory? _factory;
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
                _factory = new CookieFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak refresh cookie testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // Cookie'leri ISTEMCI OTOMATIK yonetmesin: Set-Cookie basliklarini ve "cookie YOKSA ne
        // olur" durumunu ancak ham istemciyle olcebiliriz.
        private HttpClient HamIstemci(CookieFactory f) =>
            f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        private static string? CerezSatiri(HttpResponseMessage r, string ad) =>
            r.Headers.TryGetValues("Set-Cookie", out var v)
                ? v.FirstOrDefault(c => c.StartsWith(ad + "=", StringComparison.Ordinal))
                : null;

        private static string? CerezDegeri(string? setCookieSatiri)
        {
            if (string.IsNullOrEmpty(setCookieSatiri)) return null;
            var esit = setCookieSatiri.IndexOf('=');
            var noktali = setCookieSatiri.IndexOf(';');
            if (esit < 0) return null;
            return noktali > esit
                ? setCookieSatiri.Substring(esit + 1, noktali - esit - 1)
                : setCookieSatiri.Substring(esit + 1);
        }

        // Gercek kayit/dogrulama zincirinden gecmis bir musteri uretir ve GIRIS YANITINI ham
        // haliyle dondurur (TestAuthHelper token'i dondurur, Set-Cookie basliklarini degil).
        private async Task<(HttpResponseMessage Login, int CustomerId)> GirisYapAsync(CookieFactory f)
        {
            var user = await TestAuthHelper.CreateCustomerClientAsync(f);
            var anon = HamIstemci(f);
            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            return (login, user.CustomerId);
        }

        // ── 1) LOGIN: COOKIE YAZILIR, GOVDEDE REFRESH TOKEN KALMAZ ────────────────────
        //
        // Modelin butun amaci token'in JS'in erisebildigi yerde durmamasi. Govdede birakilsaydi
        // istemci onu yine localStorage'a koyabilir ve httpOnly HICBIR SEY kazandirmazdi -
        // bu yuzden "cookie var" demek YETMEZ, "govdede YOK" da olculur.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Login_RefreshTokenI_HTTPONLY_COOKIEYE_YAZAR_GOVDEDE_BIRAKMAZ()
        {
            if (Skipped()) return;

            var (login, customerId) = await GirisYapAsync(_factory!);
            login.StatusCode.Should().Be(HttpStatusCode.OK);

            var refreshCookie = CerezSatiri(login, "refresh_token");
            refreshCookie.Should().NotBeNull("login refresh token'i cookie olarak YAZMALI");
            refreshCookie!.ToLowerInvariant().Should()
                .Contain("httponly", "cookie JS'e KAPALI olmali (XSS'te calinamasin)")
                .And.Contain("path=/api/auth", "kapsam dar tutulmali - her istekte tasinmasin")
                .And.Contain("samesite=strict");

            var csrfCookie = CerezSatiri(login, "csrf_token");
            csrfCookie.Should().NotBeNull("double-submit'in ikinci yarisi da AYNI anda yazilmali");
            csrfCookie!.ToLowerInvariant().Should().NotContain("httponly",
                "csrf token'i JS OKUYABILMELI - istemci onu X-CSRF-Token basliginda geri gonderiyor");
            // KAPSAM FARKI TARAYICIDA OLCULDU: csrf cookie'si "/api/auth" ile yazildiginda
            // storefront sayfasindaki document.cookie onu HIC GORMUYOR (yol eslesmiyor) - istemci
            // basligi dolduramaz ve yenileme kalici 403 olur. Bu yuzden kok yola yazilir.
            csrfCookie.ToLowerInvariant().Should().Contain("path=/",
                "csrf cookie'si KOK yolda olmali; dar yolda yazilirsa storefront JS'i okuyamaz");
            csrfCookie.ToLowerInvariant().Should().NotContain("path=/api/auth",
                "bu, yukaridaki hatanin ta kendisi - olculdu ve duzeltildi");
            CerezDegeri(csrfCookie).Should().NotBeNullOrWhiteSpace("deger bos olmamali - POZITIF olay kosulu");

            using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");
            data.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace(
                "access token govdede DONMEYE DEVAM ETMELI - yalniz refresh token tasinacakti");
            var govdedekiRefresh = data.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            govdedekiRefresh.Should().BeNullOrEmpty(
                "refresh token GOVDEDEN SILINMELI; govdede kalirsa istemci yine localStorage'a koyabilir " +
                "ve httpOnly hicbir sey kazandirmaz");

            // Oturum GERCEKTEN acildi mi (vakum kirici): cookie'deki deger DB'de AKTIF bir
            // oturumun refresh token'i olmali. NOT: TestAuthHelper zaten bir kez giris yapiyor,
            // bu test ikinci kez yapiyor - musterinin BIRDEN COK aktif oturumu olabilir, o yuzden
            // "tek satir" degil "bu deger aktif oturumlar arasinda VAR MI" sorulur.
            var cerezDegeri = CerezDegeri(refreshCookie);
            await using var ctx = NewContext();
            (await ctx.Set<UserSession>().AsNoTracking()
                .AnyAsync(s => s.customer_id == customerId && s.is_active && s.refresh_token == cerezDegeri))
                .Should().BeTrue("cookie'ye yazilan deger DB'deki AKTIF oturumun ta kendisi olmali");
        }

        // ── 2) COOKIE YOKSA 401 - GOVDE YOLU KAPALI ───────────────────────────────────
        //
        // CIFT-ANLAM KIRICI: yalniz "cookie yoksa 401" demiyoruz. Eski sozlesmeyle (govdede
        // refun token) GECERLI bir token gonderiliyor ve yine 401 alindigi gosteriliyor -
        // yani govde yolu gercekten KAPALI, sadece "unutulmus" degil.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Refresh_COOKIESIZ_401_Doner_ESKI_GOVDE_YOLU_KAPALI()
        {
            if (Skipped()) return;

            var (login, customerId) = await GirisYapAsync(_factory!);
            login.StatusCode.Should().Be(HttpStatusCode.OK);

            // GECERLI token'i cookie'den aliyoruz: bu, AZ ONCEKI girisin oturumu. DB'den
            // "tek aktif oturum" diye cekmek yanlis olurdu - TestAuthHelper de bir oturum aciyor.
            var gecerliToken = CerezDegeri(CerezSatiri(login, "refresh_token"));
            gecerliToken.Should().NotBeNullOrWhiteSpace("token gercekten uretilmis olmali - vakum kirici");
            await using (var on = NewContext())
                (await on.Set<UserSession>().AsNoTracking()
                    .AnyAsync(s => s.customer_id == customerId && s.is_active && s.refresh_token == gecerliToken))
                    .Should().BeTrue("token DB'de AKTIF bir oturuma karsilik gelmeli");

            var anon = HamIstemci(_factory!);

            var cerezsiz = await anon.PostAsync("/api/auth/refresh", null);
            cerezsiz.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "cookie yoksa oturum yenilenemez");

            var govdeyle = await anon.PostAsJsonAsync("/api/auth/refresh", new { refresh_token = gecerliToken });
            govdeyle.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "GECERLI bir token bile GOVDEDE kabul edilmemeli - yoksa istemci cookie modelini " +
                "sessizce bypass edip token'i yine JS'in erisebildigi yerde tutabilir");

            await using var son = NewContext();
            (await son.Set<UserSession>().AsNoTracking()
                .AnyAsync(s => s.customer_id == customerId && s.is_active && s.refresh_token == gecerliToken))
                .Should().BeTrue("reddedilen istekler oturumu DONDURMEMELI (rotasyon tetiklenmemeli)");
        }

        // ── 3) CSRF BASLIGI YOKSA 403 ─────────────────────────────────────────────────
        //
        // Cookie tasiyan + Bearer tasimayan bir istek AntiforgeryMiddleware'e takilir. Bu pin
        // hem korumanin CANLI oldugunu hem de dogru basligi (X-CSRF-Token) bekledigini sabitler.
        // Ad uyusmazligi bu dalgada olculdu: istemci "XSRF-TOKEN" cookie'sini okuyup
        // "X-XSRF-TOKEN" gonderiyordu, middleware "csrf_token"/"X-CSRF-Token" bekliyordu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Refresh_CSRF_BASLIGI_YOKSA_403_BASLIK_DOGRUYSA_GECER()
        {
            if (Skipped()) return;

            var (login, _) = await GirisYapAsync(_factory!);
            var refresh = CerezDegeri(CerezSatiri(login, "refresh_token"));
            var csrf = CerezDegeri(CerezSatiri(login, "csrf_token"));
            refresh.Should().NotBeNullOrWhiteSpace();
            csrf.Should().NotBeNullOrWhiteSpace();

            var anon = HamIstemci(_factory!);

            var basliksiz = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            basliksiz.Headers.Add("Cookie", $"refresh_token={refresh}; csrf_token={csrf}");
            var r1 = await anon.SendAsync(basliksiz);
            r1.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "cookie tasiyan durum-degistiren istek CSRF basligi olmadan gecmemeli");
            (await r1.Content.ReadAsStringAsync()).Should().Contain("CSRF",
                "cift-anlam kirici: 403 baska bir yetki sebebinden degil, CSRF'ten gelmeli");

            var yanlisBaslik = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            yanlisBaslik.Headers.Add("Cookie", $"refresh_token={refresh}; csrf_token={csrf}");
            yanlisBaslik.Headers.Add("X-CSRF-Token", "baska-bir-deger");
            (await anon.SendAsync(yanlisBaslik)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "baslik ile cookie ESLESMELI - herhangi bir deger yetmez");

            var dogru = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            dogru.Headers.Add("Cookie", $"refresh_token={refresh}; csrf_token={csrf}");
            dogru.Headers.Add("X-CSRF-Token", csrf);
            (await anon.SendAsync(dogru)).StatusCode.Should().Be(HttpStatusCode.OK,
                "POZITIF OLAY: dogru baslikla yenileme GERCEKTEN calismali - aksi halde bu pin " +
                "yalniz '403 doner' diyen bir vakum olurdu");
        }

        // ── 4) Secure HER ORTAMDA ACIK - ORTAM GUARD'I YOK ───────────────────────────
        //
        // Ilk tasarimda "Development'ta Secure kapali" guard'i vardi; TARAYICIDA OLCULDU ki
        // gerek yok - localhost guvenilir origin sayildigi icin Secure cookie duz HTTP
        // uzerinde de saklaniyor ve geri gonderiliyor (giristen sonra sessiz yenileme calisti).
        // Guard kaldirildi. Bu pin her iki ortami da olcer: bir refactor Secure'u ortama bagli
        // hale getirirse (ya da tumden kaldirirsa) BURADA kirilir.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Cerez_Secure_HER_ORTAMDA_ISARETLI_OrtamGuardi_YOK()
        {
            if (Skipped()) return;

            var (devLogin, _) = await GirisYapAsync(_factory!);
            var devCookie = CerezSatiri(devLogin, "refresh_token");
            devCookie.Should().NotBeNull();
            devCookie!.ToLowerInvariant().Should().Contain("secure",
                "Secure ortama bagli OLMAMALI - development'ta da acik kalmali (tarayicida olculdu: calisiyor)");

            // MUSTERI DEV FABRIKASINDA URETILIR, GIRIS URETIM FABRIKASINDA YAPILIR.
            // Sebep OLCULDU: uretim fail-fast'i MailSettings:Host'u ZORUNLU kiliyor; dolu birakinca
            // SmtpMailService gercekten baglanmaya calisiyor ve /api/auth/register 500 donuyor.
            // Iki fabrika AYNI veritabanina bakiyor, dolayisiyla dev'de acilan hesapla uretim
            // host'unda giris yapmak gecerli bir akis - ve bu testin olctugu sey zaten GIRIS
            // yanitindaki cookie bayragi, kayit degil.
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            await using var prodFactory = new CookieFactory("Production");
            var prodAnon = HamIstemci(prodFactory);
            var prodLogin = await prodAnon.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            prodLogin.StatusCode.Should().Be(HttpStatusCode.OK, "uretim ortaminda da giris calismali");
            var prodCookie = CerezSatiri(prodLogin, "refresh_token");
            prodCookie.Should().NotBeNull();
            prodCookie!.ToLowerInvariant().Should().Contain("secure",
                "URETIMDE Secure ZORUNLU - cookie duz HTTP uzerinden gonderilmemeli");
            prodCookie.ToLowerInvariant().Should().Contain("httponly");
        }
    }
}
