using System.Globalization;
using System.Net;
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
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
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
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
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

        // ── 2) FATURA GOVDESI KOSUCU KULTURUNDEN BAGIMSIZ tr BICIMI TASIR ────────────
        //
        // ASIL PIN. Beklenen deger ACIKCA tr-TR ile hesaplaniyor - CurrentCulture ile DEGIL.
        // Boylece invariant bir kosucuda (GitHub Actions) da AYNI degeri bekler: uygulama
        // kulturu pinlemezse orada "1,049.70" cikar ve bu assert kirilir.
        //
        // CIFT-ANLAM KIRICI: yalniz "tr bicimi var" demiyoruz - invariant bicimin govdede
        // BULUNMADIGI da olculuyor. Aksi halde her iki bicimi birden basan bir cikti da gecerdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task FaturaGovdesi_KOSUCU_KULTURUNDEN_BAGIMSIZ_tr_BICIMI_Tasir()
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
            }

            var resp = await user.Client.GetAsync($"/api/order/{orderId}/invoice-html");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var govde = await resp.Content.ReadAsStringAsync();
            govde.Should().NotBeNullOrWhiteSpace("POZITIF OLAY: fatura govdesi gercekten uretilmeli");

            var tr = new CultureInfo("tr-TR");
            var beklenen = 1049.70m.ToString("N2", tr);                 // "1.049,70"
            var invariant = 1049.70m.ToString("N2", CultureInfo.InvariantCulture);   // "1,049.70"

            govde.Should().Contain(beklenen,
                $"tutar tr bicimiyle basilmali (beklenen: {beklenen}); uygulama kulturu pinlemezse " +
                $"invariant kosucuda '{invariant}' cikar");
            govde.Should().NotContain(invariant,
                $"invariant bicim ('{invariant}') govdede HIC bulunmamali - cift-anlam kirici");
        }
    }
}
