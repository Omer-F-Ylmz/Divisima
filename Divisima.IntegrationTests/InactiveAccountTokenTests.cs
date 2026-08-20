using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Admin;
using Divisima.Entity.Entities;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Results;
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

        // SPRINT 2 - REAKTIVASYON.
        // ONCEKI HATA: SetActive musteriyi normal GetAsync ile ariyordu; Customer uzerinde global
        // is_active filtresi oldugu icin PASIF musteri bulunamiyor ve SetActive(true) her zaman
        // 404 donuyordu - yani askiya alma TEK YONLUYDU, banlanan hesap bir daha acilamiyordu.
        // Simdi admin yolu IgnoreQueryFilters kullaniyor. Ayrica her SetActive cagrisinda hesap
        // durumu cache anahtari dusuruldugu icin ayni access token ANINDA yeniden gecerli olur.
        [Fact]
        public async Task Reaktivasyon_AYNI_TOKENI_ANINDA_Yeniden_Kabul_Eder()
        {
            if (Skipped()) return;

            // POZITIF OLAY: baslangicta calisiyor.
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode.Should().Be(HttpStatusCode.OK);

            var suspend = await WithScopeAsync(sp => sp.GetRequiredService<IAdminCustomerService>()
                .SetActive(new AdminCustomerStatusDto { customer_id = A.CustomerId, is_active = false }));
            suspend.Item2.Success.Should().BeTrue($"askiya alma basarili olmali: {suspend.Item2.Message}");
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "askiya alinca token reddedilmeli");

            // ASIL SINAV: pasif musteri GERI ACILABILIYOR mu (eskiden 404 donuyordu).
            var reactivate = await WithScopeAsync(sp => sp.GetRequiredService<IAdminCustomerService>()
                .SetActive(new AdminCustomerStatusDto { customer_id = A.CustomerId, is_active = true }));
            reactivate.Item2.Success.Should().BeTrue(
                $"pasif musteri GERI ACILABILMELI - eskiden CustomerNotFound donuyordu: {reactivate.Item2.Message}");

            await using (var ctx = NewContext())
                (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == A.CustomerId))
                    .is_active.Should().BeTrue("musteri veritabaninda yeniden aktif olmali");

            // AYNI access token, TTL beklemeden yeniden gecerli (cache invalidate kaniti).
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.OK, "reaktivasyon sonrasi AYNI token ANINDA kabul edilmeli");

            // Musteri normal akista: yazma da calisiyor.
            var urun = await NewProductAsync();
            (await A.Client.PostAsync($"/api/Wishlist/toggle?productId={urun}", null))
                .IsSuccessStatusCode.Should().BeTrue("reaktive musteri yazma yapabilmeli");
            await using (var ctx = NewContext())
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId))
                    .Should().Be(1, "yazma gercekten islenmis olmali");
        }

        // ONCEKI HATA: ListCustomers de normal GetListAsync kullaniyordu; global filtre yuzunden
        // "is_active = false" filtresi HER ZAMAN bos liste donuyordu - admin askiya aldigi
        // musterileri hic goremiyordu. VAKUM KIRICI: bir pasif + bir aktif musteri tohumlanir ve
        // iki filtrenin sayilari AYRI AYRI dogrulanir (tek filtre bakilsa "hepsini donduruyor" da
        // yesil kalirdi).
        [Fact]
        public async Task AdminListesi_PasifFiltresi_PasifMusteriyi_Donduruyor()
        {
            if (Skipped()) return;

            var pasifEmail = $"pasif-{Guid.NewGuid():N}@divisima.test";
            var aktifEmail = $"aktif-{Guid.NewGuid():N}@divisima.test";
            await using (var ctx = NewContext())
            {
                ctx.Set<Customer>().Add(new Customer
                {
                    name = "Pasif Musteri", email = pasifEmail, phone = "5550000001",
                    password_hash = new byte[] { 1 }, password_salt = new byte[] { 2 },
                    is_active = false, email_verified = true, created_at = DateTime.Now
                });
                ctx.Set<Customer>().Add(new Customer
                {
                    name = "Aktif Musteri", email = aktifEmail, phone = "5550000002",
                    password_hash = new byte[] { 1 }, password_salt = new byte[] { 2 },
                    is_active = true, email_verified = true, created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            // Govdeyi metin olarak degil TIPLI okuyoruz: Result'i JSON'a cevirmek Data alanini
            // dusuruyor (bildirilen tip Result, calisma zamani SuccessDataResult<...>).
            static async Task<List<string>> EpostalariGetirAsync(
                Func<Func<IServiceProvider, Task<(HttpStatusCode, Result)>>, Task<(HttpStatusCode, Result)>> scope,
                bool aktifMi)
            {
                var r = await scope(sp => sp.GetRequiredService<IAdminCustomerService>()
                    .ListCustomers(new AdminCustomerFilterDto { is_active = aktifMi, page = 1, page_size = 100 }));
                r.Item2.Success.Should().BeTrue($"listeleme basarili olmali: {r.Item2.Message}");
                var data = r.Item2.Should().BeOfType<SuccessDataResult<PagedResult<AdminCustomerListDto>>>().Subject.Data;
                return data.Items.Select(i => i.email).ToList();
            }

            var pasifler = await EpostalariGetirAsync(f => WithScopeAsync(f), aktifMi: false);
            pasifler.Should().Contain(pasifEmail, "pasif filtresi pasif musteriyi DONDURMELI");
            pasifler.Should().NotContain(aktifEmail, "pasif filtresi aktif musteriyi dondurMEMELI");

            var aktifler = await EpostalariGetirAsync(f => WithScopeAsync(f), aktifMi: true);
            aktifler.Should().Contain(aktifEmail, "aktif filtresi aktif musteriyi dondurmeli");
            aktifler.Should().NotContain(pasifEmail, "aktif filtresi pasif musteriyi dondurMEMELI");
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
