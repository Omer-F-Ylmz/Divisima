using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Product;
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
    // ══ DALGA C - YAYIN ALTYAPISI ═════════════════════════════════════════════════════════
    //
    // Bu dalganin sorusu: "depo bugun yayina cikabilir mi?" Alti kalem olculdu; hepsinin
    // ortak ozelligi UYGULAMANIN CALISMASI degil, YAYINLANABILMESIYDI:
    //
    //   C1 storefront'u KIMIN sunacagi depoda TANIMSIZDI (Dockerfile yalniz API'yi publish
    //      ediyor, nginx.conf'ta yalniz api.divisima.com blogu vardi)
    //   C2 yuklenen gorseller konteynerin YAZILABILIR KATMANINDA - konteyner degisince kayip
    //   C3 ilk admin hic acilmamisti; ustelik AdminSeeder sifre politikasinin BESINCI ve
    //      GOZDEN KACMIS giris noktasiydi
    //   C4 uretimde yedi arka plan isi kosuyor ve biri dustugunde operatorun gorebilecegi
    //      HICBIR yuzey yoktu
    //   C5 robots.txt sitemap'i gosteriyor ama o adresi SUNAN hicbir sey yok; og:image/og:url
    //      eksik oldugu icin paylasimlar gorselsiz cikiyordu
    //   C6 Update'in stok dongusu transaction'siz; kargo ekrani kor form
    //
    // BU DOSYA: davranisla dogrulanabilenler (C3, C4, C6a). Yapilandirma artefaktlari
    // (C1, C2, C5, C6b) DalgaCDagitimSozlesmesiTests'te kaynak sozlesmesi olarak tutulur -
    // Docker/nginx bu suitte AYAGA KALDIRILAMAZ ve "kaldirilabilirmis gibi" yapan bir pin
    // yalanci guvence olurdu.
    [Trait("Category", "Sql")]
    public class DalgaCYayinAltyapisiTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaDalgaCTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        // Politikayi KARSILAYAN dusuk entropili sabit (>=8, buyuk, kucuk, rakam).
        // CLAUDE.md bolum 1: anahtar kelimeye bitisik YUKSEK ENTROPILI literal YAZILMAZ.
        private const string GecerliSifre = "Divisima2026";
        private const string ZayifSifre = "abc";

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

        private sealed class SahteMail : IMailService
        {
            public Task SendAsync(MailMessageDto message) => Task.CompletedTask;
        }

        // AdminSeed ayarlari HOST BUILDER'a verilir - gercek yapilandirma yolunun ta kendisi
        // (uretimde env/Key Vault'tan gelir). Testte elle Customer satiri YAZILMAZ.
        private sealed class DalgaCFactory : WebApplicationFactory<Program>
        {
            private readonly (string key, string value)[] _ayarlar;
            public DalgaCFactory(params (string key, string value)[] ayarlar) { _ayarlar = ayarlar; }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                foreach (var (k, v) in _ayarlar) builder.UseSetting(k, v);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    services.AddScoped<IMailService, SahteMail>();
                });
            }
        }

        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using var pre = NewContext();
                await pre.Database.EnsureDeletedAsync();
                await pre.Database.EnsureCreatedAsync();
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak Dalga C testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // ══ C3 - ILK ADMIN ═══════════════════════════════════════════════════════════════
        // "Uretimde ilk admin nasil acilacak" sorusunun cevabi TEMIZ BIR VERITABANINDA
        // surulmus olmali - bu pinler tam olarak onu yapar (her test kendi bos DB'siyle baslar).

        [Fact]
        public async Task IlkAdmin_TEMIZ_VERITABANINDA_Olusur_ve_GERCEKTEN_GIRIS_YAPABILIR()
        {
            if (Skipped()) return;
            var eposta = $"ilk-admin-{Guid.NewGuid():N}@divisima.test";

            await using var factory = new DalgaCFactory(
                ("AdminSeed:Enabled", "true"),
                ("AdminSeed:Email", eposta),
                ("AdminSeed:Password", GecerliSifre),
                ("AdminSeed:Name", "Yonetici"));
            var client = factory.CreateClient();   // host kurulunca tohumlama KOSAR

            await using (var ctx = NewContext())
            {
                var admin = await ctx.Set<Customer>().AsNoTracking().SingleOrDefaultAsync(c => c.email == eposta);
                admin.Should().NotBeNull("AdminSeed etkinken ilk admin OLUSTURULMALI");
                admin!.user_type.Should().Be((byte)UserTypeEnum.Admin);
                admin.email_verified.Should().BeTrue("seed admin dogrulanmis kabul edilir - aksi halde giris yapamaz");
                admin.is_active.Should().BeTrue();
                admin.phone.Should().NotBeNullOrWhiteSpace("customers.phone NOT NULL");
            }

            // ASIL SINAV - VAKUM KIRICI: satirin var olmasi yetmez, o hesapla GERCEKTEN
            // GIRILEBILMELI. Yanlis hash'lenmis ya da dogrulanmamis bir admin satiri
            // yukaridaki assertlerin hepsini gecer ama operator panele GIREMEZ.
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = eposta, password = GecerliSifre });
            login.StatusCode.Should().Be(HttpStatusCode.OK,
                $"ilk admin GIRIS YAPABILMELI: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await login.Content.ReadAsStringAsync())}");
            using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("data").GetProperty("token").GetString()
                .Should().NotBeNullOrWhiteSpace("giris gercek bir token uretmeli");
        }

        [Fact]
        public async Task IlkAdmin_ZAYIF_SIFREYLE_OLUSTURULMAZ_ve_UYGULAMA_YINE_ACILIR()
        {
            if (Skipped()) return;
            var eposta = $"zayif-admin-{Guid.NewGuid():N}@divisima.test";

            await using var factory = new DalgaCFactory(
                ("AdminSeed:Enabled", "true"),
                ("AdminSeed:Email", eposta),
                ("AdminSeed:Password", ZayifSifre));

            // VAKUM KIRICI: uygulama GERCEKTEN ayaga kalkmali. FAIL-FAST BILINCLI OLARAK
            // SECILMEDI - AdminSeed tek seferlik bir onyukleme bayragidir; yanlis yazilmis bir
            // sifre yuzunden uygulamanin acilmamasi SITEYI TUMDEN INDIRIRDI.
            var client = factory.CreateClient();
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK,
                "zayif AdminSeed sifresi uygulamayi DURDURMAMALI");

            await using var ctx = NewContext();
            (await ctx.Set<Customer>().AsNoTracking().AnyAsync(c => c.email == eposta))
                .Should().BeFalse("politikaya uymayan sifreyle admin OLUSTURULMAMALI - kayit ucunun "
                                + "reddedecegi bir sifre sistemin EN YETKILI hesabinda kabul edilemez");
            (await ctx.Set<Customer>().AsNoTracking().CountAsync(c => c.user_type == (byte)UserTypeEnum.Admin))
                .Should().Be(0, "hicbir admin olusmamis olmali");
        }

        [Fact]
        public async Task IlkAdmin_IDEMPOTENT_IKINCI_ACILIS_BASKA_EPOSTAYLA_da_IKINCI_ADMIN_ACMAZ()
        {
            if (Skipped()) return;
            var ilk = $"ilk-{Guid.NewGuid():N}@divisima.test";
            var ikinci = $"ikinci-{Guid.NewGuid():N}@divisima.test";

            await using (var f1 = new DalgaCFactory(
                ("AdminSeed:Enabled", "true"), ("AdminSeed:Email", ilk), ("AdminSeed:Password", GecerliSifre)))
            {
                _ = f1.CreateClient();
            }

            // IKINCI ACILIS - FARKLI e-posta. Yine de ikinci admin ACILMAMALI: yanlis
            // yapilandirilmis bir yeniden baslatma sessizce EK bir yetkili hesap uretmemeli.
            await using (var f2 = new DalgaCFactory(
                ("AdminSeed:Enabled", "true"), ("AdminSeed:Email", ikinci), ("AdminSeed:Password", GecerliSifre)))
            {
                _ = f2.CreateClient();
            }

            await using var ctx = NewContext();
            var adminler = await ctx.Set<Customer>().AsNoTracking()
                .Where(c => c.user_type == (byte)UserTypeEnum.Admin).ToListAsync();

            adminler.Should().HaveCount(1, "tohumlama IDEMPOTENT olmali");
            adminler[0].email.Should().Be(ilk, "ilk acilistaki hesap KORUNMALI");
            (await ctx.Set<Customer>().AsNoTracking().AnyAsync(c => c.email == ikinci))
                .Should().BeFalse("ikinci e-posta HIC yazilmamis olmali");
        }

        [Fact]
        public async Task AdminSeed_KAPALIYKEN_HICBIR_ADMIN_ACILMAZ()
        {
            if (Skipped()) return;
            // CIFT-ANLAM KIRICI: yukaridaki uc pin "admin olustu / olusmadi" olcuyor. Bayrak
            // KAPALIYKEN de olusmadigini gormeden, guvenli varsayilan KANITLANMAMIS kalirdi.
            //
            // Enabled ACIKCA "false" veriliyor, "vermemek" yeterli DEGIL: bu pin ilk yazildiginda
            // bayragi bos birakmisti ve YEREL MAKINEDE KIRILDI - `dotnet user-secrets` icinde
            // AdminSeed:Enabled=true duruyordu ve WebApplicationFactory onu yukluyordu (ayrinti
            // TestHostConfig'te). Yapilandirmayi olcen bir pin, degeri KENDISI vermelidir; aksi
            // halde sonucu calistiran makinenin secret'lari belirler.
            await using var factory = new DalgaCFactory(("AdminSeed:Enabled", "false"),
                                                       ("AdminSeed:Email", "kapali@divisima.test"),
                                                       ("AdminSeed:Password", GecerliSifre));
            _ = factory.CreateClient();

            await using var ctx = NewContext();
            (await ctx.Set<Customer>().AsNoTracking().CountAsync(c => c.user_type == (byte)UserTypeEnum.Admin))
                .Should().Be(0, "AdminSeed:Enabled false iken tohumlama HIC kosmamali");
        }

        // ══ C4 - BASARISIZ ARKA PLAN ISLERI GORUNUR ══════════════════════════════════════
        [Fact]
        public async Task BasarisizArkaPlanIsleri_ADMIN_UCUNDAN_GORUNUR_PAYLOAD_SIZMAZ()
        {
            if (Skipped()) return;
            await using var factory = new DalgaCFactory();

            // Gercek outbox tablosuna Failed bir mesaj yaz - OutboxProcessor'un 5 denemeden
            // sonra biraktigi halin ta kendisi.
            await using (var ctx = NewContext())
            {
                ctx.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    event_type = "EmailNotification",
                    payload = "{\"To\":\"gizli.musteri@example.com\",\"Subject\":\"x\",\"Body\":\"y\"}",
                    status = (byte)OutboxStatusEnum.Failed,
                    retry_count = 5,
                    error = "SMTP baglantisi kurulamadi.",
                    created_at = DateTime.Now
                });
                // ISLENMIS bir mesaj da eklenir: uc YALNIZ basarisiz olanlari donmeli.
                ctx.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    event_type = "OrderPlaced",
                    payload = "{}",
                    status = (byte)OutboxStatusEnum.Processed,
                    retry_count = 0,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            var client = await AdminIstemciAsync(factory);
            var r = await client.GetAsync("/api/dashboard/failed-jobs?take=50");
            r.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var liste = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

            liste.Should().HaveCount(1, "yalniz Failed mesajlar donmeli - Processed olanlar operatorun sorunu degil");
            liste[0].GetProperty("event_type").GetString().Should().Be("EmailNotification");
            liste[0].GetProperty("retry_count").GetInt32().Should().Be(5, "kac denemede pes edildigi gorunmeli");
            liste[0].GetProperty("error").GetString().Should().Contain("SMTP", "hata metni operatore ULASMALI");

            // CIFT-ANLAM KIRICI: "gorunur olsun" derken mesaj GOVDESI sizdirilmemeli - payload
            // e-posta adresi, jeton ve siparis ayrintisi tasir ve operatorun sorusuna
            // ("hangi is, kac denemede, hangi hatayla") gerekli DEGILDIR.
            liste[0].TryGetProperty("payload", out _).Should().BeFalse("mesaj govdesi DTO'da YOK");
            (await r.Content.ReadAsStringAsync()).Should().NotContain("gizli.musteri@example.com",
                "payload icindeki kisisel veri yanita SIZMAMALI");
        }

        [Fact]
        public async Task BasarisizIsUcu_ANONIM_ve_MUSTERI_TARAFINDAN_OKUNAMAZ()
        {
            if (Skipped()) return;
            await using var factory = new DalgaCFactory();

            // Anonim
            (await factory.CreateClient().GetAsync("/api/dashboard/failed-jobs")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "arka plan is hatalari ic bilgidir");

            // Giris yapmis MUSTERI (admin degil)
            var user = await TestAuthHelper.CreateCustomerClientAsync(factory);
            (await user.Client.GetAsync("/api/dashboard/failed-jobs")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, "musteri bu ucu goremez");
        }

        // ══ C6a - URUN GUNCELLEME STOK DONGUSU ATOMIK ════════════════════════════════════
        [Fact]
        public async Task UrunGuncelleme_STOK_DONGUSU_ATOMIK_TEK_BEDEN_HATASI_DIGERLERINI_YARIM_BIRAKMAZ()
        {
            if (Skipped()) return;
            await using var factory = new DalgaCFactory();
            var client = await AdminIstemciAsync(factory);
            var (urunId, katId) = await UrunHazirlaAsync();

            // Ayni beden IKI KEZ gonderiliyor - uc bunu ONDEN reddeder (Dalga B guard'i).
            // ASIL OLCULEN: reddedilen istek VERITABANINDA HICBIR IZ birakmamali. Guard
            // olmasaydi ilk beden yazilir, ikincisi unique indekse takilir ve TRANSACTION
            // OLMADAN yarim durum kalirdi.
            var r = await client.PutAsJsonAsync("/api/product/update", new ProductUpdateRequestDto
            {
                id = urunId,
                name = "DalgaC Urun",
                brand = "Divisima",
                category_id = katId,
                price = 499.90m,
                description = "d",
                color_hex = "#334455",
                product_type = ProductTypeEnum.Clothing,
                stocks = new List<ProductStockDto>
                {
                    new() { size = "M", stock_quantity = 33 },
                    new() { size = "m", stock_quantity = 44 }   // DB collation'inda AYNI anahtar
                }
            });
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest, await r.Content.ReadAsStringAsync());

            await using (var ctx = NewContext())
            {
                var satirlar = await ctx.Set<ProductStock>().AsNoTracking()
                    .Where(s => s.product_id == urunId).ToListAsync();
                satirlar.Should().HaveCount(1, "reddedilen istek YENI SATIR yazmamali");
                satirlar[0].stock_quantity.Should().Be(20, "reddedilen istek MEVCUT degeri de DEGISTIRMEMELI");
            }

            // VAKUM KIRICI: gecerli bir istek GERCEKTEN yaziyor olmali - yoksa yukaridaki
            // assert "hicbir sey yapmayan" bir uygulamada da yesil kalirdi.
            var ok = await client.PutAsJsonAsync("/api/product/update", new ProductUpdateRequestDto
            {
                id = urunId,
                name = "DalgaC Urun",
                brand = "Divisima",
                category_id = katId,
                price = 499.90m,
                description = "d",
                color_hex = "#334455",
                product_type = ProductTypeEnum.Clothing,
                stocks = new List<ProductStockDto> { new() { size = "M", stock_quantity = 33 } }
            });
            ok.StatusCode.Should().Be(HttpStatusCode.OK);

            await using var son = NewContext();
            (await son.Set<ProductStock>().AsNoTracking().SingleAsync(s => s.product_id == urunId))
                .stock_quantity.Should().Be(33);
        }

        // ── yardimcilar ───────────────────────────────────────────────────────────────────
        private static async Task<(int UrunId, int KategoriId)> UrunHazirlaAsync()
        {
            await using var ctx = NewContext();
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            var kat = new Category { name = "DalgaC " + damga, slug = "dalgac-" + damga, is_active = true, created_at = DateTime.Now };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "DalgaC Urun " + damga,
                description = "Dalga C pini icin urun.",
                color_hex = "#334455",
                brand = "Divisima",
                price = 499.90m,
                category_id = kat.id,
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(urun);
            await ctx.SaveChangesAsync();

            ctx.Set<ProductStock>().Add(new ProductStock
            {
                product_id = urun.id,
                size = "M",
                stock_quantity = 20,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return (urun.id, kat.id);
        }

        private static async Task<HttpClient> AdminIstemciAsync(WebApplicationFactory<Program> factory)
        {
            var user = await TestAuthHelper.CreateCustomerClientAsync(factory);
            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == user.CustomerId);
                c.user_type = (byte)UserTypeEnum.Admin;
                await ctx.SaveChangesAsync();
            }
            var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            login.IsSuccessStatusCode.Should().BeTrue(
                Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await login.Content.ReadAsStringAsync()));
            using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var token = doc.RootElement.GetProperty("data").GetProperty("token").GetString();
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }
}
