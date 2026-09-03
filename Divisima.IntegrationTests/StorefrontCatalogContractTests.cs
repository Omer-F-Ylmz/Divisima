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
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = TestDbAdi.Cozumle(DbName) }.ConnectionString;
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
        // MANTIK-FIX-1 / K1 - PREMIS DEGISIKLIGI (merkez onayi ister):
        // Tohumda `sale_price` atayan TEK BIR URUN YOKTU (olculdu: depodaki 64 "new Product"
        // tohumunun HICBIRI bu alani atamiyor; negatif kontrol: color_hex 40 atama). Bu haliyle
        // K1'in davranisi HICBIR YERDE olculemezdi - her K1 davranis pini VAKUM olurdu.
        // MFIX-B / K1'de bu sinifin tohumu ayni sebeple 7/0 -> 10/3 yapilmisti; ayni kalip.
        private int _indirimliId;   // penceresi ACIK indirim  -> etkin fiyat = sale_price
        private int _penceresiKapaliId; // sale_end GECMISTE   -> etkin fiyat = price (cift-anlam kirici)

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
                // MFIX-B / K1: tohum 7/0 idi ve BU YUZDEN detay asserti VAKUMDU - fiziksel ile
                // satilabilir AYNI sayiyi (7) veriyordu, yani K1 ONCESI ve SONRASI ayirt EDILEMIYORDU.
                // Ustelik testin kendi yorumlari "10 fiziksel, 3 rezerve" diyordu: tohumla CELISIYORDU.
                // Yorumlarin SOYLEDIGI tohum yazildi; satilabilir yine 7, yani liste asserti AYNEN gecerli.
                stock_quantity = 10,
                reserved_quantity = 3,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            // MANTIK-FIX-1 / K1 tohum genislemesi (gerekce alan tanimlarinda).
            // IKI urun daha: biri penceresi ACIK indirimli, biri penceresi KAPALI.
            // Ikincisi CIFT-ANLAM KIRICIDIR: "sale_price doluysa uygula" diyen yanlis
            // bir uygulama pini GECEMEZ, cunku pencere kapaliyken liste fiyati beklenir.
            var indirimli = new Product
            {
                name = "Vitrin Indirimli",
                brand = "Divisima",
                category_id = cat.id,
                price = 400m,
                sale_price = 300m,
                description = "penceresi acik indirim",
                color_hex = "#202020",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            var kapali = new Product
            {
                name = "Vitrin Penceresi Kapali",
                brand = "Divisima",
                category_id = cat.id,
                price = 500m,
                sale_price = 350m,
                sale_end = DateTime.Now.AddDays(-1),   // pencere GECMISTE kapandi
                description = "penceresi kapali indirim",
                color_hex = "#303030",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.AddRange(indirimli, kapali);
            await ctx.SaveChangesAsync();
            _indirimliId = indirimli.id;
            _penceresiKapaliId = kapali.id;

            ctx.ProductStocks.AddRange(
                new ProductStock { product_id = indirimli.id, size = "M", stock_quantity = 5, reserved_quantity = 0, is_active = true, created_at = DateTime.Now },
                new ProductStock { product_id = kapali.id, size = "M", stock_quantity = 5, reserved_quantity = 0, is_active = true, created_at = DateTime.Now });
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
        // ── P14 (MFIX-B / K1) ────────────────────────────────────────────────────────────
        // OLCULEN ONCE-DURUM (canli, urun 937): DETAY ucu S=12/M=10/L=11 (FIZIKSEL, toplam 33)
        // donerken LISTE yolu total_stock=26 (SATILABILIR) donuyordu - fark tam olarak 7 rezerve
        // adet. AYNI SINIFTA IKI STOK TANIMI. Musteri "10 var" gorup 5'ini sepete koyamiyordu
        // (beden-basi ust sinir bu degerden turuyor).
        //
        // FIZIKSEL stok artik YALNIZ admin ucundan gorunur (GET /api/Stock/{id}), anonim uclarda
        // stock_quantity SATILABILIR adedi tasir. ProductStockDto DEGISMEDI (E4a: reserved_quantity
        // anonim uca ACILMAZ; ustelik available'i stock_quantity'nin YANINA koymak da rezerveyi
        // cikarilabilir kilardi).
        [Fact]
        public async Task AnonimDetay_Stogu_SATILABILIR_Doner_FizikselDegil()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            // VAKUM KIRICI: tohum GERCEKTEN fiziksel != satilabilir olmali, aksi halde iki
            // sozlesme ayni sayiyi verir ve bu test K1 ONCESI de gecerdi.
            await using (var ctx = NewContext())
            {
                var st = await ctx.ProductStocks.AsNoTracking().SingleAsync(s => s.product_id == _productId);
                st.stock_quantity.Should().Be(10);
                st.reserved_quantity.Should().Be(3);
                st.stock_quantity.Should().NotBe(st.stock_quantity - st.reserved_quantity,
                    "tohum ayirt edici olmali - fiziksel ile satilabilir FARKLI");
            }

            var detay = await anon.GetAsync($"/api/product/get/{_productId}");
            detay.StatusCode.Should().Be(HttpStatusCode.OK);
            var d = await detay.Content.ReadFromJsonAsync<DetailEnvelope>();
            var beden = d!.data!.stocks.Should().ContainSingle().Subject;

            beden.stock_quantity.Should().Be(7, "anonim detay SATILABILIR (10-3) donmeli");
            beden.stock_quantity.Should().NotBe(10, "FIZIKSEL stok anonim uca SIZMAMALI");

            // CIFT-ANLAM KIRICI: iki yol ARTIK AYNI seyi soyluyor. Yalniz "detay 7 dondu" demek
            // yetmez - liste yolunun da AYNI degeri verdigi gosterilmeli, yoksa formulun tek
            // kaynaktan geldigi kanitlanmis olmaz.
            var resp = await anon.PostAsJsonAsync("/api/product/filter", FullFilter());
            var page = await resp.Content.ReadFromJsonAsync<FilterEnvelope>();
            var row = page!.data!.items!.First(i => i.id == _productId);
            row.total_stock.Should().Be(beden.stock_quantity,
                "liste ve detay ayni formulden (StokHesabi.Satilabilir) beslenmeli");
        }

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

            // MFIX-B / K1: DETAY UCU DE SATILABILIR DONER (once FIZIKSEL donuyordu - 10).
            // Iki yol artik AYNI sozlesmeyi tasir; "ayni sinifta iki stok tanimi" kapandi.
            // Fiziksel stok YALNIZ admin ucundan (GET /api/Stock/{id}) gorunur.
            var detay = await anon.GetAsync($"/api/product/get/{_productId}");
            detay.StatusCode.Should().Be(HttpStatusCode.OK);
            var d = await detay.Content.ReadFromJsonAsync<DetailEnvelope>();
            d!.data!.stocks.Should().ContainSingle().Which.stock_quantity.Should().Be(7,
                "anonim detay SATILABILIR (10-3) donmeli; FIZIKSEL 10 SIZMAMALI");
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

        // ═══ p-k1b - FAZ 0 / K1: ETAG'IN ILK DAVRANIS PINI ════════════════════════════════
        //
        // ETag middleware ILK COMMIT'ten beri var ama HICBIR pin onu olcmuyordu (FAZ 0'da
        // tarandi: ETag|If-None-Match|304 -> test projesinde 0 eslesme). K1'de listeden olu
        // "/api/sizeguide" oneki kaldirildi; bu pin hem CALISAN kapsami hem de KAPSAM DISINI
        // davranisla sabitler - yani onek listesine bir gun "/api/size-guide" eklenirse de
        // KIRILIR ve o karar BILINCLI verilmek zorunda kalir.
        //
        // Bu sinifa eklendi (yeni SQL sinifi ACILMADI - 10d794d dersi): host ve tohum zaten var.
        [Fact]
        public async Task ETag_KATALOG_UCUNDA_VAR_ve_304_DONER_SIZE_GUIDE_KAPSAM_DISINDA()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            // ── (1) Kapsam ICI: /api/product GET -> ETag VAR ──
            var ilk = await anon.GetAsync($"/api/product/get/{_productId}");
            ilk.StatusCode.Should().Be(HttpStatusCode.OK, "tohumlanan urun anonim okunabilmeli");
            ilk.Headers.ETag.Should().NotBeNull("/api/product oneki ETag kapsaminda");
            var etag = ilk.Headers.ETag!.ToString();
            etag.Should().NotBeNullOrWhiteSpace();

            // ── (2) AYNI istege If-None-Match -> 304 + BOS govde ──
            var istek = new HttpRequestMessage(HttpMethod.Get, $"/api/product/get/{_productId}");
            istek.Headers.TryAddWithoutValidation("If-None-Match", etag);
            var ikinci = await anon.SendAsync(istek);
            ikinci.StatusCode.Should().Be(HttpStatusCode.NotModified,
                "degismemis icerikte 304 donmeli - ETag'in VARLIK sebebi bu");
            (await ikinci.Content.ReadAsByteArrayAsync()).Length.Should().Be(0,
                "304 govde TASIMAMALI - bant genisligi tasarrufu tam da bu");

            // ── (3) CIFT-ANLAM KIRICI: kapsam DISI yol -> 200 ama ETag YOK ──
            // "her yanita ETag koyan" bir uygulama (1) ve (2)'yi gecerdi; kapsamin DAR oldugu
            // ancak boyle kanitlanir. K1'de canli olculen once-durumun ta kendisi.
            var sg = await anon.GetAsync($"/api/size-guide/category/{_categoryId}");
            ((int)sg.StatusCode).Should().BeLessThan(500,
                $"size-guide ucu ayakta olmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await sg.Content.ReadAsStringAsync())}");
            sg.Headers.ETag.Should().BeNull(
                "K1 karari: '/api/sizeguide' OLU onegi KALDIRILDI (duzeltilmedi). size-guide " +
                "vitrine baglanirsa onek BILINCLI olarak '/api/size-guide' yapilir ve bu pin kirilir");
        }

        // ── P18) MANTIK-FIX-1 / K1 - ETKIN FIYAT LISTEDE DE DONER, DETAYLA AYRISMAZ ──────
        // DAVRANIS pini (durust etiket): gercek HTTP uclari, gercek DB tohumu.
        //
        // OLCULEN ONCE-DURUM (R-M1a, siparis 257): urun 926 x5 icin ekran 2.499,50 TL +
        // "Ucretsiz kargo kazandin!" gosterdi, sunucu 1.874,60 + 49,90 = 1.924,50 tahsil etti.
        // Kok IKI KATMANLIYDI: liste DTO'su indirim bilgisini TASIMIYORDU (istemci telafi
        // EDEMEZDI) ve istemci detaydakini de okumuyordu. Bu pin BIRINCI katmani tutar.
        [Fact]
        public async Task Liste_ETKIN_FIYATI_Doner_ve_DETAYLA_AYRISMAZ()
        {
            if (Skipped()) return;
            var anon = _factory!.CreateClient();

            var resp = await anon.PostAsJsonAsync("/api/product/filter", FullFilter(1, 50));
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var env = await resp.Content.ReadFromJsonAsync<FilterEnvelope>();
            var satirlar = env?.data?.items;
            satirlar.Should().NotBeNullOrEmpty("tohum urunleri listede gorunmeli");

            var normal = satirlar!.SingleOrDefault(x => x.id == _productId);
            var indirimli = satirlar!.SingleOrDefault(x => x.id == _indirimliId);
            var kapali = satirlar!.SingleOrDefault(x => x.id == _penceresiKapaliId);
            normal.Should().NotBeNull("tohumdaki indirimsiz urun listede olmali");
            indirimli.Should().NotBeNull("tohumdaki indirimli urun listede olmali");
            kapali.Should().NotBeNull("tohumdaki penceresi kapali urun listede olmali");

            // (1) ASIL IDDIA: indirimli urunun etkin fiyati sale_price'tir.
            indirimli!.effective_price.Should().Be(300m,
                "liste yolu artik indirimli fiyati tasimali - musterinin ODEYECEGI tutar budur");

            // (2) VAKUM KIRICI: indirimsiz urunde etkin fiyat = liste fiyati.
            // Bu olmadan "her urune indirim uygula" diyen bir uygulama da (1)'i gecerdi.
            normal!.effective_price.Should().Be(250m,
                "indirimsiz urunde etkin fiyat liste fiyatina ESIT olmali");

            // (3) CIFT-ANLAM KIRICI: penceresi KAPALI indirim UYGULANMAZ.
            // "sale_price doluysa uygula" diyen yanlis uygulama bu asserti GECEMEZ; ayrica
            // PricingHelper.IsOnSale sozlesmesinin SUNUCUDA degerlendirildigini kanitlar.
            kapali!.effective_price.Should().Be(500m,
                "sale_end GECMISTE ise indirim UYGULANMAZ - pencere SUNUCUDA degerlendirilir");

            // (4) `price` ALANININ ANLAMI DEGISMEDI. Admin duzenleme formu ayni degeri geri
            // yaziyor (A2/EKSEN-1); anlami kaysaydi taban fiyat KALICI olarak asagi kayardi.
            indirimli.price.Should().Be(400m, "liste fiyati ALANININ anlami DEGISMEMELI");
            kapali.price.Should().Be(500m);

            // (5) LISTE <-> DETAY AYRISMAZ. Ayni urun icin iki uc AYNI etkin fiyati vermeli;
            // MANTIK-AV-1'de olculen zarar tam da bu iki yolun ayrismasiydi.
            foreach (var (id, beklenen) in new[] { (_productId, 250m), (_indirimliId, 300m), (_penceresiKapaliId, 500m) })
            {
                var d = await anon.GetAsync($"/api/product/get/{id}");
                d.StatusCode.Should().Be(HttpStatusCode.OK);
                var dd = (await d.Content.ReadFromJsonAsync<DetailEnvelope>())?.data;
                dd.Should().NotBeNull();
                dd!.effective_price.Should().Be(beklenen,
                    $"urun {id}: detay ucu listeyle AYNI etkin fiyati vermeli");
            }

            // (6) sale_price HAM KALIR. Admin formu bu alani geri yaziyor; pencere kapaliyken
            // NULL'lansaydi ileride tanimlanacak bir kampanya SESSIZCE SILINIRDI (Dalga B sinifi).
            var kapaliDetay = (await (await anon.GetAsync($"/api/product/get/{_penceresiKapaliId}"))
                .Content.ReadFromJsonAsync<DetailEnvelope>())?.data;
            kapaliDetay!.sale_price.Should().Be(350m,
                "sale_price HAM kalmali - admin formunun geri yazdigi alan budur");
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
            public decimal effective_price { get; set; }
            public decimal? old_price { get; set; }
            public int total_stock { get; set; }
            public List<string> sizes { get; set; } = new();
        }

        private sealed class DetailEnvelope { public DetailRow? data { get; set; } }
        private sealed class DetailRow
        {
            public decimal price { get; set; }
            public decimal effective_price { get; set; }
            public decimal? sale_price { get; set; }
            public List<StockRow> stocks { get; set; } = new();
        }
        private sealed class StockRow { public string size { get; set; } = ""; public int stock_quantity { get; set; } }

        // PagedResult<T> camelCase serilesir: Items -> items
        private sealed class SearchEnvelope { public SearchPage? data { get; set; } }
        private sealed class SearchPage { public List<SearchRow>? items { get; set; } }
        private sealed class SearchRow { public int id { get; set; } public string name { get; set; } = ""; }
    }
}
