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
    // E1 - STOREFRONT KATALOG SOZLESMESI
    //
    // Storefront (anonim ziyaretci) katalogunu bu uclardan cekiyor. E1'de uc sozlesme
    // hatasi OLCULDU ve istemci tarafinda duzeltildi; burada sozlesmenin KENDISI pinlenir
    // ki backend sessizce degistiginde vitrin bozulmadan ONCE kirmizi gorelim.
    //
    // Ikisi MEVCUT DAVRANIS pini (SUPHELI - duzeltme karari kullanicinin):
    //   - filter yolu category_name / total_stock / sizes DOLDURMUYORDU (SPRINT 8 MADDE 5'te DUZELTILDI)
    //   - getlist ADMIN ister (storefront kullanamaz)
    [Trait("Category", "Sql")]
    public class StorefrontCatalogContractTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaStorefrontContractTest";
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

        private sealed class StorefrontFactory : WebApplicationFactory<Program>
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

        private StorefrontFactory? _factory;
        private bool _sqlAvailable;
        private int _productId;
        private int _categoryId;

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
                _factory = new StorefrontFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
                await SeedAsync();
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak storefront sozlesme testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task SeedAsync()
        {
            await using var ctx = NewContext();
            var cat = new Category
            {
                name = "Vitrin Kategori",
                slug = $"vitrin-{Guid.NewGuid():N}",
                vat_rate = 0.10m,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();
            _categoryId = cat.id;

            var p = new Product
            {
                name = "Vitrin Urun",
                brand = "Divisima",
                category_id = cat.id,
                price = 250m,
                description = "vitrin sozlesme testi",
                color_hex = "#101010",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();
            _productId = p.id;

            ctx.ProductStocks.Add(new ProductStock
            {
                product_id = p.id,
                size = "M",
                stock_quantity = 7,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
        }

        private static object FullFilter(int page = 1, int size = 20) => new
        {
            page,
            size,
            sort = "new",
            sizes = Array.Empty<string>(),
            colors = Array.Empty<string>()
        };

        // ── 1) ANONIM KATALOG YOLU: filter ACIK, getlist ADMIN ───────────────────────────
        // Kopru eskiden getlist cagiriyordu; anonim ziyaretci 401 aliyor (kimliksiz) ve storefront
        // sessizce MOCK veriye dusuyordu - yani vitrin hicbir zaman gercek urun gostermedi.
        [Fact]
        public async Task AnonimKatalog_FilterACIK_GetListADMIN_ISTER()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var filter = await anon.PostAsJsonAsync("/api/product/filter", FullFilter());
            filter.StatusCode.Should().Be(HttpStatusCode.OK,
                $"storefront katalogu ANONIM erisilebilir olmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await filter.Content.ReadAsStringAsync())}");

            var getlist = await anon.GetAsync("/api/product/getlist");
            getlist.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "MEVCUT DAVRANIS: getlist admin ister - storefront bu ucu KULLANAMAZ");
        }

        // ── 2) FILTER'IN ZORUNLU ALANLARI (non-nullable -> model dogrulama) ──────────────
        // sort/sizes/colors eksikse 400 doner. Istemci bunlari HER ZAMAN gonderir; sozlesme
        // degisirse (or. nullable yapilirsa) bu pin kirilir ve istemci sadelestirilebilir.
        [Fact]
        public async Task Filter_Sort_Sizes_Colors_ZORUNLU_PINLENIR()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var eksik = await anon.PostAsJsonAsync("/api/product/filter", new { page = 1, size = 5 });
            eksik.StatusCode.Should().Be(HttpStatusCode.BadRequest, "sort/sizes/colors eksikken 400 beklenir");
            var govde = await eksik.Content.ReadAsStringAsync();
            govde.Should().Contain("required", $"hata gerekce ICERMELI (yalniz durum koduna bakilmaz): {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(govde)}");

            // POZITIF OLAY: ucu de gonderilince ayni istek CALISIYOR.
            var tam = await anon.PostAsJsonAsync("/api/product/filter", FullFilter());
            tam.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ── 3) SPRINT 8 MADDE 5: LISTE YOLU ARTIK category_name / total_stock / sizes DOLDURUYOR ──
        //
        // ESKI PIN BILINCLI KIRILDI: "Filter_ListeYolu_CategoryName_TotalStock_Sizes_DOLDURMUYOR_PINLENIR".
        // O pin, E1'de OLCULEN bir SUPHELI davranisi sabitliyordu: liste yolu bu uc alani hic
        // doldurmuyordu, ham veriyle vitrindeki her urun "kategorisiz + 0 stok + bedensiz"
        // gorunuyor ve bastan sona "Tukendi" yaziyordu. Istemci bunu urun basina AYRI detay
        // cagrisiyla telafi ediyordu (6 eszamanli; bir vitrin sayfasi 1 + 24 = 25 istek).
        // Sprint 8'de backend duzeltildi ve o telafi KALDIRILDI - dolayisiyla eski pin artik
        // YANLIS bir sozlesmeyi savunuyordu ve yerini bu pin aldi.
        //
        // CIFT-ANLAM KIRICI: "sizes dolu" demek yetmez - SATILABILIR bedenin geldigi, rezerve
        // edilmis bedenin GELMEDIGI de olculur. Aksi halde "tum bedenleri dondur" gibi yanlis
        // bir uygulama da testi gecerdi.
        [Fact]
        public async Task Filter_ListeYolu_CategoryName_TotalStock_Sizes_DOLDURUR()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var resp = await anon.PostAsJsonAsync("/api/product/filter", FullFilter());
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = await resp.Content.ReadFromJsonAsync<FilterEnvelope>();
            var items = page?.data?.items;
            items.Should().NotBeNull().And.NotBeEmpty("tohum urun listede olmali (vakum kirici)");

            var row = items!.First(i => i.id == _productId);
            row.price.Should().Be(250m);
            row.category_id.Should().Be(_categoryId);

            row.category_name.Should().NotBeNullOrWhiteSpace(
                "liste yolu artik kategori ADINI donduruyor - istemci ayrica kategori listesi cekmek zorunda degil");
            row.total_stock.Should().Be(7,
                "SATILABILIR stok donmeli (stock_quantity - reserved_quantity). Tohum: 10 fiziksel, 3 rezerve.");
            row.sizes.Should().NotBeEmpty("liste yolu artik musait bedenleri donduruyor");

            // Detay ucu FIZIKSEL stogu (10) donmeye devam eder - iki uc AYRI sorulari yanitliyor.
            // Bu, "liste yolu detaydan kopyaliyor" gibi bir yanlis okumayi da engeller.
            var detay = await anon.GetAsync($"/api/product/get/{_productId}");
            detay.StatusCode.Should().Be(HttpStatusCode.OK);
            var d = await detay.Content.ReadFromJsonAsync<DetailEnvelope>();
            d!.data!.stocks.Should().ContainSingle().Which.stock_quantity.Should().Be(7,
                "detay ucu kendi sozlesmesini korur");
        }

        // ── 4) ARAMA PARAMETRE ADI: "query" (q DEGIL) ────────────────────────────────────
        // Istemci "q" gonderiyordu; [FromQuery] ProductSearchRequestDto "query" bagliyor.
        // "q" ile arama metni HIC uygulanmiyor - filtresiz sonuc donuyordu.
        [Fact]
        public async Task Arama_QueryParametresi_Filtreler_q_Parametresi_FILTRELEMEZ()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            // Eslesmeyen bir metin: "query" ile SIFIR sonuc gelmeli.
            var dogru = await anon.GetAsync("/api/search/products?query=zzz-eslesmeyen-kelime&page=1&size=10");
            dogru.StatusCode.Should().Be(HttpStatusCode.OK);
            var d1 = await dogru.Content.ReadFromJsonAsync<SearchEnvelope>();
            (d1?.data?.items ?? new List<SearchRow>()).Should().BeEmpty(
                "query BAGLANIYOR - eslesmeyen metin sonuc dondurmemeli");

            // Ayni metin "q" ile gonderilirse parametre baglanmaz ve urun yine doner.
            var yanlis = await anon.GetAsync("/api/search/products?q=zzz-eslesmeyen-kelime&page=1&size=10");
            yanlis.StatusCode.Should().Be(HttpStatusCode.OK);
            var d2 = await yanlis.Content.ReadFromJsonAsync<SearchEnvelope>();
            (d2?.data?.items ?? new List<SearchRow>()).Should().NotBeEmpty(
                "MEVCUT DAVRANIS: 'q' baglanmiyor - arama metni yok sayilip tum urunler donuyor");

            // POZITIF OLAY: gercek kelimeyle query GERCEKTEN buluyor.
            var bulan = await anon.GetAsync("/api/search/products?query=Vitrin&page=1&size=10");
            var d3 = await bulan.Content.ReadFromJsonAsync<SearchEnvelope>();
            (d3?.data?.items ?? new List<SearchRow>()).Should().NotBeEmpty("gercek kelime sonuc dondurmeli");
        }

        // ── Zarf tipleri ────────────────────────────────────────────────────────────────
        // ══ D3 (GERCEK OLCEK PROVASI) - SAYFALAMA SOZLESMESI ════════════════════════════
        //
        // OLCULEN ZARAR (403 urunluk katalogla, tarayicida): storefront `loadCatalog` HER ZAMAN
        // { page:1, size:24 } cekiyor, sayfa 2'yi HIC istemiyordu ve bellegi o 24 urunle
        // DEGISTIRIYORDU. Musteri katalogun ilk 24 urununu gezebiliyor, kalan %94'e GEZINEREK
        // ULASAMIYORDU. Istemci duzeltildi; burada SUNUCU SOZLESMESI pinlenir - istemcinin
        // dayandigi sey sessizce degisirse vitrin bozulmadan ONCE kirmizi gorelim.

        private async Task<int> EkUrunlerAsync(int adet, int? kategoriId = null)
        {
            await using var ctx = NewContext();
            var kid = kategoriId ?? _categoryId;
            for (var i = 0; i < adet; i++)
            {
                ctx.Products.Add(new Product
                {
                    name = $"Sayfalama Urun {Guid.NewGuid():N}",
                    brand = "Divisima",
                    category_id = kid,
                    price = 100m + i,
                    description = "sayfalama sozlesmesi",
                    color_hex = "#202020",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
            }
            await ctx.SaveChangesAsync();
            return kid;
        }

        // ── D3-1) IKINCI SAYFA GERCEKTEN FARKLI URUNLER DONER ────────────────────────────
        // Istemcinin yeni "Daha Fazla Yukle" akisi TAM OLARAK buna dayaniyor.
        [Fact]
        public async Task Filter_IKINCI_SAYFA_FARKLI_URUNLER_Doner_ve_TOPLAM_SAYFA_TUTARLI()
        {
            if (Skipped()) return;
            await EkUrunlerAsync(9);   // tohumdaki 1 urunle birlikte 10 -> size 4 ile 3 sayfa
            var anon = _factory!.CreateClient();

            var s1 = await anon.PostAsJsonAsync("/api/product/filter", FullFilter(page: 1, size: 4));
            s1.StatusCode.Should().Be(HttpStatusCode.OK);
            var p1 = await s1.Content.ReadFromJsonAsync<FilterEnvelope>();

            // VAKUM KIRICI: ilk sayfa GERCEKTEN dolu olmali, yoksa "farkli" iddiasi bedava dogru olurdu.
            p1!.data!.items.Should().NotBeNullOrEmpty("ilk sayfa dolu olmali");
            p1.data.total_count.Should().BeGreaterThan(4, "sayfalamayi anlamli kilacak kadar urun olmali");
            p1.data.total_pages.Should().BeGreaterThan(1, "birden fazla sayfa olmali");

            var s2 = await anon.PostAsJsonAsync("/api/product/filter", FullFilter(page: 2, size: 4));
            s2.StatusCode.Should().Be(HttpStatusCode.OK);
            var p2 = await s2.Content.ReadFromJsonAsync<FilterEnvelope>();
            p2!.data!.items.Should().NotBeNullOrEmpty("ikinci sayfa da dolu olmali");

            // CIFT-ANLAM KIRICI: "her sayfa ilk N'i donduren" bir uygulama da 200 doner ve
            // dolu liste verir - ama AYNI urunleri. Kesisim BOS olmali.
            var id1 = p1.data.items!.Select(x => x.id).ToHashSet();
            var id2 = p2.data.items!.Select(x => x.id).ToHashSet();
            id1.Overlaps(id2).Should().BeFalse(
                "ikinci sayfa BIREBIR farkli urunler dondurmeli - aksi halde istemcinin sayfalamasi ayni urunu tekrar tekrar ceker");

            p1.data.total_count.Should().Be(p2.data.total_count, "toplam kayit sayisi sayfalar arasi DEGISMEMELI");
        }

        // ── D3-2) KATEGORI FILTRESI SUNUCUDA UYGULANIR ───────────────────────────────────
        // Istemci artik kategori rotasinda `category_id` gonderiyor (onceden ana sayfanin
        // 24 urunu ICINDEN istemci tarafinda suzuyordu - kategori basina 3 urun gorunuyordu).
        [Fact]
        public async Task Filter_KATEGORI_FILTRESINI_SUNUCUDA_Uygular()
        {
            if (Skipped()) return;
            int digerKategoriId;
            await using (var ctx = NewContext())
            {
                var c = new Category
                {
                    name = "Ikinci Kategori",
                    slug = $"ikinci-{Guid.NewGuid():N}",
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(c);
                await ctx.SaveChangesAsync();
                digerKategoriId = c.id;
            }
            await EkUrunlerAsync(5, digerKategoriId);
            var anon = _factory!.CreateClient();

            // VAKUM KIRICI: filtresiz cagri IKI kategoriyi de goruyor olmali.
            var hepsi = await anon.PostAsJsonAsync("/api/product/filter", FullFilter(page: 1, size: 50));
            var h = await hepsi.Content.ReadFromJsonAsync<FilterEnvelope>();
            h!.data!.items!.Select(x => x.category_id).Distinct().Should().HaveCountGreaterThan(1,
                "filtresiz katalog birden fazla kategori icermeli - yoksa filtre iddiasi olculemez");

            var resp = await anon.PostAsJsonAsync("/api/product/filter",
                new { page = 1, size = 50, sort = "new", sizes = Array.Empty<string>(), colors = Array.Empty<string>(), category_id = digerKategoriId });
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var f = await resp.Content.ReadFromJsonAsync<FilterEnvelope>();

            f!.data!.items.Should().NotBeNullOrEmpty("kategori filtresi SONUC dondurmeli (vakum kirici)");
            f.data.items!.Should().OnlyContain(x => x.category_id == digerKategoriId,
                "kategori filtresi SUNUCUDA uygulanmali - istemci tarafi suzmeye birakilamaz");

            // CIFT-ANLAM KIRICI: filtre GERCEKTEN daraltmali; "hepsini don" uygulamasi gecemez.
            f.data.total_count.Should().BeLessThan(h.data.total_count,
                "filtreli toplam, filtresiz toplamdan KUCUK olmali");
        }

        // ── D3-3) ZENGINLESTIRME SAYFA 2'DE DE CALISIR (Dalga 3 yapi pini, olcekte) ──────
        // Dalga 3'un iddiasi: "liste ucu kalem basina EK SORGU atmaz ve alanlari doldurur".
        // Istemci artik sayfa 2+ cektigi icin bu iddia ORADA da gecerli olmali.
        [Fact]
        public async Task Filter_ZENGINLESTIRME_SAYFA_2_DE_AYNI_ALANLARI_Doldurur()
        {
            if (Skipped()) return;
            await EkUrunlerAsync(9);
            var anon = _factory!.CreateClient();

            var s2 = await anon.PostAsJsonAsync("/api/product/filter", FullFilter(page: 2, size: 4));
            s2.StatusCode.Should().Be(HttpStatusCode.OK);
            var p2 = await s2.Content.ReadFromJsonAsync<FilterEnvelope>();
            p2!.data!.items.Should().NotBeNullOrEmpty("ikinci sayfa dolu olmali (vakum kirici)");

            p2.data.items!.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.category_name),
                "kategori adi SAYFA 2'de de dolmali - zenginlestirme yalniz ilk sayfaya ozel olamaz");
            p2.data.items!.Should().OnlyContain(x => x.category_id > 0,
                "kategori kimligi SAYFA 2'de de gelmeli");
        }

        // ── TAKSONOMI) KATEGORI UCU MENUNUN DAYANDIGI ALANLARI DONER ────────────────────
        // Gezinme menusu artik SABIT bir dizi degil, bu ucun yanitindan uretiliyor. Menu
        // `slug` (rota), `name` (etiket), `display_order` (sira) ve `sub_categories`
        // (alt menu) alanlarina dayaniyor; biri sessizce kaybolursa vitrin menusu bozulur.
        [Fact]
        public async Task KategoriUcu_MENUNUN_DAYANDIGI_ALANLARI_Doner()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var resp = await anon.GetAsync("/api/category/getlist");
            resp.StatusCode.Should().Be(HttpStatusCode.OK, "kategori listesi ANONIM erisilebilir olmali");
            var env = await resp.Content.ReadFromJsonAsync<KategoriEnvelope>();

            // VAKUM KIRICI: liste GERCEKTEN dolu olmali, yoksa alan iddialari bedava dogru olurdu.
            env!.data.Should().NotBeNullOrEmpty("tohum kategori listede olmali");

            var satir = env.data!.First(c => c.id == _categoryId);
            satir.slug.Should().NotBeNullOrWhiteSpace("rota slug'i SUNUCUDAN gelmeli - istemci ad'dan turetmemeli");
            satir.name.Should().NotBeNullOrWhiteSpace("menu etiketi gelmeli");

            // ALT KATEGORI SOZLESMESI: alan MEVCUT olmali. Bugun BOS (sub_categories tablosu
            // bos ve onlar icin ayri uc YOK) - istemci de bu yuzden alt menu CIZMIYOR.
            // Alan tumden kaybolursa alt menu sessizce hic gelmez; pin bunu yakalar.
            satir.sub_categories.Should().NotBeNull(
                "sub_categories alani SOZLESMEDE olmali - istemci alt menuyu ONDAN uretiyor");
        }

        private sealed class KategoriEnvelope { public List<KategoriRow>? data { get; set; } }
        private sealed class KategoriRow
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? slug { get; set; }
            public int display_order { get; set; }
            public List<object>? sub_categories { get; set; }
        }

        private sealed class FilterEnvelope { public FilterPage? data { get; set; } }
        private sealed class FilterPage
        {
            public List<FilterRow>? items { get; set; }
            public int total_count { get; set; }
            public int total_pages { get; set; }
            public int page { get; set; }
        }
        private sealed class FilterRow
        {
            public int id { get; set; }
            public int category_id { get; set; }
            public string? category_name { get; set; }
            public decimal price { get; set; }
            public int total_stock { get; set; }
            public List<string> sizes { get; set; } = new();
        }

        private sealed class DetailEnvelope { public DetailRow? data { get; set; } }
        private sealed class DetailRow { public List<StockRow> stocks { get; set; } = new(); }
        private sealed class StockRow { public string size { get; set; } = ""; public int stock_quantity { get; set; } }

        // PagedResult<T> camelCase serilesir: Items -> items
        private sealed class SearchEnvelope { public SearchPage? data { get; set; } }
        private sealed class SearchPage { public List<SearchRow>? items { get; set; } }
        private sealed class SearchRow { public int id { get; set; } public string name { get; set; } = ""; }
    }
}
