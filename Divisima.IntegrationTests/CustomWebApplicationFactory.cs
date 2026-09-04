using Divisima.DataAccess.Concrete.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Testcontainers ile GERÇEK SQL Server container'ı ayağa kaldırır, API'yi ona bağlar.
    // Böylece testler gerçek EF davranışını (transaction, concurrency, migration) doğrular - mock değil.
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // GF-4/K3: Testcontainers 3.10.0 -> 4.14.0 ile parametresiz MsSqlBuilder() kurucusu
        // kullanim disi kaldi (CS0618) ve imaj artik KURUCUYA verilir; .WithImage(...) cagrisi
        // bu yuzden kalkti. Imza kaynaktan okundu (paketin XML dokumani):
        // "M:Testcontainers.MsSql.MsSqlBuilder.#ctor(System.String)".
        // GF-4/K5 (Y6): tag + digest pini - gerekce ve dort kopyanin listesi ci.yml'da.
        // Testcontainers 4.14.0'in digest'li referansi DOGRU ayristirdigi CALISTIRILARAK
        // olculdu (DockerImage: Repository=mssql/server, Tag=2022-latest, Digest=sha256:0730f368...,
        // FullName girdiyle BIREBIR) - yerelde Docker olmadigi icin suit bunu kosamaz.
        private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest@sha256:0730f3689a6dcc33beaf8f466376ac056d7483a2272dcbd3bcc36d3a6df05437")
            .WithPassword("Test_Password123!")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestHostConfig.Apply(builder);
            // AUTH RATE LIMIT - test host'unda yukseltilir.
            // Sebep: bu siniftaki eszamanlilik testi KASITLI olarak 8 AYRI musteri yaratiyor
            // (gercek oversell senaryosu farkli alicilarin yarismasidir). TestAuthHelper musteri
            // basina 3 auth istegi atiyor; test sunucusunda RemoteIpAddress null oldugu icin
            // hepsi AYNI partition'a duser ve uretim limiti (10/dk) test KURULUMUNU cokertirdi -
            // nitekim cokertti: OrderEndpointTests dort run boyunca "HTTP 429 TooManyRequests"
            // ile kirmiziydi ve sebep genisletilmis annotation deseni sayesinde gorulebildi.
            // Limitin KENDISI AuthRateLimitPinTests'te URETIM VARSAYILANIYLA pinli kalir;
            // burada yukseltmek o sozlesmeyi zayiflatmaz, yalnizca bu sinifi limitten ayirir.
            builder.UseSetting("RateLimit:AuthPermitLimit", "1000");

            builder.ConfigureServices(services =>
            {
                // Açıklayıcı yorum: Gerçek DbContext'i test container'ının connection string'iyle değiştir
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<DivisimaDbContext>(options =>
                    options.UseSqlServer(_dbContainer.GetConnectionString()));
            });
        }

        // A BULGUSU (sinir): StartAsync varsayilan olarak SINIRSIZ bekler - imaj cekme takilirsa
        // ya da container hazir olmazsa test sonsuza kadar asili kalir (CI'da 6 saat slot tutan
        // job'larin sebebi buydu). Artik 5 dakika sinir var ve asilirsa NET mesajla patliyor.
        public async Task InitializeAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await _dbContainer.StartAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "SQL Server test container'i 5 dakika icinde ayaga kalkmadi (imaj cekme veya Docker " +
                    "sorunu olabilir). Sessiz sonsuz bekleme yerine bilerek basarisiz olundu.");
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            await TestDbKurulum.OlusturAsync(db.Database);
        }

        // D BULGUSU (gizleme kaldirildi): eskiden "public new async Task DisposeAsync()" vardi.
        // "new", taban WebApplicationFactory.DisposeAsync()'i GIZLIYORDU; host, TestServer ve arka
        // plan servisleri (Hangfire server, SignalR, Serilog dosya sink'i) hic kapanmiyordu - testler
        // bittikten sonra test host process'inin sonlanmamasinin sebebi buydu.
        // Cozum: IAsyncLifetime.DisposeAsync ACIK arayuz implementasyonu olarak yazildi. Boylece
        // taban sinifin ValueTask donen DisposeAsync'i ile isim cakismasi olmuyor ve GIZLEME kalkiyor.
        // Sira onemli: ONCE taban (host + arka plan servisleri), SONRA container.
        async Task IAsyncLifetime.DisposeAsync()
        {
            await base.DisposeAsync();
            await _dbContainer.DisposeAsync();
        }
    }
}
