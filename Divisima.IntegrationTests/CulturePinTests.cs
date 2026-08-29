using System.Globalization;
using System.Net;
using System.Text.Json;
using Divisima.Bussiness.Abstract;
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
    // SPRINT 8 MADDE 13 - KULTUR PINLEME
    //
    // OLCULEN SORUN (E3 run'inda CANLI ORTAMDA gorundu, teori degil): uygulama hicbir yerde
    // kultur pinlemiyordu. `Program.cs`'te ne `RequestLocalization` ne
    // `CultureInfo.DefaultThreadCurrentCulture` vardi, csproj'de `InvariantGlobalization` ayari
    // da yoktu. Para/tarih bicimlendirmesi KOSTUGU KABININ yereline gore degisiyordu.
    //   OLCUM: tr-TR -> "549,90" / "1.049,70"   |   Invariant -> "549.90" / "1,049.70"
    // GitHub kosucusu (Linux, LANG=C.UTF-8) invariant kulturde kostugu icin fatura govdesindeki
    // tutar orada NOKTA ayracli basildi ve tr bicimi bekleyen bir test kirildi. Uretimdeki
    // karsiligi: LANG verilmemis bir dagitimda Turk musteriye kesilen faturada tutar
    // "1,049.70 TL" yazar - fatura MALI BIR BEYANDIR, bu bir gorunum meselesi degil.
    //
    // COZUM: `Program.cs`'te TEK NOKTA pinleme (tr-TR). RequestLocalization SECILMEDI cunku
    // (a) magaza tek pazarli - bicim istemcinin Accept-Language'ine gore degismemeli,
    // (b) fatura/e-posta/outbox ARKA PLAN islerinde de uretiliyor ve orada istek hatti yok.
    //
    // BU DOSYADAKI PINLER IKI KATMANI DA OLCER:
    //   1) pinlemenin GERCEKTEN kostugu (surec kulturu tr-TR),
    //   2) UCUN CIKTISININ tr bicimi tasidigi - assert ACIKCA tr-TR ile hesaplanir, yani
    //      invariant bir kosucuda da AYNI degeri bekler. Pinleme kaldirilirsa CI'da kirilir.
    [Trait("Category", "Sql")]
    public class CulturePinTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCulturePinTest";
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

        private sealed class CultureFactory : WebApplicationFactory<Program>
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

        private CultureFactory? _factory;
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
                _factory = new CultureFactory();
                _ = _factory.Services;      // host BURADA kurulur - Program.cs'in pinlemesi burada kosar
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak kultur pin testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // ── 1) SUREC KULTURU PINLENDI ────────────────────────────────────────────────
        //
        // Uygulama acildiginda kultur tr-TR'ye cekilmeli. Bu pin, ikinci pinin NEDEN gectigini
        // de acikliyor: cikti tr bicimli cunku SUREC tr-TR'de kosuyor.
        [Fact]
        [Trait("Category", "Sql")]
        public void UygulamaAcilinca_SurecKulturu_tr_TR_ye_PINLENIR()
        {
            if (Skipped()) return;

            CultureInfo.DefaultThreadCurrentCulture?.Name.Should().Be("tr-TR",
                "Program.cs acilista kulturu pinlemeli - aksi halde bicimlendirme kabin yereline duser");
            CultureInfo.CurrentCulture.Name.Should().Be("tr-TR",
                "pinleme yalniz yeni thread'lere degil, calisan koda da yansimali");
        }

        // ── 2) FATURA UCU SAYI BICIMLEMEZ - KULTUR SIZINTISI YAPISAL OLARAK YOK ──────
        //
        // ASIL PIN. Beklenen deger ACIKCA tr-TR ile hesaplaniyor - CurrentCulture ile DEGIL.
        // Boylece invariant bir kosucuda (GitHub Actions) da AYNI degeri bekler: uygulama
        // kulturu pinlemezse orada "1,049.70" cikar ve bu assert kirilir.
        //
        // CIFT-ANLAM KIRICI: yalniz "tr bicimi var" demiyoruz - invariant bicimin govdede
        // BULUNMADIGI da olculuyor. Aksi halde her iki bicimi birden basan bir cikti da gecerdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task FaturaUcu_SAYI_BICIMLEMEZ_HAM_DEGER_Doner_KulturSizintisi_YAPISAL()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            int orderId;
            await using (var ctx = NewContext())
            {
                var o = new Order
                {
                    customer_id = user.CustomerId,
                    order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + damga,
                    status = (byte)3,
                    subtotal = 999.80m,
                    discount_amount = 0m,
                    shipping_cost = 49.90m,
                    total_price = 1049.70m,     // tr: 1.049,70   |   invariant: 1,049.70
                    currency = "TRY",
                    payment_type = 0,
                    is_online_payment_done = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Order>().Add(o);
                await ctx.SaveChangesAsync();
                orderId = o.id;

                // MANTIK-FIX-2R / K2: KURGU DURUSTLESTI. Onceden bu bacak YALNIZ bir Order
                // satiri yaziyor, FATURA URETMIYORDU (olculdu: "Invoice" gecisi 0) - yani
                // "fatura govdesi" diye sinanan sey aslinda SIPARISTEN YENIDEN HESAPLANMIS
                // bir belgeydi. Uc artik KAYITTAN besleniyor; kurgu da gercek bir fatura kurar.
                var kat = new Category
                {
                    name = "Kultur Pin Kategori",
                    slug = $"kultur-{Guid.NewGuid():N}",
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(kat);
                await ctx.SaveChangesAsync();

                var urun = new Product
                {
                    name = "Kultur Pin Urun",
                    brand = "T",
                    category_id = kat.id,
                    price = 999.80m,
                    description = "kultur pini urunu",
                    color_hex = "#222222",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Products.Add(urun);
                await ctx.SaveChangesAsync();

                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = orderId,
                    product_id = urun.id,
                    size = "M",
                    quantity = 1,
                    unit_price = 999.80m,
                    is_cancelled = false,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            // FATURA URETIM YOLUNDAN kesilir (elle satir yazilmaz).
            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                var gen = await inv.GenerateForOrder(orderId);
                gen.Item1.Should().Be(HttpStatusCode.OK, $"fatura uretilmeli: {gen.Item2.Message}");
            }

            var resp = await user.Client.GetAsync($"/api/order/{orderId}/invoice-html");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var govde = await resp.Content.ReadAsStringAsync();
            govde.Should().NotBeNullOrWhiteSpace("POZITIF OLAY: fatura yaniti gercekten uretilmeli");

            // ── SOZLESMENIN YENI BICIMI (MANTIK-FIX-2R / K2) ──────────────────────────────
            // KORUNAN SOZLESME AYNI: kultur sizintisi yasagi. DEGISEN: nasil korundugu.
            //
            // ESKI: "govde tr bicimli parayi ICERMELI, invariant bicimi ICERMEMELI" -> sunucu
            //       parayi BICIMLIYORDU ve dogru bicim tek bir kulture (tr-TR) kilitliydi.
            //       O halde EN/AR kullanicisina dogru bicimi vermenin tek yolu sunucuda
            //       RequestLocalization acmakti - Sprint 8 madde 13 onu OLCEREK REDDETTI.
            // YENI: sunucu SAYI BICIMLEMEZ. Yanit HAM decimal tasir; bicimleme istemcide
            //       dvsLocale ile yapilir. Boylece kultur sizintisi YAPISAL OLARAK imkansiz
            //       hale gelir - "yanlis kulturde bicimlemek" degil, HIC BICIMLEMEMEK.
            //
            // CIFT-ANLAM KIRICI: HER IKI bicim de yasak. Yalnizca invariant'i yasaklamak,
            // sunucunun tr-TR'ye geri donmesini gormezdi.
            var tr = new CultureInfo("tr-TR");
            var trBicim = 1049.70m.ToString("N2", tr);                              // "1.049,70"
            var invariantBicim = 1049.70m.ToString("N2", CultureInfo.InvariantCulture); // "1,049.70"

            govde.Should().NotContain(trBicim,
                $"uc SAYI BICIMLEMEMELI - tr bicimli para dizgesi ('{trBicim}') yanitta bulunmamali");
            govde.Should().NotContain(invariantBicim,
                $"uc SAYI BICIMLEMEMELI - invariant bicimli para dizgesi ('{invariantBicim}') yanitta bulunmamali");

            // VAKUM KIRICI: "hicbir sey dondurme" de iki NotContain'i gecerdi. Alanlarin
            // GERCEKTEN ham decimal geldigi ayrica olculur.
            using var belge = JsonDocument.Parse(govde);
            var veri = belge.RootElement.GetProperty("data");
            veri.GetProperty("has_invoice").GetBoolean().Should().BeTrue("kurgu gercek fatura kurdu");
            veri.GetProperty("total").GetDecimal().Should().Be(1049.70m,
                "toplam HAM decimal olarak gelmeli - bicimlenmis dizge DEGIL");
            veri.GetProperty("total").ValueKind.Should().Be(JsonValueKind.Number,
                "alan SAYI olmali; dizge olsaydi bicimleme sunucuda yapilmis olurdu");
        }
    }
}
