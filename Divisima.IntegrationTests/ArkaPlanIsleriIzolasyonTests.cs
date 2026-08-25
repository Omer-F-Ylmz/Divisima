using System;
using System.Collections.Generic;
using System.IO;
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

        // FLAKE-FIX: kayitlar AKTIVE EDILMEDEN gozlenir. `GetService<IGlobalConfiguration>()`
        // cagirmak, kaydin VAR OLDUGU durumda tam da olcmek istedigimiz SQL baglantisini
        // acardi - yani pin, olctugu zarari KENDI URETIRDI. Bu yuzden `IServiceCollection`
        // Program.cs'in kayitlarindan SONRA yakalanir ve yalnizca TIP ADLARINA bakilir.
        private static readonly List<string> YakalananKayitlar = new();

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

                    lock (YakalananKayitlar)
                    {
                        YakalananKayitlar.Clear();
                        YakalananKayitlar.AddRange(services
                            .Select(x => x.ServiceType.FullName ?? x.ServiceType.Name));
                    }
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

        // ══ FLAKE-FIX / p1 - BAYRAK FALSE ISE HANGFIRE DEPOLAMASI HIC KURULMAZ ═════════════
        //
        // OLCULEN ONCE-DURUM: `AddHangfireServer()` bayrakla kapaliydi AMA
        // `AddHangfire(... UseSqlServerStorage ...)` KOSULSUZDU - yani test host'u Hangfire
        // icin SQL'e YINE BAGLANIYORDU. Adi olan flake'in kok sebebi buydu:
        //   Autofac ... activating λ:Hangfire.IGlobalConfiguration
        //   ---- Timeout expired ... max pool size was reached
        //
        // BU PIN DETERMINISTIKTIR: kayitlar AKTIVE EDILMEDEN, `IServiceCollection` uzerinden
        // TIP ADIYLA gozlenir. `GetService<IGlobalConfiguration>()` cagirmak, kayit VARSA
        // olcmek istedigimiz SQL baglantisini KENDI ACARDI.
        [Fact]
        public void BAYRAK_FALSE_ISE_HANGFIRE_DI_KAYDI_HIC_YOK_DEPOLAMA_KURULMAZ()
        {
            List<string> kayitlar;
            lock (YakalananKayitlar) kayitlar = YakalananKayitlar.ToList();

            // VAKUM KIRICI: yakalama GERCEKTEN calismis olmali. Bos bir listede "Hangfire yok"
            // iddiasi bedavaya dogru olurdu.
            kayitlar.Should().HaveCountGreaterThan(100,
                "Program.cs'in DI kayitlari yakalanmis olmali - yoksa bu tarama vakuma duser");

            var hangfireKayitlari = kayitlar
                .Where(a => a.StartsWith("Hangfire.", StringComparison.Ordinal))
                .Distinct()
                .ToList();

            hangfireKayitlari.Should().BeEmpty(
                "bayrak false iken Hangfire'a ait HICBIR DI kaydi olmamali - `IGlobalConfiguration` "
              + "aktive EDILEMEZSE havuz tukenmesi YAPISAL OLARAK olusamaz");
        }

        // ══ FLAKE-FIX / p2 - HICBIR HANGFIRE CAGRISI BAYRAGIN DISINDA KALMAZ ══════════════
        //
        // KAYNAK SOZLESMESI PINI (durust etiket): bayrak TRUE host'u bu suitte AYAGA
        // KALDIRILAMAZ - o host Hangfire depolamasini kurar, SQL'e baglanir ve GELISTIRICININ
        // veritabanina recurring job tanimi yazar; yani pinin KENDISI, kaldirmaya calistigimiz
        // zarari uretirdi. Onun yerine YAPISAL kural pinlenir: Hangfire'a dokunan HER cagri
        // `if (arkaPlanIsleri)` blogunun ICINDE olmali. Yarin eklenecek bir cagri da yakalanir.
        //
        // Bayrak TRUE davranisinin DAVRANIS kaniti raporda: uygulama varsayilan bayrakla
        // ayaga kaldirildi ve `/hangfire` yanit verdi (depolama kurulmasaydi acilis PATLARDI).
        [Fact]
        public void HICBIR_HANGFIRE_CAGRISI_BAYRAGIN_DISINDA_KALMAZ()
        {
            var program = ProgramKaynagi();

            // Yorum satirlari AYIKLANIR: bu dosya Hangfire'i ONLARCA KEZ yorumda anıyor
            // (olculen zarar kayitlari). Kaynak tarayan bir pin, kendi belgeledigi kalibi da
            // tarar - bu tuzagin bedeli depoda iki kez odendi.
            var kod = string.Join("\n", program
                .Split('\n')
                .Select(s => s.TrimEnd('\r'))
                .Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)));

            var bloklar = BayrakBloklari(kod);
            bloklar.Should().HaveCountGreaterThanOrEqualTo(2,
                "en az iki `if (arkaPlanIsleri)` blogu olmali (kayit + boru hatti); yoksa tarama vakuma duser");

            var aranan = new[]
            {
                "AddHangfire(", "AddHangfireServer(", "UseHangfireDashboard(", "RecurringJob.AddOrUpdate"
            };

            foreach (var desen in aranan)
            {
                var yerler = TumIndeksler(kod, desen).ToList();
                yerler.Should().NotBeEmpty($"'{desen}' kaynakta GERCEKTEN bulunmali - yoksa assert bedava dogru olur");

                foreach (var yer in yerler)
                {
                    bloklar.Any(b => yer > b.Bas && yer < b.Son).Should().BeTrue(
                        $"'{desen}' cagrisi `if (arkaPlanIsleri)` blogunun ICINDE olmali - disarida kalan "
                      + "bir Hangfire cagrisi, bayrak false iken SQL'e baglanir ve flake'i geri getirir");
                }
            }
        }

        private static string ProgramKaynagi()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: docker-compose.yml iceren ust dizin yok. "
                  + "Sessiz skip YOK - bu pin kaynagi okuyamadan yesil kalamaz.");

            var yol = Path.Combine(d.FullName, "Divisima.API", "Program.cs");
            File.Exists(yol).Should().BeTrue("Program.cs bulunmali");
            return File.ReadAllText(yol);
        }

        private static IEnumerable<int> TumIndeksler(string metin, string desen)
        {
            var i = metin.IndexOf(desen, StringComparison.Ordinal);
            while (i >= 0)
            {
                yield return i;
                i = metin.IndexOf(desen, i + 1, StringComparison.Ordinal);
            }
        }

        // `if (arkaPlanIsleri)` bloklarinin [bas, son] araliklari. Tek satirlik govde
        // (susli parantezsiz) KABUL EDILMEZ - o bicimde ikinci bir cagri eklemek sessizce
        // blogun DISINDA kalirdi.
        private static List<(int Bas, int Son)> BayrakBloklari(string kod)
        {
            var sonuc = new List<(int, int)>();
            foreach (var i in TumIndeksler(kod, "if (arkaPlanIsleri)"))
            {
                var acilis = kod.IndexOf('{', i);
                if (acilis < 0) continue;
                // Aradaki metin yalnizca bosluk/yeni satir olmali - aksi halde bu `if` tek
                // satirlik bir govdeye sahiptir ve blok DEGILDIR.
                if (kod.Substring(i + "if (arkaPlanIsleri)".Length, acilis - i - "if (arkaPlanIsleri)".Length)
                       .Any(c => !char.IsWhiteSpace(c)))
                    continue;

                var derinlik = 0;
                for (var j = acilis; j < kod.Length; j++)
                {
                    if (kod[j] == '{') derinlik++;
                    else if (kod[j] == '}')
                    {
                        derinlik--;
                        if (derinlik == 0) { sonuc.Add((acilis, j)); break; }
                    }
                }
            }

            return sonuc;
        }
    }
}
