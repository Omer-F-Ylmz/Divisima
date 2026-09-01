using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
    // ══ GF-1 / K2 (C-1) - ACCESS TOKEN IPTALI ══════════════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM (GUVENLIK-AV-1/K1 + bu dalganin on olcumu):
    //   - `TokenBlacklistMiddleware` OKUMA tarafi CANLIYDI (Program.cs ile boru hattinda),
    //   - ama `ITokenBlacklist.RevokeAsync` uretimde **SIFIR** yerden cagriliyordu,
    //   - ve dahasi kara liste KENDINI ZEHIRLIYORDU: `IsRevokedAsync` `GetOrSetAsync` ile
    //     okuyup anahtara `false` YAZIYOR, `RevokeAsync` da ayni `GetOrSetAsync`i kullandigi
    //     icin DOLU anahtari EZEMIYORDU. Yazma tarafi baglansa BILE iptal 60 sn'ye kadar
    //     SESSIZ NO-OP olurdu. (Kusur `RedisCacheService`te de AYNI - uretim dali dahil.)
    // Sonuc: cikis / sifre degisimi yapan kullanicinin ELDEKI access token'i 15 dakikaya
    // kadar CALISMAYA DEVAM EDIYORDU.
    //
    // KAPSAM SINIRI - RAPORA GIRER: iptal edilen sey SUNULAN jetondur. Kullanicinin BASKA
    // cihazlardaki access token'lari `jti`leri hicbir yerde SAKLANMADIGI icin iptal EDILEMEZ.
    // Tam coklu-cihaz iptali `tokens_valid_from` benzeri bir KOLON ister; bu dalganin TEK
    // migration'i K3'e ayrildi (merkez karari).
    [Trait("Category", "Sql")]
    public class AccessTokenIptalTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaAccessTokenIptalTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        // DUSUK ENTROPILI SABIT (depo kalibi - CLAUDE.md bolum 1): politikayi karsilar,
        // gitleaks `generic-api-key` esiginin COK ALTINDA.
        private const string YeniGecerliSifre = "Bbbbbb22";

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

        private sealed class IptalFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private IptalFactory? _factory;
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
                _factory = new IptalFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak access token iptal testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // Musteri-kapsamli, HAFIF, kimlik ISTEYEN uc. 401 <-> 200 ayrimi burada okunur.
        private const string KorumaliUc = "/api/Account/summary";

        // ── BACAK 1: CIKIS ──────────────────────────────────────────────────────────────────
        [Fact]
        public async Task K2_CIKISTAN_SONRA_ESKI_ACCESS_TOKEN_401_ALIR()
        {
            if (Skipped()) return;
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            // POZITIF OLAY KOSULU (vakum yasagi): jeton cikistan ONCE GERCEKTEN calisiyor.
            (await musteri.Client.GetAsync(KorumaliUc)).StatusCode.Should().Be(HttpStatusCode.OK,
                "on kosul: jeton cikistan ONCE gecerli olmali - yoksa 401 'zaten gecersizdi' anlamina gelirdi");

            (await musteri.Client.PostAsync("/api/auth/logout", null)).IsSuccessStatusCode
                .Should().BeTrue("cikis ucu basarili donmeli");

            var sonra = await musteri.Client.GetAsync(KorumaliUc);
            sonra.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "cikistan SONRA ayni access token REDDEDILMELI (once 200 doneriyordu - C-1)");

            // CIFT-ANLAM KIRICI: 401'in sebebi kara listedir, "hesap pasif" dali DEGIL.
            await using var ctx = NewContext();
            (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == musteri.CustomerId))
                .is_active.Should().BeTrue("hesap AKTIF kalmali - 401 hesap durumundan degil jeton iptalinden gelmeli");
        }

        // ── BACAK 2: SIFRE DEGISIMI ─────────────────────────────────────────────────────────
        [Fact]
        public async Task K2_SIFRE_DEGISIMINDEN_SONRA_ESKI_ACCESS_TOKEN_401_ALIR()
        {
            if (Skipped()) return;
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            (await musteri.Client.GetAsync(KorumaliUc)).StatusCode.Should().Be(HttpStatusCode.OK,
                "on kosul: jeton sifre degisiminden ONCE gecerli olmali");

            var degis = await musteri.Client.PostAsJsonAsync("/api/Account/change-password", new
            {
                current_password = TestAuthHelper.TestPassword,
                new_password = YeniGecerliSifre
            });
            var degisGovde = await degis.Content.ReadAsStringAsync();
            degis.IsSuccessStatusCode.Should().BeTrue($"sifre degisimi basarili olmali. Govde: {degisGovde}");

            (await musteri.Client.GetAsync(KorumaliUc)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "sifre degisiminden SONRA ayni access token REDDEDILMELI (once 200 doneriyordu - C-1)");

            await using var ctx = NewContext();
            (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == musteri.CustomerId))
                .is_active.Should().BeTrue("hesap AKTIF kalmali - 401 jeton iptalinden gelmeli");
        }

        // ── BACAK 3: HESAP PASIFLESTIRME ────────────────────────────────────────────────────
        //
        // DURUST KAYIT: bu bacagin 401'i K2'nin YENI kodundan DEGIL, ZATEN VAR OLAN
        // `TokenBlacklistMiddleware` hesap-durumu dalindan gelir (`is_active` + <=60 sn cache).
        // Yani bu pin bir REGRESYON KORUYUCUSUDUR, K2'nin kanitI DEGIL - ve boyle isaretlidir.
        // ONEMLI SINIR (A on olcumu): o dal YALNIZ `user_type == Customer` icin kosar; admin ve
        // satici jetonlari icin access-token engeli YOKTUR. Bu, GF-1 kapsaminda DEGIL.
        [Fact]
        public async Task K2_HESAP_PASIFLESINCE_ESKI_ACCESS_TOKEN_401_ALIR_MEVCUT_MEKANIZMA()
        {
            if (Skipped()) return;
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            (await musteri.Client.GetAsync(KorumaliUc)).StatusCode.Should().Be(HttpStatusCode.OK,
                "on kosul: jeton pasiflestirmeden ONCE gecerli olmali");

            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == musteri.CustomerId);
                c.is_active = false;
                await ctx.SaveChangesAsync();
            }

            // OLCULEN SINIR (ilk yazimda bu pini YANLIS kurmustum - kayit): DB'yi DOGRUDAN
            // cevirmek TEK BASINA 401 URETMEZ. Middleware hesap durumunu <=60 sn cache'liyor
            // (`AccountStatusTtl`) ve ilk istek `true` degerini ZATEN doldurmustu; o pencerede
            // gelen istek middleware'i GECER ve 404'e duser (Customer global sorgu filtresi
            // satiri gizledigi icin). Middleware'in kendi yorumu bu durumu zaten "DB'den elle
            // guncelleme" diye adlandirip TTL'i UST SINIR olarak tarif ediyor.
            (await musteri.Client.GetAsync(KorumaliUc)).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
                "SALT DB yazimi ban'i ANINDA etkili KILMAZ - cache penceresi (<=60 sn) gecerlidir; "
                + "bu satir o SINIRI pinliyor, davranisi savunmuyor");

            // URETIM YOLUNUN YAPTIGI SEY: hem satiri gunceller HEM anahtari duser
            // (`AdminCustomerManager.cs:104` ve `AccountManager.cs:316` birebir bunu yapiyor).
            // Ikinci adim uygulanınca ban ANINDA etkili olur.
            _factory!.Services.GetRequiredService<Divisima.Core.Utilities.Caching.ICacheService>()
                .Remove(Divisima.Core.Utilities.Caching.CacheKeys.CustomerActive(musteri.CustomerId));

            (await musteri.Client.GetAsync(KorumaliUc)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "pasiflestirme + anahtar dusurme sonrasi access token REDDEDILMELI");
        }
    }
}
