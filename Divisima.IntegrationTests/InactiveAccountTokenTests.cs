using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Admin;
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
    // SPRINT 1 - PASIF HESABIN ACCESS TOKEN I
    //
    // ONCEKI DAVRANIS (D4 te pinlenmisti): askiya alinan musterinin access token i calismaya
    // devam ediyordu. user_sessions dusuruluyordu ama JWT stateless oldugu icin token gecerli
    // kaliyor, tek engel Customer uzerindeki global is_active sorgu filtresi oluyordu - yani
    // musteri satirini OKUMAYAN uclar (favori, sepet) pasif hesap icin CALISIYORDU.
    //
    // YENI DAVRANIS: TokenBlacklistMiddleware her kimlikli musteri isteginde hesap durumunu
    // kontrol eder. Her istekte DB'ye gitmemek icin 60 sn TTL li cache var; askiya alma ve
    // silme yollari anahtari DUSURUR, boylece ban TTL beklemeden ANINDA etkili olur.
    // Bu sinif hem reddi hem de invalidate'in aninda calistigini olcer.
    [Trait("Category", "Sql")]
    public class InactiveAccountTokenTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaInactiveTokenTest";
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

        private sealed class InactiveFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                });
            }
        }

        private InactiveFactory? _host;
        private TestAuthHelper.AuthenticatedCustomer? _a;
        private bool _sqlAvailable;

        private TestAuthHelper.AuthenticatedCustomer A => _a!;

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
                _host = new InactiveFactory();
                // TEK musteri: auth policy si 5 istek/dk ve TestAuthHelper musteri basina 3 istek
                // atiyor. Ikinci musteri altinci istegi 429 yapardi.
                _a = await TestAuthHelper.CreateCustomerClientAsync(_host);
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak pasif-token testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_host != null) await _host.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _host!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        private static async Task<int> NewProductAsync()
        {
            await using var ctx = NewContext();
            var cat = new Category
            {
                name = "Pasif Kategori", slug = $"pasif-{Guid.NewGuid():N}",
                is_active = true, created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Pasif Urun", brand = "T", category_id = cat.id, price = 60m,
                description = "pasif hesap testi urunu", color_hex = "#0C0C0C",
                product_type = 0, is_active = true, created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();
            return p.id;
        }

        [Fact]
        public async Task AdminAskiyaAlma_AYNI_TOKENI_ANINDA_Reddeder_OkumaVeYazma_401()
        {
            if (Skipped()) return;
            var oncekiUrun = await NewProductAsync();
            var sonrakiUrun = await NewProductAsync();

            // POZITIF OLAY: askiya almadan ONCE hem okuma hem YAZMA calisiyor.
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.OK, "aktif hesap kendi ozetini gorebilmeli");
            (await A.Client.PostAsync($"/api/Wishlist/toggle?productId={oncekiUrun}", null))
                .IsSuccessStatusCode.Should().BeTrue("aktif hesap yazma yapabilmeli");
            await using (var ctx = NewContext())
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId))
                    .Should().Be(1, "yazma gercekten veritabanina islenmis olmali");

            // GERCEK admin yolu: SetActive(false) - hem oturumlari dusurur hem cache'i invalidate eder.
            var suspend = await WithScopeAsync(sp => sp.GetRequiredService<IAdminCustomerService>()
                .SetActive(new AdminCustomerStatusDto { customer_id = A.CustomerId, is_active = false }));
            suspend.Item2.Success.Should().BeTrue($"askiya alma basarili olmali: {suspend.Item2.Message}");

            // AYNI token, askiya almadan HEMEN sonra. 60 sn TTL beklenmez - invalidate calisiyorsa
            // bir sonraki istek zaten reddedilir.
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "pasif hesabin token i OKUMADA reddedilmeli");

            var yazma = await A.Client.PostAsync($"/api/Wishlist/toggle?productId={sonrakiUrun}", null);
            ((int)yazma.StatusCode).Should().Be(401, "pasif hesabin token i YAZMADA da reddedilmeli");

            // ISLEM GERCEKTEN OLMADI: ikinci urun favorilere yazilmamis.
            await using (var ctx = NewContext())
            {
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId))
                    .Should().Be(1, "askiya alma sonrasi YENI satir yazilmamali");
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId && w.product_id == sonrakiUrun))
                    .Should().Be(0, "reddedilen istek favori eklememeli");
            }
        }

        // CIFT-ANLAM KIRICI: kontrol "herkesi reddediyor" olmasin. Aktif hesap ard arda
        // isteklerde (ilki DB'den, sonrakiler cache'ten) sorunsuz calismali.
        [Fact]
        public async Task AktifHesap_HesapDurumuKontrolunden_Etkilenmez()
        {
            if (Skipped()) return;
            var urun = await NewProductAsync();

            for (int i = 0; i < 3; i++)
                (await A.Client.GetAsync("/api/Account/summary")).StatusCode
                    .Should().Be(HttpStatusCode.OK, $"aktif hesap {i + 1}. istekte de gecmeli (cache yolu dahil)");

            (await A.Client.PostAsync($"/api/Wishlist/toggle?productId={urun}", null))
                .IsSuccessStatusCode.Should().BeTrue("aktif hesap yazma yapabilmeli");

            await using var ctx = NewContext();
            (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId))
                .Should().Be(1, "aktif hesabin yazmasi gercekten islenmis olmali");
        }

        // Middleware yalniz kimlikli MUSTERI isteklerinde devreye girer: anonim uclar ve
        // saglik kontrolu etkilenmemeli (jti/claim yoksa kontrol hic calismaz).
        [Fact]
        public async Task AnonimUclar_Ve_SaglikKontrolu_Etkilenmez()
        {
            if (Skipped()) return;
            var anon = _host!.CreateClient();

            (await anon.GetAsync("/health/live")).StatusCode
                .Should().Be(HttpStatusCode.OK, "saglik kontrolu kimlik istemez");

            // Musteri askiya alinsa bile anonim yollar degismez.
            var suspend = await WithScopeAsync(sp => sp.GetRequiredService<IAdminCustomerService>()
                .SetActive(new AdminCustomerStatusDto { customer_id = A.CustomerId, is_active = false }));
            suspend.Item2.Success.Should().BeTrue();

            (await anon.GetAsync("/health/live")).StatusCode
                .Should().Be(HttpStatusCode.OK, "askiya alma anonim uclari etkilememeli");

            // Token TASIMAYAN istek 401 alir ama bu kimlik yoklugundandir, hesap durumundan degil -
            // yani kontrol anonim akisi bozmuyor, sadece kimlikli musteriye bakiyor.
            (await anon.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "kimliksiz istek zaten 401 olmali");
        }
    }
}
