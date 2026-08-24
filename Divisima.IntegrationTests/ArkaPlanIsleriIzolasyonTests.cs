using System.Linq;
using Divisima.DataAccess.Concrete.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Divisima.IntegrationTests
{
    // === DALGA D - TEST HOST'LARINDA ARKA PLAN ISLERI KAPALI ============================
    //
    // OLCULEN ZARAR (CI kirmizisi cd51a52): `AddHangfireServer()` ve recurring job kayitlari
    // KOSULSUZDU. Her test host'u bir Hangfire sunucusu calistirip "outbox-processor" isini
    // DAKIKADA BIR kosuyordu. Bir test kendi drenajini yapip `retry_count == 1` beklerken
    // arka plan isi araya girip 2 yapabiliyordu:
    //
    //   Failed PaymentCallbackSecurityTests.YanEtkiHatasi_OdemeSUCCESS_KALIR_..._TAMAMLANIR
    //   Expected mesaj.retry_count to be 1 because deneme sayaci artmali, but found 2.
    //
    // YARIS ONCEDEN VARDI, YALNIZCA GORUNMUYORDU: dakikalik bir is ancak host YETERINCE UZUN
    // yasarsa atesler. Ayni test yerelde 3/3 GECTI (izole kosumda host saniyeler yasiyor);
    // CI'da suit daha uzun surdugu icin atesledi. CLAUDE.md'de kayitli ISIMSIZ FLAKE'lerin de
    // en olasi aciklamasi budur.
    //
    // AYRICA: Hangfire depolamasi `ConnectionStrings:DivisimaDb`e bagli - yani her test host'u
    // GELISTIRICININ veritabanina recurring job tanimi yaziyordu.
    //
    // Testler arka plan ZAMANLAMASINA dayanmiyor: outbox'i olcen her test isleyiciyi KENDISI
    // cagiriyor (`OutboxProcessor.ProcessPendingAsync`). Kapatmak hicbir testin OLCTUGU seyi
    // kaldirmaz - yalnizca YARISI kaldirir.
    [Trait("Category", "Sql")]
    public class ArkaPlanIsleriIzolasyonTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaArkaPlanIzolasyonTest";
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

        private sealed class IzolasyonFactory : WebApplicationFactory<Program>
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

        private IzolasyonFactory? _factory;
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
                _factory = new IzolasyonFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak izolasyon testi ortami hazirlanamadi - ATLANMAMALI.", ex);
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
        public void TEST_HOSTUNDA_HANGFIRE_ARKA_PLAN_SUNUCUSU_KOSMAZ()
        {
            if (Skipped()) return;

            var barindirilanlar = _factory!.Services.GetServices<IHostedService>().ToList();

            // VAKUM KIRICI: host GERCEKTEN barindirilan servis tasiyor olmali. Aksi halde
            // "Hangfire yok" iddiasi, hicbir hosted service olmadigi icin bedava dogru olurdu.
            barindirilanlar.Should().NotBeEmpty("host barindirilan servisleri cozebilmeli");

            var hangfireOlanlar = barindirilanlar
                .Where(s => (s.GetType().FullName ?? "").Contains("Hangfire", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.GetType().FullName)
                .ToList();

            hangfireOlanlar.Should().BeEmpty(
                "test host'unda Hangfire arka plan sunucusu KOSMAMALI - dakikalik outbox isi "
              + "testlerin kendi drenajiyla YARISIYORDU (CI'da retry_count 1 yerine 2 olculdu)");
        }

        // CIFT-ANLAM KIRICI: bayrak arka plan islerini kapatir ama UYGULAMAYI bozmaz -
        // outbox isleyicisi DI'dan hala cozulebilmeli (testler onu KENDILERI cagiriyor).
        [Fact]
        public void ARKA_PLAN_KAPALI_OLSA_DA_OUTBOX_ISLEYICISI_COZULEBILIR()
        {
            if (Skipped()) return;

            using var scope = _factory!.Services.CreateScope();
            var islemci = scope.ServiceProvider.GetService<Divisima.Bussiness.Outbox.OutboxProcessor>();
            islemci.Should().NotBeNull(
                "bayrak yalnizca ZAMANLAYICIYI kapatir - isleyicinin KENDISI hala kayitli olmali");
        }
    }
}
