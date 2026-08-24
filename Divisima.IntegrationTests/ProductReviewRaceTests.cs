using Divisima.Bussiness.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.ProductReview;
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
    // D5-1 - CIFT YORUM YARISI
    //
    // ProductReviewManager.Add check-then-act yapiyor: once "bu musterinin bu urune aktif yorumu
    // var mi" diye SORUYOR, sonra EKLIYOR. Arada kilit YOK. Eszamanli cagrilarda tek gercek
    // koruma su filtreli tekil indeks:
    //     ProductReview -> (customer_id, product_id) UNIQUE, HasFilter("[is_active] = 1")
    // Bu indeks migration surecinde SONRADAN eklenmisti; asil sinavi bu test.
    //
    // Add metodu artik DTO yu ELLE entity ye ceviriyor (AutoMapper eslemesi yoktu ve her cagri
    // 500 doner du) ve indeks ihlalini dogrulayip 409 e ceviriyor. Indeks ayrica ikinci testte
    // veritabani seviyesinde dogrudan sinaniyor.
    [Trait("Category", "Sql")]
    public class ProductReviewRaceTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaReviewRaceTest";
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

        private sealed class ReviewFactory : WebApplicationFactory<Program>
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

        private ReviewFactory? _factory;
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
                _factory = new ReviewFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak yorum yarisi testi ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private static async Task<(int customerId, int productId)> SeedAsync()
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "Yorum Testi",
                email = $"review-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            var cat = new Category
            {
                name = "Yorum Kategori",
                slug = $"yorum-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "Yorum Urun",
                brand = "T",
                category_id = cat.id,
                price = 100m,
                description = "yorum testi urunu",
                color_hex = "#0A0A0A",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();
            return (c.id, p.id);
        }

        [Fact]
        public async Task AyniMusteriUrune_SekizParalelYorum_TAM_BIR_AKTIF_Kaybedenler409()
        {
            if (Skipped()) return;
            const int callers = 8;
            var (customerId, productId) = await SeedAsync();

            // Her cagri KENDI DI scope unu (dolayisiyla kendi DbContext ini) alir - gercek
            // eszamanli istekler gibi. Istisna firlatan cagrilar da sayilir, yutulmaz.
            var outcomes = await Task.WhenAll(Enumerable.Range(0, callers).Select(async i =>
            {
                try
                {
                    var r = await WithScopeAsync(sp => sp.GetRequiredService<IProductReviewService>()
                        .Add(new ProductReviewAddRequestDto
                        {
                            customer_id = customerId,
                            product_id = productId,
                            rating = 5,
                            comment = $"Paralel yorum {i}"
                        }));
                    return ((int)r.Item1, (string?)null);
                }
                catch (Exception ex)
                {
                    return (0, (string?)ex.GetType().Name);
                }
            }));

            var created = outcomes.Count(o => o.Item1 == 201);
            var conflict = outcomes.Count(o => o.Item1 == 409);
            var threw = outcomes.Count(o => o.Item2 != null);

            // CIFT-ANLAM KIRICI: her assert mesaji NE OLDUGUNU soylesin - yalniz sayi degil,
            // donen kodlarin ve istisna tiplerinin dokumu.
            var dokum = string.Join(" | ", outcomes.Select(o => o.Item2 ?? o.Item1.ToString()));

            // ASIL SINAV: 8 paralel cagridan sonra veritabaninda TAM 1 aktif yorum.
            // Filtreli tekil indeks tutmazsa buraya birden fazla satir duser.
            await using (var ctx = NewContext())
            {
                (await ctx.Set<ProductReview>().IgnoreQueryFilters().CountAsync(r =>
                    r.customer_id == customerId && r.product_id == productId && r.is_active))
                    .Should().Be(1, $"filtreli tekil indeks yalniz BIR aktif yorum birakmali. Cagri sonuclari: {dokum}");
            }

            // VAKUM KIRICI: bir cagri GERCEKTEN yorum olusturmus olmali.
            created.Should().Be(1, $"tam bir cagri 201 Created almali. Cagri sonuclari: {dokum}");

            // KAYBEDENLER TEMIZ 409 ALIR - ne 500, ne istisna. Add icindeki DbUpdateException
            // yakalamasi (yaris dogrulanip 409'a cevrilmesi) tam olarak bunu saglar.
            conflict.Should().Be(callers - 1, $"kaybeden yedi cagri 409 almali. Cagri sonuclari: {dokum}");
            threw.Should().Be(0, $"hicbir cagri istisna ile dusmemeli. Cagri sonuclari: {dokum}");
        }

        // Yukaridaki uretim hatasi yuzunden servis yolundan indekse HIC ulasilamiyor. Indeksin
        // kendisi yine de sinanmali: veritabani seviyesinde dogrudan yazarak filtreli tekil
        // indeksin (customer_id, product_id) WHERE is_active = 1 semantigini kanitla.
        [Fact]
        public async Task FiltreliTekilIndeks_IkinciAKTIF_Yorumu_Engeller_PasifOlaniEngellemez()
        {
            if (Skipped()) return;
            var (customerId, productId) = await SeedAsync();

            static ProductReview Yorum(int customerId, int productId, string comment, bool aktif) => new()
            {
                customer_id = customerId,
                product_id = productId,
                rating = 5,
                comment = comment,
                is_verified_purchase = false,
                helpful_count = 0,
                review_status = 0,
                is_active = aktif,
                created_at = DateTime.Now
            };

            // POZITIF OLAY: ilk aktif yorum sorunsuz yaziliyor.
            int ilkId;
            await using (var ctx = NewContext())
            {
                var ilk = Yorum(customerId, productId, "Ilk yorum", true);
                ctx.Set<ProductReview>().Add(ilk);
                await ctx.SaveChangesAsync();
                ilkId = ilk.id;
            }

            // IKINCI aktif yorum AYNI (musteri, urun) icin -> indeks ihlali.
            var ikinciDeneme = async () =>
            {
                await using var ctx = NewContext();
                ctx.Set<ProductReview>().Add(Yorum(customerId, productId, "Ikinci yorum", true));
                await ctx.SaveChangesAsync();
            };
            await ikinciDeneme.Should().ThrowAsync<DbUpdateException>(
                "filtreli tekil indeks ikinci AKTIF yorumu reddetmeli");

            // FILTRENIN ANLAMI: ilk yorum pasiflesince ayni cift icin yeni aktif yorum SERBEST.
            // Indeks kosulsuz tekil olsaydi burasi da patlardi - filtrenin varligi boylece kanitlanir.
            await using (var ctx = NewContext())
            {
                var ilk = await ctx.Set<ProductReview>().IgnoreQueryFilters().SingleAsync(r => r.id == ilkId);
                ilk.is_active = false;
                await ctx.SaveChangesAsync();
            }
            await using (var ctx = NewContext())
            {
                ctx.Set<ProductReview>().Add(Yorum(customerId, productId, "Pasif sonrasi yeni yorum", true));
                await ctx.SaveChangesAsync();
            }

            await using (var son = NewContext())
            {
                (await son.Set<ProductReview>().IgnoreQueryFilters()
                    .CountAsync(r => r.customer_id == customerId && r.product_id == productId && r.is_active))
                    .Should().Be(1, "her an yalniz TEK aktif yorum bulunabilir");
                (await son.Set<ProductReview>().IgnoreQueryFilters()
                    .CountAsync(r => r.customer_id == customerId && r.product_id == productId))
                    .Should().Be(2, "pasif satir korunur - toplam iki satir olur");
            }
        }
    }
}
