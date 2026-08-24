using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Seed;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Dtos.Content;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // E3 - LEGAL ICERIK TOHUMLAMA + IKI KATMAN SANITIZASYONUN YAZMA KATMANI
    //
    // OLCULEN ENGEL: storefront 10 sozlesme sayfasina link veriyor ama `contents` tablosu BOSTU
    // ve hicbir yerde tohumlama yoktu. Metinler index.html'de GOMULUYDU; gomuluyu kaldirip
    // API'ye baglamak tohumlama olmadan 10 BOS legal sayfa demekti.
    //
    // OLCULEN IKINCI ENGEL: ContentManager.Update ham govdeyi DOGRUDAN yaziyordu (satir 51-52),
    // hicbir sanitizasyon yoktu. Govde storefront'ta innerHTML ile ciziliyor - yani kayittaki
    // her sey CALISABILIR durumdaydi (stored XSS).
    [Trait("Category", "Sql")]
    public class ContentSeedAndSanitizeTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaContentSeedTest";
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

        private sealed class ContentFactory : WebApplicationFactory<Program>
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

        private ContentFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        private static ContentSeeder NewSeeder(DivisimaDbContext ctx) =>
            new ContentSeeder(new EfContentDal(ctx), NullLogger<ContentSeeder>.Instance);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new ContentFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak icerik tohumlama testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        // ── 1) TOHUM IDEMPOTENT: ADMIN DUZENLEMESI EZILMEZ ─────────────────────────────
        //
        // Bu pin tohumlamanin EN KRITIK sozlesmesini olcer. Seeder her uygulama acilisinda
        // kosuyor; "varsa guncelle" yazilsaydi, admin'in CMS'ten yaptigi her duzenleme bir
        // sonraki dagitimda SESSIZCE geri alinirdi - ve kimse fark etmezdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Tohumlama_IDEMPOTENT_AdminDuzenlemesi_SonrakiAciliste_EZILMEZ()
        {
            if (Skipped()) return;

            // 1. acilis: bos tablo -> 10 icerik eklenir
            await using (var ctx = NewContext())
                await NewSeeder(ctx).SeedAsync();

            int ilkSayi;
            await using (var ctx = NewContext())
                ilkSayi = await ctx.Set<Content>().CountAsync();
            ilkSayi.Should().Be(ContentSeeder.Tohumlar.Count,
                "bos tabloda TUM tohumlar eklenmeli - POZITIF OLAY kosulu");

            // Admin CMS'ten duzenliyor
            const string adminMetni = "<h3>ADMIN DUZENLEDI</h3><p>Bu metin CMS'ten yazildi.</p>";
            await using (var ctx = NewContext())
            {
                var kvkk = await ctx.Set<Content>().SingleAsync(c => c.slug == "kvkk");
                kvkk.body_tr = adminMetni;
                kvkk.title_tr = "ADMIN BASLIGI";
                await ctx.SaveChangesAsync();
            }

            // 2. acilis: seeder TEKRAR kosuyor
            await using (var ctx = NewContext())
                await NewSeeder(ctx).SeedAsync();

            await using (var ctx = NewContext())
            {
                var kvkk = await ctx.Set<Content>().AsNoTracking().SingleAsync(c => c.slug == "kvkk");
                kvkk.body_tr.Should().Be(adminMetni,
                    "seeder MEVCUT slug'a DOKUNMAMALI - admin duzenlemesi ezilirse CMS anlamsizlasir");
                kvkk.title_tr.Should().Be("ADMIN BASLIGI");

                (await ctx.Set<Content>().CountAsync()).Should().Be(ilkSayi,
                    "ikinci kosum KOPYA satir uretmemeli");
            }
        }

        // ── 2) TOHUM TEMIZ: Sanitize() TOHUMU DEGISTIRMEZ ──────────────────────────────
        //
        // Tohum ile yazma katmani sanitizasyonu ARASINDA CELISKI OLMADIGININ kaniti. Eger
        // tohum metinlerinden biri Sanitize'in soktugu bir sey iceriyorsa, admin o sayfayi
        // CMS'ten kaydettigi anda icerik SESSIZCE degisirdi (kayipli tur). Bu pin onu engeller.
        // SQL GEREKTIRMEZ - statik tohum listesini dogrudan okur.
        [Fact]
        public void TohumGovdeleri_Sanitize_ile_DEGISMEDEN_Gecer()
        {
            ContentSeeder.Tohumlar.Should().NotBeEmpty("tohum listesi bos olmamali - vakum kirici");

            foreach (var t in ContentSeeder.Tohumlar)
            {
                InputSanitizer.Sanitize(t.BodyTr).Should().Be(t.BodyTr,
                    $"'{t.Slug}' TR govdesi tohumda ZATEN temiz olmali");
                InputSanitizer.Sanitize(t.BodyEn).Should().Be(t.BodyEn,
                    $"'{t.Slug}' EN govdesi tohumda ZATEN temiz olmali");
                InputSanitizer.Sanitize(t.TitleTr).Should().Be(t.TitleTr, $"'{t.Slug}' TR basligi");
                InputSanitizer.Sanitize(t.TitleEn).Should().Be(t.TitleEn, $"'{t.Slug}' EN basligi");
            }
        }

        // ── 3) YAZMA KATMANI: SCRIPT'LI GOVDE TEMIZLENMIS DONER ────────────────────────
        //
        // CIFT-ANLAM KIRICI: yalniz "script yok" demiyoruz - MESRU HTML'in KORUNDUGUNU da
        // olcuyoruz. Aksi halde "her seyi encode et" gibi bir cozum de testi gecerdi ve CMS
        // govdesi bozulurdu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task IcerikGuncelleme_ScriptliGovde_TEMIZLENMIS_Kaydedilir_MesruHTML_KORUNUR()
        {
            if (Skipped()) return;

            await using (var ctx = NewContext())
                await NewSeeder(ctx).SeedAsync();

            int id;
            await using (var ctx = NewContext())
                id = (await ctx.Set<Content>().AsNoTracking().SingleAsync(c => c.slug == "gizlilik")).id;

            const string zararli =
                "<h3>Basligim</h3>" +
                "<script>alert('xss')</script>" +
                "<p>Mesru <strong>kalin</strong> metin.</p>" +
                "<img src=x onerror=alert(1)>" +
                "<svg/onload=alert(2)>" +
                "<a href=\"javascript:alert(3)\">tikla</a>" +
                "<iframe src=\"http://kotu.example\"></iframe>";

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IContentService>()
                .Update(new ContentUpdateRequestDto
                {
                    id = id,
                    title_tr = "Gizlilik",
                    title_en = "Privacy",
                    body_tr = zararli,
                    body_en = zararli
                }));

            sonuc.Item1.Should().Be(HttpStatusCode.OK, $"guncelleme basarili olmali: {sonuc.Item2.Message}");

            await using var oku = NewContext();
            var kayit = await oku.Set<Content>().AsNoTracking().SingleAsync(c => c.id == id);

            // TEHLIKELI OLANLAR GITMELI
            kayit.body_tr.Should().NotContain("<script", "script etiketi DEPOYA GIRMEMELI");
            kayit.body_tr.Should().NotContain("onerror", "olay yakalayici sokulmeli");
            kayit.body_tr.Should().NotContain("onload", "slash-ayracli olay yakalayici da sokulmeli");
            kayit.body_tr.Should().NotContain("javascript:", "javascript: protokolu sokulmeli");
            kayit.body_tr.Should().NotContain("<iframe", "iframe sokulmeli");

            // MESRU OLANLAR KALMALI (cift-anlam kirici: "hepsini sil/encode et" cozumu bu asserti gecemez)
            kayit.body_tr.Should().Contain("<h3>Basligim</h3>", "mesru basliklar KORUNMALI");
            kayit.body_tr.Should().Contain("<strong>kalin</strong>", "mesru bicimlendirme KORUNMALI");
            kayit.body_tr.Should().Contain("Mesru", "metin icerigi KORUNMALI");

            kayit.body_en.Should().NotContain("<script", "EN govdesi de temizlenmeli - tek dil degil");
        }
    }
}
