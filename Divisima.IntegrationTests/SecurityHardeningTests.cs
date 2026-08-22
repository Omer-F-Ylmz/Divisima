using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GUVENLIK-FIX PINLERI (G1..G9) ═════════════════════════════════════════════════════════
    //
    // (C) GUVENLIK DALGASI yalniz OLCUMDU; bu sinif o dalgada olculen dokuz bulgunun
    // duzeltmelerini sabitler. Her pin, olculen ONCE-DURUMU yorumunda tasir - bir gun biri
    // duzeltmeyi geri alirsa testin adi ve mesaji "neyin geri geldigini" soyler.
    //
    // KAPSAM DISI (bilerek): G4 (satici refresh token'inin govdede donmesi). Bugun ERISILEMEZ -
    // Seller:RegistrationEnabled=false ve sellers tablosu bos (olculdu). Satici modulu acilmadan
    // once ZORUNLU on kosul olarak deftere yazildi; simdi kod degistirilmedi, pin de yazilmadi
    // (var olmayan bir yuzeyi pinlemek yanlis bir guvence olurdu).
    [Trait("Category", "Sql")]
    public class SecurityHardeningTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaSecurityHardeningTest";
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

        // ── Hata seviyeli log YAKALAYICI (G3'un "log kirletmiyor" yarisi icin) ──────────────
        //
        // G3'un olculen zarari IKI parcaliydi: (a) HTTP 500, (b) her istekte tam yigin izli bir
        // ERROR satiri. (b)'yi pinlemeden "duzeltildi" demek eksik olurdu - 500'u yakalayip
        // yutan bir cozum de (a)'yi gecerdi ama logu kirletmeye devam ederdi.
        private sealed class HataYakalayiciProvider : ILoggerProvider
        {
            public readonly List<string> Hatalar = new();
            public ILogger CreateLogger(string categoryName) => new Yakalayici(this);
            public void Dispose() { }

            private sealed class Yakalayici : ILogger
            {
                private readonly HataYakalayiciProvider _sahip;
                public Yakalayici(HataYakalayiciProvider sahip) { _sahip = sahip; }
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                    if (logLevel < LogLevel.Error) return;
                    lock (_sahip.Hatalar)
                        _sahip.Hatalar.Add($"{formatter(state, exception)} || {exception?.GetType().Name} {exception?.Message}");
                }
            }
        }

        private sealed class GuvenlikFactory : WebApplicationFactory<Program>
        {
            private readonly bool _saticiKaydiAcik;
            public readonly HataYakalayiciProvider HataLogu = new();

            public GuvenlikFactory(bool saticiKaydiAcik = false) { _saticiKaydiAcik = saticiKaydiAcik; }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Seller:RegistrationEnabled", _saticiKaydiAcik ? "true" : "false");
                // Bu sinif cok sayida auth cagrisi yapiyor (kayit/giris/yenileme). Uretim
                // varsayilani 10/dk; olculen sey rate limit DEGIL, o yuzden kova genisletilir.
                builder.UseSetting("RateLimit:AuthPermitLimit", "1000");
                builder.ConfigureLogging(lb => lb.AddProvider(HataLogu));
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));

                    // G5 pini icin: "yetki ozniteligi UNUTULMUS" bir uc gercekten var olmali.
                    // Sonda controller'i TEST derlemesindedir; uygulama parcasi olarak YALNIZ
                    // bu fabrikaya eklenir, uretim yuzeyine HIC girmez.
                    services.AddControllers()
                        .PartManager.ApplicationParts.Add(
                            new AssemblyPart(typeof(G5FallbackSondaController).Assembly));
                });
            }
        }

        private GuvenlikFactory? _factory;
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
                _factory = new GuvenlikFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak guvenlik pinleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
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

        private HttpClient HamIstemci(GuvenlikFactory f) =>
            f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        private static string? CerezSatiri(HttpResponseMessage r, string ad) =>
            r.Headers.TryGetValues("Set-Cookie", out var v)
                ? v.FirstOrDefault(c => c.StartsWith(ad + "=", StringComparison.Ordinal))
                : null;

        private static string? CerezDegeri(string? satir)
        {
            if (string.IsNullOrEmpty(satir)) return null;
            var esit = satir.IndexOf('=');
            var noktali = satir.IndexOf(';');
            if (esit < 0) return null;
            return noktali > esit ? satir.Substring(esit + 1, noktali - esit - 1) : satir.Substring(esit + 1);
        }

        private static object KayitGovdesi(string email) => new
        {
            name = "Guvenlik Pini",
            email,
            phone = "5550000000",
            password = TestAuthHelper.TestPassword,
            accepted_terms = true,
            accepted_privacy = true,
            accepted_marketing = false
        };

        // ═══════════════════════════════════════════════════════════════════════════════════
        // G3 - ARAMA TERIMI UZUNLUGU
        // ═══════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: query=3998 -> 200, query=4000 -> HTTP 500. Sebep sunucu logunda:
        // SqlException 8152 "String or binary data would be truncated". 9 istek 6 ERROR satiri +
        // 66 SQL yigin satiri + 17.655 bayt log uretiyordu - KIMLIKSIZ bir log sisirme yuzeyi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task AramaTerimi_UZUNSA_400_Doner_500_ve_YIGIN_IZI_URETMEZ()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();
            lock (_factory.HataLogu.Hatalar) { _factory.HataLogu.Hatalar.Clear(); }

            foreach (var uzunluk in new[] { 201, 4000, 5000 })
            {
                var yanit = await client.GetAsync("/api/Search/products?query=" + new string('a', uzunluk));
                yanit.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                    $"{uzunluk} karakterlik terim GIRISTE reddedilmeli (500 DEGIL) - olculen once-durum: 4000 -> 500");
                (await yanit.Content.ReadAsStringAsync()).Should().Contain("200",
                    "cift-anlam kirici: 400 herhangi bir sebepten degil, UZUNLUK sinirindan gelmeli " +
                    "(mesaj sinir degerini tasir)");
            }

            // LOG KIRLILIGI: 500 yolu her istekte tam yigin izli bir ERROR satiri yaziyordu.
            // 400 yolu HICBIR hata seviyeli kayit uretmemeli.
            string[] hatalar;
            lock (_factory.HataLogu.Hatalar) { hatalar = _factory.HataLogu.Hatalar.ToArray(); }
            hatalar.Should().BeEmpty(
                "reddedilen istek log KIRLETMEMELI; onceden her istek SqlException yigin izi yaziyordu. " +
                "Bulunanlar: " + string.Join(" | ", hatalar));
        }

        // VAKUM KIRICI: sinir "her seyi reddet" degil. Sinir icindeki terim CALISMALI ve
        // arama GERCEKTEN eslesmeli - yoksa yukaridaki pin, aramayi tumden bozan bir
        // uygulamada da yesil kalirdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task AramaTerimi_SINIR_ICINDE_200_Doner_ve_GERCEKTEN_ESLESIR()
        {
            if (Skipped()) return;
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            await using (var ctx = NewContext())
            {
                var kategori = new Category
                {
                    name = "Guvenlik-" + damga,
                    slug = "guvenlik-" + damga,   // NOT NULL (olculdu: eksikse insert duser)
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(kategori);
                await ctx.SaveChangesAsync();
                ctx.Set<Product>().Add(new Product
                {
                    name = "Sonda Urun " + damga,
                    brand = "SondaMarka",
                    category_id = kategori.id,
                    price = 100m,
                    description = "sonda",
                    color_hex = "#000000",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            var client = _factory!.CreateClient();
            var yanit = await client.GetAsync("/api/Search/products?query=" + damga);
            yanit.StatusCode.Should().Be(HttpStatusCode.OK, "normal uzunlukta terim CALISMALI");

            using var doc = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32()
                .Should().Be(1, "POZITIF OLAY: arama gercekten ESLESMELI - sinir aramayi bozmamali");

            // TAM SINIR (200 karakter) hala kabul edilmeli: sinir products.name genisliginden
            // turetildi, yani 200 karakterlik bir ad TEORIK OLARAK eslesebilir.
            (await client.GetAsync("/api/Search/products?query=" + new string('b', 200)))
                .StatusCode.Should().Be(HttpStatusCode.OK, "TAM sinir kabul edilmeli (200 = products.name genisligi)");
        }

        // G3b: ayni hata sinifi admin arama yuzeyinde de vardi (olculdu: 4000 karakter -> 500).
        [Fact]
        [Trait("Category", "Sql")]
        public async Task AdminMusteriAramasi_UZUN_TERIMDE_de_400_Doner()
        {
            if (Skipped()) return;
            var admin = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<Customer>().FirstAsync(c => c.id == admin.CustomerId);
                m.user_type = (byte)Divisima.Core.Utilities.Enums.UserTypeEnum.Admin;
                await ctx.SaveChangesAsync();
            }
            var yeniAdmin = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<Customer>().FirstAsync(c => c.id == yeniAdmin.CustomerId);
                m.user_type = (byte)Divisima.Core.Utilities.Enums.UserTypeEnum.Admin;
                await ctx.SaveChangesAsync();
            }
            // Token user_type'i GIRIS aninda tasiyor; tip degistikten SONRA yeniden giris gerekir.
            var anon = _factory!.CreateClient();
            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = yeniAdmin.Email, password = TestAuthHelper.TestPassword });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var token = loginDoc.RootElement.GetProperty("data").GetProperty("token").GetString();

            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // VAKUM KIRICI: once NORMAL aramanin gercekten calistigi (200) dogrulanir; aksi halde
            // asagidaki 400 "admin ucu zaten bozuk" sebebinden de gelebilirdi.
            (await adminClient.PostAsJsonAsync("/api/admin/customer/list", new { search = "a", page = 1, page_size = 5 }))
                .StatusCode.Should().Be(HttpStatusCode.OK, "normal arama CALISMALI");

            var uzun = await adminClient.PostAsJsonAsync("/api/admin/customer/list",
                new { search = new string('a', 4000), page = 1, page_size = 5 });
            uzun.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "admin arama yuzeyi de ayni sinira tabi - olculen once-durum: 4000 karakter -> HTTP 500");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // G2 - KAYIT ENUMERATION
        // ═══════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: var olan adres -> 400 "Bu e-posta adresi zaten kayitli.",
        // yeni adres -> 201. Anonim caginan TEK istekte "bu adres kayitli mi" sorusunu
        // yanitlayabiliyordu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Kayit_VAR_OLAN_ve_YENI_ADRES_AYNI_YANITI_Doner()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();
            var varOlan = $"g2-var-{Guid.NewGuid():N}@divisima.test";
            var yeni1 = $"g2-yeni1-{Guid.NewGuid():N}@divisima.test";
            var yeni2 = $"g2-yeni2-{Guid.NewGuid():N}@divisima.test";

            (await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(varOlan)))
                .StatusCode.Should().Be(HttpStatusCode.Created, "on kosul: ilk kayit gecmeli");

            var tekrar = await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(varOlan));
            var taze = await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(yeni1));

            tekrar.StatusCode.Should().Be(taze.StatusCode,
                "var olan adres ile yeni adres AYNI durum kodunu donmeli - farkli kod enumeration'dir");
            (await tekrar.Content.ReadAsStringAsync()).Should().Be(await taze.Content.ReadAsStringAsync(),
                "yanit GOVDELERI de birebir ayni olmali - mesaj farki da enumeration'dir");
            (await tekrar.Content.ReadAsStringAsync()).Should().NotContain("zaten",
                "cift-anlam kirici: eski sizdiran metin ('zaten kayitli') yanitta KALMAMALI");

            // POZITIF OLAY (vakum kirici): esitlik "her seye 400 don" ile de saglanabilirdi.
            // Yeni adres icin hesap GERCEKTEN acilmis olmali.
            await using var ctx = NewContext();
            (await ctx.Set<Customer>().IgnoreQueryFilters().CountAsync(c => c.email == yeni1))
                .Should().Be(1, "yeni adres icin hesap GERCEKTEN acilmali - yoksa pin bir vakum olurdu");
            // Ve var olan adres IKINCI bir satir URETMEMELI (B1'in kalici invarianti).
            (await ctx.Set<Customer>().IgnoreQueryFilters().CountAsync(c => c.email == varOlan))
                .Should().Be(1, "ayni adres IKINCI hesap acmamali - yanit esitlendi diye satir cogalmaz");
            (await ctx.Set<Customer>().IgnoreQueryFilters().CountAsync(c => c.email == yeni2))
                .Should().Be(0, "hic denenmemis adres icin satir olmamali - sayim sorgusu gercekten ayirt ediyor");
        }

        // OLCULEN ONCE-DURUM: ASKIYA ALINMIS hesabin adresiyle kayit -> HTTP 500.
        // Kok sebep: GetByEmailAsync global `is_active` filtresine tabiydi, hesap NULL gorunuyor,
        // INSERT unique indekse takiliyordu. Bu 500, yanit esitlendikten SONRA da (201 vs 500)
        // sizintiyi acik birakirdi - yani G2'nin bir parcasi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Kayit_ASKIYA_ALINMIS_ADRESTE_de_AYNI_YANIT_500_URETMEZ()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();
            var askidaki = $"g2-aski-{Guid.NewGuid():N}@divisima.test";
            var taze = $"g2-taze-{Guid.NewGuid():N}@divisima.test";

            (await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(askidaki)))
                .StatusCode.Should().Be(HttpStatusCode.Created);
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<Customer>().IgnoreQueryFilters().FirstAsync(c => c.email == askidaki);
                m.is_active = false;
                await ctx.SaveChangesAsync();
            }

            var askiyla = await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(askidaki));
            var tazeyle = await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(taze));

            askiyla.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
                "askiya alinmis adres 500 URETMEMELI - olculen once-durum tam buydu");
            askiyla.StatusCode.Should().Be(tazeyle.StatusCode, "askidaki hesap da ayirt edilememeli");
            (await askiyla.Content.ReadAsStringAsync()).Should().Be(await tazeyle.Content.ReadAsStringAsync());

            await using var son = NewContext();
            (await son.Set<Customer>().IgnoreQueryFilters().CountAsync(c => c.email == askidaki))
                .Should().Be(1, "askidaki hesap icin IKINCI satir acilmamali");
        }

        // G2b - OLCULEN ONCE-DURUM: resend-verification UC AYRI yanit veriyordu
        //   olmayan adres       -> 404 "E-posta veya sifre hatali."
        //   var + dogrulanmis   -> 200 "E-posta zaten dogrulanmis."
        //   var + dogrulanmamis -> 200 "Dogrulama e-postasi gonderildi."
        // Yani hem VARLIK hem DOGRULANMA DURUMU siziyordu (kayit ucundan DAHA fazla).
        [Fact]
        [Trait("Category", "Sql")]
        public async Task ResendVerification_UC_DURUMDA_da_AYNI_YANITI_Doner()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var dogrulanmis = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var dogrulanmamis = $"g2b-bekleyen-{Guid.NewGuid():N}@divisima.test";
            (await anon.PostAsJsonAsync("/api/auth/register", KayitGovdesi(dogrulanmamis)))
                .StatusCode.Should().Be(HttpStatusCode.Created);
            var olmayan = $"g2b-yok-{Guid.NewGuid():N}@divisima.test";

            var r1 = await anon.PostAsync($"/api/auth/resend-verification?email={Uri.EscapeDataString(olmayan)}", null);
            var r2 = await anon.PostAsync($"/api/auth/resend-verification?email={Uri.EscapeDataString(dogrulanmis.Email)}", null);
            var r3 = await anon.PostAsync($"/api/auth/resend-verification?email={Uri.EscapeDataString(dogrulanmamis)}", null);

            var imzalar = new[]
            {
                $"{(int)r1.StatusCode}|{await r1.Content.ReadAsStringAsync()}",
                $"{(int)r2.StatusCode}|{await r2.Content.ReadAsStringAsync()}",
                $"{(int)r3.StatusCode}|{await r3.Content.ReadAsStringAsync()}"
            };
            imzalar.Distinct().Should().HaveCount(1,
                "uc durumun UCU DE ayni yaniti vermeli. Bulunanlar: " + string.Join(" >< ", imzalar));

            // POZITIF OLAY (vakum kirici): esitlik "hicbir sey yapma" ile de saglanabilirdi.
            // Dogrulanmamis hesabin jetonu GERCEKTEN yenilenmis olmali.
            await using var ctx = NewContext();
            var bekleyen = await ctx.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == dogrulanmamis);
            bekleyen.email_verification_token.Should().NotBeNullOrWhiteSpace(
                "dogrulanmamis hesaba yeni jeton URETILMELI - yoksa uc sessizce olurdu");
            bekleyen.email_verification_sent_at.Should().NotBeNull();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // G1 - REFRESH TOKEN YENIDEN KULLANIM TESPITI
        // ═══════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: 1. yenileme 200 (rotasyon VAR), ESKI jeton 401, ama YENI jeton
        // hirsizlik sinyalinden SONRA da 200 doner - zincir AYAKTA kalirdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task DondurulmusRefreshToken_YENIDEN_SUNULUNCA_TUM_ZINCIR_IPTAL_EDILIR()
        {
            if (Skipped()) return;
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var anon = HamIstemci(_factory!);

            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var eski = CerezDegeri(CerezSatiri(login, "refresh_token"));
            var csrf = CerezDegeri(CerezSatiri(login, "csrf_token"));
            eski.Should().NotBeNullOrWhiteSpace();

            var ilk = await YenileAsync(anon, eski!, csrf!);
            ilk.StatusCode.Should().Be(HttpStatusCode.OK, "on kosul: normal yenileme calismali");
            var yeni = CerezDegeri(CerezSatiri(ilk, "refresh_token"));
            yeni.Should().NotBeNullOrWhiteSpace().And.NotBe(eski, "rotasyon YENI bir jeton uretmeli");

            // HIRSIZLIK SINYALI: dondurulmus jeton ikinci kez sunuluyor.
            (await YenileAsync(anon, eski!, csrf!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "dondurulmus jeton reddedilmeli");

            // ASIL PIN: sinyalden SONRA YENI jeton da calismamali. ONCEDEN 200 DONUYORDU.
            (await YenileAsync(anon, yeni!, csrf!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "yeniden kullanim sinyalinden sonra ZINCIRIN TAMAMI iptal edilmeli - onceden YENI jeton " +
                "hala 200 donuyordu ve saldirgan rotasyona devam edebiliyordu");

            await using (var ctx = NewContext())
            {
                (await ctx.Set<UserSession>().AsNoTracking().CountAsync(s => s.customer_id == user.CustomerId && s.is_active))
                    .Should().Be(0, "musterinin AKTIF oturumu kalmamali - kullanici yeniden giris yapar");
                (await ctx.Set<SecurityEvent>().AsNoTracking()
                    .CountAsync(e => e.customer_id == user.CustomerId && e.event_type == "RefreshTokenReuse"))
                    .Should().Be(1, "olay GURULTULU olmali: guvenlik defterine TAM 1 kayit dusmeli");
            }

            // ALARM SPAM'I OLMAMALI: zincir bir kez iptal edildikten sonra ayni jetonlarla
            // yapilan her deneme 401 alir ama YENI alarm URETMEZ. Kosulsuz loglama, tekrar
            // deneyen bir istemcide admin bildirimini spam'a cevirir ve GERCEK sinyali gomerdi.
            // (Olculdu: ilk yazimda bu test "2 olay" buldu - duzeltme oradan dogdu.)
            for (var i = 0; i < 3; i++)
                (await YenileAsync(anon, yeni!, csrf!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            await using var son = NewContext();
            (await son.Set<SecurityEvent>().AsNoTracking()
                .CountAsync(e => e.customer_id == user.CustomerId && e.event_type == "RefreshTokenReuse"))
                .Should().Be(1, "olu bir zincire yapilan tekrar denemeler YENI alarm uretmemeli");
        }

        // VAKUM/CIFT-ANLAM KIRICI: iptal "her yenilemede oturumlari kapat" ile de saglanabilirdi.
        // Sinyal YOKKEN rotasyon zinciri AYAKTA kalmali ve ard arda yenileme CALISMALI.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task HIRSIZLIK_SINYALI_YOKSA_Ardisik_Yenileme_CALISIR_ZincirYASAR()
        {
            if (Skipped()) return;
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var anon = HamIstemci(_factory!);

            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            var jeton = CerezDegeri(CerezSatiri(login, "refresh_token"));
            var csrf = CerezDegeri(CerezSatiri(login, "csrf_token"));

            for (var tur = 1; tur <= 3; tur++)
            {
                var r = await YenileAsync(anon, jeton!, csrf!);
                r.StatusCode.Should().Be(HttpStatusCode.OK, $"{tur}. ardisik yenileme calismali (sinyal YOK)");
                jeton = CerezDegeri(CerezSatiri(r, "refresh_token"));
                jeton.Should().NotBeNullOrWhiteSpace();
            }

            await using var ctx = NewContext();
            (await ctx.Set<SecurityEvent>().AsNoTracking()
                .CountAsync(e => e.customer_id == user.CustomerId && e.event_type == "RefreshTokenReuse"))
                .Should().Be(0, "mesru rotasyon hirsizlik olayi URETMEMELI - yoksa gercek sinyal gurultuye gomulur");
        }

        private static async Task<HttpResponseMessage> YenileAsync(HttpClient c, string refresh, string csrf)
        {
            var istek = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            istek.Headers.Add("Cookie", $"refresh_token={refresh}; csrf_token={csrf}");
            istek.Headers.Add("X-CSRF-Token", csrf);
            return await c.SendAsync(istek);
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // G6 - KARGO UCU VARLIK SIZDIRIYORDU
        // ═══════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: baskasinin siparisinin kargosu -> 403 "Bu kargo size ait degil.",
        // OLMAYAN siparisin kargosu -> 404. Fark, kaydin VAR oldugunu dogruluyordu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task BaskasininKargosu_404_Doner_VAR_OLMAYANLA_AYNI()
        {
            if (Skipped()) return;
            var sahip = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var yabanci = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            int siparisId;
            await using (var ctx = NewContext())
            {
                var siparis = new Order
                {
                    customer_id = sahip.CustomerId,
                    order_number = "G6-" + Guid.NewGuid().ToString("N").Substring(0, 10),
                    total_price = 100m,
                    status = 1,
                    created_at = DateTime.Now
                };
                ctx.Set<Order>().Add(siparis);
                await ctx.SaveChangesAsync();
                siparisId = siparis.id;
            }

            var yabanciyaVarOlan = await yabanci.Client.GetAsync($"/api/shipment/track/{siparisId}");
            var yabanciyaOlmayan = await yabanci.Client.GetAsync("/api/shipment/track/99999999");

            yabanciyaVarOlan.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "sahiplik ihlali de 'bulunamadi' donmeli - 403 kaydin VAR oldugunu dogruluyordu");
            ((int)yabanciyaVarOlan.StatusCode).Should().Be((int)yabanciyaOlmayan.StatusCode,
                "var olan ile olmayan AYIRT EDILEMEMELI");
            (await yabanciyaVarOlan.Content.ReadAsStringAsync()).Should()
                .NotContain("size ait", "eski sizdiran metin YANITTA KALMAMALI");

            // POZITIF OLAY (vakum kirici): 404 "uc bozuk" oldugu icin degil, SAHIPLIK yuzunden.
            // Sahibi ayni ucu cagirinca 404'ten FARKLI bir sonuc almali (kargo kaydi yoksa
            // "kargo bulunamadi" gelir - o da 404'tur, bu yuzden GOVDE ayrimi olculur).
            var sahibe = await sahip.Client.GetAsync($"/api/shipment/track/{siparisId}");
            (await sahibe.Content.ReadAsStringAsync()).Should().NotBe(
                await yabanciyaVarOlan.Content.ReadAsStringAsync(),
                "sahibi ile yabanci AYNI cevabi almamali - yoksa uc herkese ayni seyi diyen bir vakum olurdu");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // G5 - KIMLIK DOGRULAMA VARSAYILAN
        // ═══════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: FallbackPolicy YOKTU. Bugun bir bosluk yoktu (150 action'in tamami
        // oznitelikli) ama yetki ozniteligi UNUTULAN yeni bir uc SESSIZCE herkese acik olurdu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task YetkiOznitelugu_UNUTULAN_Uc_VARSAYILAN_OLARAK_401_Doner()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var unutulan = await anon.GetAsync("/api/test/g5-sonda/oznitelik-unutuldu");
            unutulan.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "yetki ozniteligi OLMAYAN uc VARSAYILAN OLARAK kapali olmali - onceden 200 donerdi");

            // CIFT-ANLAM KIRICI: 401 "sonda controller'i hic maplenmedi" yuzunden degil.
            // AYNI controller'daki [AllowAnonymous] kardesi 200 donmeli.
            var acik = await anon.GetAsync("/api/test/g5-sonda/acikca-anonim");
            acik.StatusCode.Should().Be(HttpStatusCode.OK,
                "ACIKCA isaretlenmis anonim uc calismaya devam etmeli - fallback her seyi kapatmamali");
        }

        // Varsayilan-kapali kuralin MEVCUT ACIK UCLARI KIRMADIGI + her uretim ucunun ACIKCA
        // isaretli oldugu. Ikincisi (C) dalgasindaki bir kerelik taramanin PIN'e cevrilmis hali:
        // tarama bir gunun olcumudur, pin onu surekli kilar.
        //
        // KAPSAM NOTU: kural `MapControllers().RequireAuthorization()` ile CONTROLLER'LARA bagli
        // (FallbackPolicy DEGIL - gerekcesi Program.cs'te olculerek yazildi). Bu yuzden ileride
        // eklenecek bir minimal-API ucu runtime'da degil BURADA yakalanir.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task VarsayilanKapali_ACIK_Uclari_KIRMAZ_ve_HER_UC_ACIKCA_ISARETLIDIR()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            foreach (var yol in new[] { "/health", "/health/live", "/health/ready" })
                (await anon.GetAsync(yol)).StatusCode.Should().Be(HttpStatusCode.OK,
                    $"{yol} ANONIM erisilebilir KALMALI - orkestratör probe'u 401 alirsa pod saglıksiz sayilir");

            // Storefront'un anonim yuzeyi ayakta kalmali (vakum kirici: kural 'her seyi kapat' degil)
            (await anon.GetAsync("/api/category/getlist")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await anon.GetAsync("/api/Search/products?query=x")).StatusCode.Should().Be(HttpStatusCode.OK);
            // ... ama KORUMALI uc hala 401 (kuralin her seyi ACMADIGI da olculur)
            (await anon.GetAsync("/api/product/getlist")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // TARAMA -> PIN. Oznitelikler DOGRUDAN yansimayla okunuyor: `EndpointMetadata`
            // uzerinden okumak yaniltici olurdu, cunku RequireAuthorization() konvansiyonu HER
            // controller ucune AuthorizeAttribute EKLER ve tarama vakuma donerdi. Sorulan soru
            // "emniyet agi var mi" degil, "NIYET koda YAZILMIS mi".
            var uretimAssembly = typeof(Program).Assembly;
            var actions = _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
                .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                .Where(a => a.ControllerTypeInfo.Assembly == uretimAssembly)
                .ToList();
            actions.Should().HaveCountGreaterThan(100,
                "vakum kirici: uretim action'lari GERCEKTEN taranmali (C dalgasinda 150 olculdu)");

            static bool Isaretli(ControllerActionDescriptor a) =>
                a.MethodInfo.GetCustomAttributes(true).Any(x => x is IAuthorizeData or IAllowAnonymous)
                || a.ControllerTypeInfo.GetCustomAttributes(true).Any(x => x is IAuthorizeData or IAllowAnonymous);

            var isaretsiz = actions.Where(a => !Isaretli(a))
                .Select(a => $"{a.ControllerName}.{a.ActionName}").ToList();
            isaretsiz.Should().BeEmpty(
                "her uretim ucu ACIKCA yetkili ya da ACIKCA anonim olmali; varsayilan-kapali kural bir " +
                "emniyet agidir, niyet beyani degildir. Isaretsizler: " + string.Join(", ", isaretsiz));

            // CIFT-ANLAM KIRICI: tarama gercekten ayirt ediyor mu? Test derlemesindeki SONDA
            // controller'i bilerek ISARETSIZDIR ve taramaya girseydi listede gorunurdu.
            Isaretli(_factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
                    .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                    .First(a => a.ActionName == nameof(G5FallbackSondaController.Unutuldu)))
                .Should().BeFalse("tarama isaretsiz bir ucu GERCEKTEN isaretsiz gormeli");
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // G9 / G8 / G7
        // ═══════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: use_store_credit = -1000 -> HTTP 201 (siparis olustu). Bakiye
        // DEGISMEMISTI (manager degeri yutuyordu), yani zarar yoktu - dogrulama boslugu vardi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task NegatifMagazaKredisi_400_Doner_SIPARIS_OLUSMAZ()
        {
            if (Skipped()) return;
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var govde = new
            {
                customer_id = user.CustomerId,
                coupon_code = "",
                use_store_credit = -1000m,
                payment_method = (byte)1,
                items = new[] { new { product_id = 1, size = "M", quantity = 1 } }
            };
            var yanit = await user.Client.PostAsJsonAsync("/api/order/place", govde);
            yanit.StatusCode.Should().Be(HttpStatusCode.BadRequest, "negatif kredi GIRISTE reddedilmeli");
            (await yanit.Content.ReadAsStringAsync()).Should().Contain("negatif",
                "cift-anlam kirici: 400 baska bir dogrulama sebebinden degil, NEGATIF krediden gelmeli");

            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == user.CustomerId))
                .Should().Be(0, "reddedilen istek siparis OLUSTURMAMALI - 400 kozmetik degil");
        }

        // OLCULEN ONCE-DURUM: her yanit "Server: Kestrel" tasiyordu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Server_Basligi_YANITTA_YOK()
        {
            if (Skipped()) return;
            var yanit = await _factory!.CreateClient().GetAsync("/health");

            yanit.Headers.Contains("Server").Should().BeFalse(
                "sunucu yigini beyan edilmemeli - onceden 'Server: Kestrel' donuyordu");

            // VAKUM KIRICI: baslik "yanit bos geldigi" icin degil, KAPATILDIGI icin yok.
            // Guvenlik basliklari yerinde duruyor olmali.
            yanit.Headers.Contains("X-Content-Type-Options").Should().BeTrue(
                "diger guvenlik basliklari HALA yazilmali - yoksa bu pin sadece bos bir yaniti olcerdi");
        }

        // OLCULEN ONCE-DURUM (satici kaydi KAPALI): eksik govde -> 400 "The email field is
        // required." (kapi HIC gorulmeden), gecerli govde -> 403. Kapali kapi, arkasindaki
        // DTO sozlesmesini anlatiyordu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task SaticiKaydi_KAPALIYKEN_EKSIK_GOVDE_de_403_Doner()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var eksik = await anon.PostAsJsonAsync("/api/seller/auth/register", new { });
            eksik.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "kapi dogrulamadan ONCE kosmali - onceden 400 + alan adlari donuyordu");
            (await eksik.Content.ReadAsStringAsync()).Should().NotContain("required",
                "DTO'nun zorunlu alanlari kapali kapinin arkasindan SIZMAMALI");
        }

        // CIFT-ANLAM KIRICI: 403 "uc bozuk" degil. Kapi ACIKKEN dogrulama normal isler ve
        // eksik govde yine 400 alir - yani filtre yalniz kapi kapaliyken devreye giriyor.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task SaticiKaydi_ACIKKEN_EKSIK_GOVDE_400_Doner_DogrulamaCalisir()
        {
            if (Skipped()) return;
            await using var acikFactory = new GuvenlikFactory(saticiKaydiAcik: true);
            var anon = acikFactory.CreateClient();

            var eksik = await anon.PostAsJsonAsync("/api/seller/auth/register", new { });
            eksik.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "kapi acikken model dogrulamasi NORMAL islemeli - filtre yalniz kapali kapiyi korur");
        }
    }

    // ══ G5 SONDA CONTROLLER'I - YALNIZ TEST DERLEMESINDE ══════════════════════════════════
    //
    // "Oznitelik unutulan bir uc 401 verir" iddiasi ancak GERCEKTEN oznitelisiz bir uc varsa
    // olculebilir. Uretim koduna boyle bir uc EKLENMEZ; bu sinif test derlemesinde durur ve
    // uygulama parcasi olarak YALNIZ SecurityHardeningTests fabrikasina eklenir.
    [ApiController]
    [Route("api/test/g5-sonda")]
    public sealed class G5FallbackSondaController : ControllerBase
    {
        // BILEREK yetki ozniteligi YOK - "gelistirici unuttu" durumunu temsil eder.
        [HttpGet("oznitelik-unutuldu")]
        public IActionResult Unutuldu() => Ok(new { ok = true });

        // Kardes uc: ACIKCA anonim. Cift-anlam kirici olarak kullanilir.
        [HttpGet("acikca-anonim")]
        [AllowAnonymous]
        public IActionResult Anonim() => Ok(new { ok = true });
    }
}
