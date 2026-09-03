using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Dtos;
using Divisima.DataAccess.Abstract;
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
    // D5-6/7/8 - TEKIL INDEKSLER, SAYFALAMA CLAMP I, TOPLU OTURUM IPTALI
    //
    // Uc ayri koruma tek sinifta toplandi; ucu de "sessizce bozulursa kimse fark etmez" tipinde:
    //   - WishlistItem (customer_id, product_id) UNIQUE  (filtresiz - hard delete deseni)
    //   - CartItem (cart_id, product_id, size) UNIQUE WHERE is_active = 1  (soft delete deseni)
    //   - EfEntityRepositoryBase.GetPagedAsync: page >= 1, size 1..100
    //   - AdminCustomerManager.SetActive: askiya alinca TUM oturumlar dusmeli
    [Trait("Category", "Sql")]
    public class IndexPagingAndSessionTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaIndexPagingTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static string ConnStr
        {
            get
            {
                var baseConn = string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn;
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = TestDbAdi.Cozumle(DbName) }.ConnectionString;
            }
        }

        private sealed class IndexFactory : WebApplicationFactory<Program>
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

        private IndexFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new IndexFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak indeks/sayfalama testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        private static async Task<int> NewCustomerAsync()
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Indeks Testi",
                email = $"index-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            await ctx.SaveChangesAsync();
            return c.id;
        }

        private static async Task<int> NewCategoryAsync()
        {
            await using var ctx = NewContext();
            var cat = new Category
            {
                name = "Indeks Kategori",
                slug = $"idx-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();
            return cat.id;
        }

        private static async Task<int> NewProductAsync(int categoryId)
        {
            await using var ctx = NewContext();
            var p = new Product
            {
                name = "Indeks Urun",
                brand = "T",
                category_id = categoryId,
                price = 50m,
                description = "indeks testi urunu",
                color_hex = "#404040",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();
            return p.id;
        }

        // WishlistItem indeksi FILTRESIZ tekil. Yani ayni cift icin ikinci satir hicbir kosulda
        // acilamaz; favoriden cikarma HARD DELETE olmali (soft delete olsaydi musteri urunu bir
        // daha favoriye ekleyemezdi).
        [Fact]
        public async Task Wishlist_AyniMusteriUrun_IkinciKayit_Engellenir_SilinceYenidenEklenebilir()
        {
            if (Skipped()) return;
            var customerId = await NewCustomerAsync();
            var productId = await NewProductAsync(await NewCategoryAsync());

            int ilkId;
            await using (var ctx = NewContext())
            {
                var w = new WishlistItem { customer_id = customerId, product_id = productId, created_at = DateTime.Now };
                ctx.Set<WishlistItem>().Add(w);
                await ctx.SaveChangesAsync();
                ilkId = w.id;
            }

            var ikinciDeneme = async () =>
            {
                await using var ctx = NewContext();
                ctx.Set<WishlistItem>().Add(new WishlistItem
                { customer_id = customerId, product_id = productId, created_at = DateTime.Now });
                await ctx.SaveChangesAsync();
            };
            await ikinciDeneme.Should().ThrowAsync<DbUpdateException>(
                "ayni musteri-urun cifti icin ikinci favori satiri acilamaz");

            // HARD DELETE sonrasi yeniden eklenebilmeli - indeks filtresiz oldugu icin favoriden
            // cikarmanin GERCEKTEN satiri silmesi gerekir.
            await using (var ctx = NewContext())
            {
                ctx.Set<WishlistItem>().Remove(await ctx.Set<WishlistItem>().SingleAsync(x => x.id == ilkId));
                await ctx.SaveChangesAsync();
            }
            await using (var ctx = NewContext())
            {
                ctx.Set<WishlistItem>().Add(new WishlistItem
                { customer_id = customerId, product_id = productId, created_at = DateTime.Now });
                await ctx.SaveChangesAsync();
            }
            await using (var son = NewContext())
            {
                (await son.Set<WishlistItem>().CountAsync(w => w.customer_id == customerId && w.product_id == productId))
                    .Should().Be(1, "silme sonrasi yeniden eklenen tek satir kalmali");
            }
        }

        // CartItem indeksi FILTRELI (is_active = 1). Sepetten cikarma soft delete oldugu icin
        // ayni urun+beden tekrar eklenebilmeli; iki AKTIF satir ise olamaz.
        [Fact]
        public async Task CartItem_FiltreliUnique_IkinciAKTIF_SatiriEngeller_PasifSonrasiSerbest()
        {
            if (Skipped()) return;
            var customerId = await NewCustomerAsync();
            var productId = await NewProductAsync(await NewCategoryAsync());

            int cartId, ilkItemId;
            await using (var ctx = NewContext())
            {
                var cart = new Cart { customer_id = customerId, is_active = true, created_at = DateTime.Now };
                ctx.Set<Cart>().Add(cart);
                await ctx.SaveChangesAsync();
                cartId = cart.id;

                var item = new CartItem
                { cart_id = cartId, product_id = productId, size = "M", quantity = 1, is_active = true, created_at = DateTime.Now };
                ctx.Set<CartItem>().Add(item);
                await ctx.SaveChangesAsync();
                ilkItemId = item.id;
            }

            var ikinciDeneme = async () =>
            {
                await using var ctx = NewContext();
                ctx.Set<CartItem>().Add(new CartItem
                { cart_id = cartId, product_id = productId, size = "M", quantity = 1, is_active = true, created_at = DateTime.Now });
                await ctx.SaveChangesAsync();
            };
            await ikinciDeneme.Should().ThrowAsync<DbUpdateException>(
                "ayni sepet+urun+beden icin ikinci AKTIF satir acilamaz");

            // Farkli BEDEN serbest - indeks uc kolonlu, tek kolonlu degil.
            await using (var ctx = NewContext())
            {
                ctx.Set<CartItem>().Add(new CartItem
                { cart_id = cartId, product_id = productId, size = "L", quantity = 1, is_active = true, created_at = DateTime.Now });
                await ctx.SaveChangesAsync();
            }

            // Soft delete sonrasi ayni beden yeniden eklenebilmeli (filtrenin varlik sebebi).
            await using (var ctx = NewContext())
            {
                var item = await ctx.Set<CartItem>().IgnoreQueryFilters().SingleAsync(x => x.id == ilkItemId);
                item.is_active = false;
                await ctx.SaveChangesAsync();
            }
            await using (var ctx = NewContext())
            {
                ctx.Set<CartItem>().Add(new CartItem
                { cart_id = cartId, product_id = productId, size = "M", quantity = 2, is_active = true, created_at = DateTime.Now });
                await ctx.SaveChangesAsync();
            }

            await using (var son = NewContext())
            {
                (await son.Set<CartItem>().IgnoreQueryFilters().CountAsync(i => i.cart_id == cartId && i.is_active))
                    .Should().Be(2, "aktif satirlar: M (yeni) + L");
                (await son.Set<CartItem>().IgnoreQueryFilters().CountAsync(i => i.cart_id == cartId))
                    .Should().Be(3, "pasif satir korunur");
            }
        }

        // Absurd sayfalama istegi zincirin SONUNDA sinirlanmali: PagingRequestDto setter i clamp
        // ediyor, EfEntityRepositoryBase.GetPagedAsync tekrar clamp ediyor (merkezi savunma).
        // Olculen sey uctan uca sonuc: negatif sayfa SQL hatasi vermiyor, devasa boyut DB yi
        // suurmuyor.
        [Fact]
        public async Task GetPagedAsync_AbsurdSayfaVeBoyut_Clamplenir()
        {
            if (Skipped()) return;
            var categoryId = await NewCategoryAsync();

            await using (var ctx = NewContext())
            {
                for (int i = 0; i < 120; i++)
                    ctx.Products.Add(new Product
                    {
                        name = $"Sayfalama Urun {i}",
                        brand = "T",
                        category_id = categoryId,
                        price = 10m + i,
                        description = "sayfalama testi",
                        color_hex = "#505050",
                        product_type = 0,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                await ctx.SaveChangesAsync();
            }

            var paged = await WithScopeAsync(sp => sp.GetRequiredService<IProductDal>()
                .GetPagedAsync(new PagingRequestDto { page = -5, size = 99999 }));

            paged.Page.Should().Be(1, "negatif sayfa 1 e cekilmeli - negatif OFFSET SQL hatasi verirdi");
            paged.Size.Should().Be(100, "boyut ust sinir 100 e cekilmeli");
            paged.Items.Count.Should().Be(100, "donen satir sayisi clamp ile ayni olmali");
            paged.TotalCount.Should().Be(120, "toplam sayi clamp ten etkilenmemeli");
        }

        // AdminCustomerManager.SetActive yorumu "banlanan kullanici mevcut token iyla devam
        // edemez" diyor. Bu test iddianin DOGRU olan yarisini dogruluyor: oturumlar gercekten
        // dusuyor. Iddianin YANLIS olan yarisi (access token in gecerli kalmasi) D4 te
        // pinlendi - bkz. AuthorizationIdorTests ve rapor.
        [Fact]
        public async Task AdminAskiyaAlma_TumAktifOturumlari_Dusurur()
        {
            if (Skipped()) return;
            var customerId = await NewCustomerAsync();

            await using (var ctx = NewContext())
            {
                for (int i = 0; i < 3; i++)
                    ctx.Set<UserSession>().Add(new UserSession
                    {
                        customer_id = customerId,
                        refresh_token = Guid.NewGuid().ToString("N"),
                        device = $"cihaz-{i}",
                        // GF-1b / F4: fikstur de TEK KAYNAKTAN turer - elle "7" yazilmaz,
                        // yoksa uretim omru degistiginde bu satir SESSIZCE ayrisir.
                        expires_at = DateTime.Now.AddDays(Divisima.Core.Security.Tokens.OturumOmru.RefreshGun),
                        is_active = true,
                        created_at = DateTime.Now
                    });
                await ctx.SaveChangesAsync();
            }

            // POZITIF OLAY: askiya almadan once oturumlar GERCEKTEN acik.
            await using (var ctx = NewContext())
                (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == customerId && s.is_active))
                    .Should().Be(3, "baslangicta uc aktif oturum olmali");

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IAdminCustomerService>()
                .SetActive(new AdminCustomerStatusDto { customer_id = customerId, is_active = false }));
            r.Item2.Success.Should().BeTrue($"askiya alma basarili olmali: {r.Item2.Message}");

            await using (var ctx = NewContext())
            {
                (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == customerId && s.is_active))
                    .Should().Be(0, "askiya alinca TUM oturumlar dusmeli");
                (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == customerId))
                    .Should().Be(3, "oturum satirlari silinmez, yalniz pasiflesir");
                (await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.id == customerId))
                    .is_active.Should().BeFalse("musteri pasiflesmeli");
            }
        }
    }
}
