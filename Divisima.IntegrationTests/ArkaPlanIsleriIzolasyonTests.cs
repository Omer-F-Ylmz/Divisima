using System.Linq;
using Divisima.DataAccess.Concrete.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    //
    // ─────────────────────────────────────────────────────────────────────────────────────
    // BU SINIF VERITABANI OLUSTURMAZ - ve bu bilincli bir DUZELTMEDIR (CI kirmizisi 10d794d).
    //
    // Ilk yazimda sinif, depodaki diger SQL sinifllarinin kalibini KOPYALAYIP kendi
    // veritabanini `EnsureDeleted` + `EnsureCreated` ile kuruyordu. OLCULDU: bu sinifin IKI
    // pini de YALNIZCA DI kayitlarina bakiyor - tek bir sorgu bile calistirmiyor. Yani
    // olusturulan veritabani HIC KULLANILMIYORDU.
    //
    // Bedeli SESSIZ DEGILDI: depoda 46 test sinifi kendi veritabanini kuruyor ve SQL Server
    // `CREATE DATABASE`/`DROP DATABASE` islemlerini `model` veritabani uzerinden SERILESTIRIR.
    // 47. katilimci eklenince Security CI'da bes AYRI sinif ayni hatayla dustu:
    //
    //   SqlException : Could not obtain exclusive lock on database 'model'. Retry the operation later.
    //
    // Kirilan siniflar BU SINIF DEGILDI (InvoiceLineVatTests, InactiveAccountTokenTests,
    // ContentSeedAndSanitizeTests, LaunchFixMailZinciriTests, NotificationSubscriptionTests) -
    // yani zarar, gereksiz DDL yukunun BASKALARINI dusurmesiydi.
    //
    // Cozum: veritabani HIC olusturulmuyor (sifir DDL). Host yine de IZOLE bir veritabani ADINA
    // yonlendiriliyor - amac onu kullanmak degil, uygulamanin acilisindaki `ContentSeeder`in
    // GELISTIRICININ veritabanina yazmasini ENGELLEMEK (CLAUDE.md: "TEST, URUNUN GERCEK
    // KAYNAKLARINA DOKUNMAZ"). Var olmayan veritabani acilis tohumlamasini dusurur; `Program.cs`
    // bunu ACIKCA yakalayip loglar ve uygulama DEVAM EDER ("Tohumlama hatasi uygulamayi
    // DURDURMAZ") - yani host saglikli kalkiyor ve pinlerin olctugu DI kayitlari eksiksiz.
    // `[Trait("Category","Sql")]` de bu yuzden YOK: sinif SQL GEREKTIRMIYOR.
    public class ArkaPlanIsleriIzolasyonTests : IAsyncLifetime
    {
        // Kasitli olarak VAR OLMAYAN bir veritabani adi: host'un gercek bir kaynaga
        // (gelistirici DivisimaDb'si) baglanmasini engeller, ama kurulmasi da gerekmez.
        private const string KullanilmayanDb = "DivisimaArkaPlanIzolasyon_KULLANILMAZ";

        private sealed class IzolasyonFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(
                        $"Server=(localdb)\\MSSQLLocalDB;Initial Catalog={KullanilmayanDb};"
                      + "Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3;"));
                });
            }
        }

        private IzolasyonFactory? _factory;

        public Task InitializeAsync()
        {
            _factory = new IzolasyonFactory();
            _ = _factory.Services;   // host'u GERCEKTEN kur - DI kayitlari ancak boyle olculur
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
        }

        [Fact]
        public void TEST_HOSTUNDA_HANGFIRE_ARKA_PLAN_SUNUCUSU_KOSMAZ()
        {
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
            using var scope = _factory!.Services.CreateScope();
            var islemci = scope.ServiceProvider.GetService<Divisima.Bussiness.Outbox.OutboxProcessor>();
            islemci.Should().NotBeNull(
                "bayrak yalnizca ZAMANLAYICIYI kapatir - isleyicinin KENDISI hala kayitli olmali");
        }
    }
}
