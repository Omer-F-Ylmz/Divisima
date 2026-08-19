using Xunit;
using Divisima.DataAccess.Concrete.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: Testcontainers ile GERÇEK SQL Server container'ı ayağa kaldırır, API'yi ona bağlar.
    // Böylece testler gerçek EF davranışını (transaction, concurrency, migration) doğrular - mock değil.
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Test_Password123!")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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
            await db.Database.EnsureCreatedAsync();
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
