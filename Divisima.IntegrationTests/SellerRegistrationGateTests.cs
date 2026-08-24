using System.Net;
using System.Net.Http.Json;
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
    // SPRINT 4 MINI EK - SATICI BASVURUSU KAPALI KAPI
    //
    // /api/seller/auth/register ucu [AllowAnonymous] idi: internetten HERKES satici hesabi
    // acabiliyordu. Launch tek saticiyla yapilacagi icin acik durmasinin faydasi yok,
    // saldiri yuzeyi var. Artik Seller:RegistrationEnabled bayragi arkasinda ve VARSAYILAN
    // KAPALI. Marketplace acildiginda bayrak true yapilir - kod yolu aynen korunuyor.
    [Trait("Category", "Sql")]
    public class SellerRegistrationGateTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaSellerGateTest";
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

        // Bayragi host duzeyinde ayarlanabilen fabrika - iki yon de ayni sinifta surulur.
        private sealed class SellerFactory : WebApplicationFactory<Program>
        {
            private readonly bool _registrationEnabled;
            public SellerFactory(bool registrationEnabled) => _registrationEnabled = registrationEnabled;

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Seller:RegistrationEnabled", _registrationEnabled ? "true" : "false");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
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
                await using var ctx = NewContext();
                await TestDbKurulum.SilAsync(ctx.Database);
                await TestDbKurulum.OlusturAsync(ctx.Database);
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak satici kapisi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static object Basvuru(string email) => new
        {
            business_name = "Test Butik",
            email,
            phone = "5550000000",
            password = "TestPass123",
            tax_number = "1234567890"
        };

        [Fact]
        public async Task Bayrak_KAPALI_Register_403_Doner_VeSaticiOLUSMAZ()
        {
            if (Skipped()) return;
            await using var factory = new SellerFactory(registrationEnabled: false);
            var client = factory.CreateClient();
            var email = $"satici-{Guid.NewGuid():N}@divisima.test";

            var resp = await client.PostAsJsonAsync("/api/seller/auth/register", Basvuru(email));
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                $"bayrak kapaliyken basvuru reddedilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await resp.Content.ReadAsStringAsync())}");

            // ISLEM GERCEKTEN OLMADI - 403 kozmetik degil.
            await using var ctx = NewContext();
            (await ctx.Set<Seller>().IgnoreQueryFilters().CountAsync(s => s.email == email))
                .Should().Be(0, "reddedilen basvuru satici satiri OLUSTURMAMALI");
        }

        // CIFT-ANLAM KIRICI: 403 "uc bozuk" oldugu icin degil, BAYRAK kapali oldugu icin geliyor.
        // Bayrak acilinca ayni istek eski davranisi gosterir ve satici kaydi olusur.
        [Fact]
        public async Task Bayrak_ACIK_Register_EskiDavranis_SaticiOLUSUR()
        {
            if (Skipped()) return;
            await using var factory = new SellerFactory(registrationEnabled: true);
            var client = factory.CreateClient();
            var email = $"satici-{Guid.NewGuid():N}@divisima.test";

            var resp = await client.PostAsJsonAsync("/api/seller/auth/register", Basvuru(email));
            resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
                $"bayrak acikken kapiya takilmamali: {(int)resp.StatusCode} {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await resp.Content.ReadAsStringAsync())}");
            resp.IsSuccessStatusCode.Should().BeTrue(
                $"basvuru kabul edilmeli: {(int)resp.StatusCode} {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await resp.Content.ReadAsStringAsync())}");

            await using var ctx = NewContext();
            (await ctx.Set<Seller>().IgnoreQueryFilters().CountAsync(s => s.email == email))
                .Should().Be(1, "bayrak acikken satici satiri OLUSMALI");
        }
    }
}
