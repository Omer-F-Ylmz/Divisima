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

        // Açıklayıcı yorum: Test başlamadan container'ı başlat + şemayı oluştur
        public async Task InitializeAsync()
        {
            await _dbContainer.StartAsync();
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DivisimaDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        public new async Task DisposeAsync() => await _dbContainer.DisposeAsync();
    }
}
