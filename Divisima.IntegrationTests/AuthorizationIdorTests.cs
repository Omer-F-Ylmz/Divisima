using System.Net;
using System.Net.Http.Json;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Address;
using Divisima.Entity.Dtos.Cart;
using Divisima.Entity.Dtos.Order;
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
    // D4 - YETKI + IDOR MATRISI
    //
    // Iki GERCEK musteri (A ve B) TestAuthHelper ile uretilir; B, A nin kaynaklarina erismeye
    // calisir. Olculen sey uygulamanin kendi sahiplik filtreleridir (servis katmanindaki
    // customer_id karsilastirmalari). Sizinti = kirmizi.
    //
    // NEDEN IKI AYRI HOST: AuthController uzerinde [EnableRateLimiting("auth")] var; policy
    // PermitLimit=5/dakika ve PARTITIONSUZ tek kova. TestAuthHelper musteri basina 3 auth
    // istegi atiyor (register + verify-email + login). Tek host uzerinde iki musteri 6 istek
    // eder, altincisi 429 alir ve test KURULUMU cokerdi. Iki ayri WebApplicationFactory ornegi
    // AYNI test veritabanina bakar; her host kendi limiter kovasini tasidigi icin 3 + 3 olur.
    // Paylasilan sey veritabani, ayrilan sey yalniz surec ici limiter durumu.
    [Trait("Category", "Sql")]
    public class AuthorizationIdorTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaAuthIdorTest";

        // DUSUK ENTROPILI SABIT (depo kalibi: SifrePolitikasiTests / LaunchFixMailZinciriTests).
        // Sifre politikasini KARSILAR (>=8, buyuk, kucuk, rakam) ama Shannon entropisi 1.30 ile
        // gitleaks `generic-api-key` esiginin (3.5) COK ALTINDA. Gerekce CLAUDE.md bolum 1:
        // ayni sinif `secret-scan` kirmizisi bu depoda UC KEZ odendi ve ucunde de bedel
        // URETIM KODU DEGIL, TEST/KANIT YAZMA ANIYDI.
        private const string YeniGecerliSifre = "Aaaaaa11";
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

        // ═══ FIX-1A - PIN KANALI DEGISTIRILDI: BU HOST AUDIT INTERCEPTOR'I TASIR ═══════════════
        //
        // VAKUM KANITI (FAZ 1 / FIX-1A adim 0.d'de OLCULDU, varsayilmadi):
        // `Program.cs:182` DbContext'i `.AddInterceptors(sp.GetRequiredService<AuditInterceptor>())`
        // ile kaydediyor. Test fabrikalari ise `DbContextOptions<DivisimaDbContext>` kaydini
        // KALDIRIP duz `UseSqlServer(ConnStr)` ile YENIDEN kuruyor - yani interceptor'i DUSURUYOR.
        // Bu desen depoda TEK BIR dosyaya ozgu DEGIL: **42 test dosyasi** ayni sekilde yaziyor.
        // Sonuc: `AuditInterceptor` bugune kadar HICBIR test host'unda kosmadi. F2/F3 pinleri
        // duz bir fabrikaya yazilsaydi VAKUM PIN olurlardi - denetim kaydi hic uretilmeyecegi
        // icin "sir alani yok" assert'i BEDAVA yesil kalirdi.
        //
        // KAPSAM BILINCLI OLARAK DAR: yalnizca BU sinifin fabrikasi duzeltildi. 42 fabrikayi
        // birden degistirmek, denetim satiri sayan/beklemeyen testlerde ongorulemeyen yan etki
        // uretirdi ve bu commit'in kapsami disindadir. Genel duzeltme [HAVALE->FAZ 6].
        //
        // `AuditInterceptor` DI kaydi (`Program.cs:169 AddScoped<AuditInterceptor>()`) test
        // host'unda KALDIRILMIYOR; burada yalnizca DbContext'e yeniden BAGLANIYOR.
        private sealed class IdorFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>((sp, o) =>
                        o.UseSqlServer(ConnStr)
                         .AddInterceptors(sp.GetRequiredService<Divisima.DataAccess.Interceptors.AuditInterceptor>()));
                });
            }
        }

        private IdorFactory? _hostA;
        private IdorFactory? _hostB;
        private TestAuthHelper.AuthenticatedCustomer? _a;
        private TestAuthHelper.AuthenticatedCustomer? _b;
        private bool _sqlAvailable;

        private TestAuthHelper.AuthenticatedCustomer A => _a!;
        private TestAuthHelper.AuthenticatedCustomer B => _b!;

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
                _hostA = new IdorFactory();
                _hostB = new IdorFactory();
                _a = await TestAuthHelper.CreateCustomerClientAsync(_hostA);
                _b = await TestAuthHelper.CreateCustomerClientAsync(_hostB);
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak yetki/IDOR test ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_hostA != null) await _hostA.DisposeAsync();
            if (_hostB != null) await _hostB.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // Aciklayici yorum: Urun + beden bazli stok tohumla (description/color_hex zorunlu, kategori gercek).
        private static async Task<int> NewProductAsync(decimal price = 250m, int stock = 100)
        {
            await using var ctx = NewContext();
            var cat = new Category
            {
                name = "IDOR Kategori",
                slug = $"idor-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "IDOR Urun",
                brand = "T",
                category_id = cat.id,
                price = price,
                description = "idor testi urunu",
                color_hex = "#101010",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            ctx.ProductStocks.Add(new ProductStock
            {
                product_id = p.id,
                size = "M",
                stock_quantity = stock,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return p.id;
        }

        // Aciklayici yorum: A icin GERCEK siparis - HTTP ucundan, A nin token i ile. Sahiplik bagi
        // uygulamanin kendi akisindan kurulur, elle yazilan bir satirdan degil.
        private async Task<(int orderId, int itemId, int productId)> PlaceOrderForAAsync(int quantity = 2)
        {
            var productId = await NewProductAsync();

            var place = await A.Client.PostAsJsonAsync("/api/Order/place", new OrderCreateRequestDto
            {
                customer_id = A.CustomerId,
                coupon_code = "",
                use_store_credit = 0m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = quantity } }
            });
            var placeBody = await place.Content.ReadAsStringAsync();
            place.IsSuccessStatusCode.Should().BeTrue(
                $"A kendi siparisini verebilmeli: {(int)place.StatusCode} {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(placeBody)}");

            await using var ctx = NewContext();
            var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.customer_id == A.CustomerId);
            var it = await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(x => x.order_id == o.id);
            return (o.id, it.id, productId);
        }

        private static async Task<string> ReadOrderNumberAsync(int orderId)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).order_number;
        }

        // =====================================================================
        // GRUP 1 - IDOR: A nin kaynagi, B nin token i
        // =====================================================================

        // SOZLESME: baskasinin siparisi 404 (varligi gizler). Govdede A ya ait veri olmamali.
        [Fact]
        public async Task Idor_BaskaninSiparisi_Okunamaz_GovdedeVeriSizmaz()
        {
            if (Skipped()) return;
            var (orderId, _, _) = await PlaceOrderForAAsync();
            var orderNumber = await ReadOrderNumberAsync(orderId);

            // POZITIF OLAY: sahibi okuyabiliyor. Bu olmadan 404 "kaynak zaten yok" anlamina da gelirdi.
            var mine = await A.Client.GetAsync($"/api/Order/get/{orderId}");
            mine.StatusCode.Should().Be(HttpStatusCode.OK, "A kendi siparisini gorebilmeli");
            (await mine.Content.ReadAsStringAsync()).Should().Contain(orderNumber,
                "sahibinin govdesinde siparis numarasi bulunmali");

            var theirs = await B.Client.GetAsync($"/api/Order/get/{orderId}");
            theirs.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "yabanci siparis icin varlik bilgisi sizdirilmadan 404 donmeli");

            var body = await theirs.Content.ReadAsStringAsync();
            body.Should().NotContain(orderNumber, "hata govdesinde siparis numarasi SIZMAMALI");
            body.Should().NotContain(A.Email, "hata govdesinde sahibin e-postasi SIZMAMALI");
        }

        // SOZLESME: timeline da 404 - siparisle ayni gizleme politikasi.
        [Fact]
        public async Task Idor_BaskaninSiparisTimeline_Okunamaz()
        {
            if (Skipped()) return;
            var (orderId, _, _) = await PlaceOrderForAAsync();
            var orderNumber = await ReadOrderNumberAsync(orderId);

            var mine = await A.Client.GetAsync($"/api/Order/timeline/{orderId}");
            mine.StatusCode.Should().Be(HttpStatusCode.OK, "A kendi siparisinin gecmisini gorebilmeli");

            var theirs = await B.Client.GetAsync($"/api/Order/timeline/{orderId}");
            theirs.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var body = await theirs.Content.ReadAsStringAsync();
            body.Should().NotContain(orderNumber);
            body.Should().NotContain(A.Email);
        }

        // SOZLESME: kalem iptalinde 403 (siparisten FARKLI kod). Ve islem GERCEKTEN olmaz.
        [Fact]
        public async Task Idor_BaskaninSiparisKalemi_IptalEdilemez_KalemAynenKalir()
        {
            if (Skipped()) return;
            var (orderId, itemId, _) = await PlaceOrderForAAsync(quantity: 2);

            // Kismi iptal yalniz Confirmed/Preparing durumunda acik. Sahiplik kontrolu bundan ONCE
            // calisiyor; durumu ayarlamak POZITIF kontrolun (A iptal edebiliyor) calismasi icin.
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().SingleAsync(x => x.id == orderId);
                o.status = (byte)OrderStatusEnum.Confirmed;
                await ctx.SaveChangesAsync();
            }

            var theirs = await B.Client.PostAsync($"/api/Order/{orderId}/cancel-item/{itemId}", null);
            theirs.StatusCode.Should().Be(HttpStatusCode.NotFound, "TEK SOZLESME: sahiplik ihlali de 404 - varlik sizdirilmaz");
            (await theirs.Content.ReadAsStringAsync()).Should().NotContain(A.Email);

            // ISLEM GERCEKTEN OLMADI.
            await using (var ctx = NewContext())
            {
                (await ctx.Set<OrderItem>().AsNoTracking().SingleAsync(i => i.id == itemId))
                    .is_cancelled.Should().BeFalse("B nin denemesi kalemi iptal ETMEMELI");
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.id == orderId))
                    .status.Should().Be((byte)OrderStatusEnum.Confirmed, "siparis durumu degismemeli");
            }

            // POZITIF OLAY: ayni cagri SAHIBI tarafindan yapilinca calisiyor -> 403 gercekten sahiplikten.
            var mine = await A.Client.PostAsync($"/api/Order/{orderId}/cancel-item/{itemId}", null);
            mine.IsSuccessStatusCode.Should().BeTrue(
                $"sahibi kendi kalemini iptal edebilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await mine.Content.ReadAsStringAsync())}");
            await using (var ctx = NewContext())
            {
                (await ctx.Set<OrderItem>().AsNoTracking().SingleAsync(i => i.id == itemId))
                    .is_cancelled.Should().BeTrue("sahibinin iptali kalemi gercekten iptal etmeli");
            }
        }

        // SOZLESME: fatura 403 - siparisten FARKLI kod (varligi dogruluyor). Bkz. rapor: tutarsizlik.
        [Fact]
        public async Task Idor_BaskaninFaturasi_Okunamaz_FaturaNoSizmaz()
        {
            if (Skipped()) return;
            var (orderId, _, _) = await PlaceOrderForAAsync();

            // Fatura siparis verilirken UYGULAMA tarafindan uretiliyor (invoices.order_id uzerinde
            // tekil indeks var). Elle ikinci bir satir eklemek duplicate key verir - var olani okuyoruz.
            string invoiceNumber;
            await using (var ctx = NewContext())
                invoiceNumber = (await ctx.Set<Invoice>().AsNoTracking()
                    .SingleAsync(i => i.order_id == orderId)).invoice_number;
            invoiceNumber.Should().NotBeNullOrWhiteSpace("siparisle birlikte fatura uretilmis olmali");

            var mine = await A.Client.GetAsync($"/api/Invoice/order/{orderId}");
            mine.StatusCode.Should().Be(HttpStatusCode.OK, "A kendi faturasini gorebilmeli");
            (await mine.Content.ReadAsStringAsync()).Should().Contain(invoiceNumber);

            var theirs = await B.Client.GetAsync($"/api/Invoice/order/{orderId}");
            theirs.StatusCode.Should().Be(HttpStatusCode.NotFound, "TEK SOZLESME: sahiplik ihlali de 404 - fatura varligi sizdirilmaz");

            var body = await theirs.Content.ReadAsStringAsync();
            body.Should().NotContain(invoiceNumber, "fatura numarasi SIZMAMALI");
            body.Should().NotContain(A.Email);
        }

        // SOZLESME: adres silmede 403 ve adres GERCEKTEN durur (soft delete tetiklenmez).
        [Fact]
        public async Task Idor_BaskaninAdresi_Silinemez_AdresAktifKalir()
        {
            if (Skipped()) return;

            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "Ev",
                full_name = "A Musteri",
                phone = "5551112233",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "IDOR test adresi",
                is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue(
                $"A adres ekleyebilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await upsert.Content.ReadAsStringAsync())}");

            int addressId;
            await using (var ctx = NewContext())
                addressId = (await ctx.Set<Address>().AsNoTracking()
                    .SingleAsync(a => a.customer_id == A.CustomerId)).id;

            var theirs = await B.Client.DeleteAsync($"/api/Address/delete/{addressId}");
            theirs.StatusCode.Should().Be(HttpStatusCode.NotFound, "TEK SOZLESME: sahiplik ihlali de 404");

            await using (var ctx = NewContext())
            {
                var addr = await ctx.Set<Address>().AsNoTracking().SingleAsync(a => a.id == addressId);
                addr.is_active.Should().BeTrue("B nin denemesi adresi pasiflestirMEMELI");
                addr.customer_id.Should().Be(A.CustomerId);
            }

            // POZITIF OLAY: sahibi silebiliyor.
            var mine = await A.Client.DeleteAsync($"/api/Address/delete/{addressId}");
            mine.IsSuccessStatusCode.Should().BeTrue(
                $"sahibi kendi adresini silebilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await mine.Content.ReadAsStringAsync())}");
            // IgnoreQueryFilters SART: Address uzerinde global is_active filtresi var; soft-delete
            // sonrasi satir normal sorguda GORUNMEZ. Filtre kalkmadan bu assert "satir yok"
            // istisnasi atardi - yani silinme kaniti degil, sorgu koruldugu icin kaybolurdu.
            await using (var ctx = NewContext())
                (await ctx.Set<Address>().IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.id == addressId))
                    .is_active.Should().BeFalse("sahibinin silmesi soft-delete ile is_active i dusurmeli");
        }

        // SOZLESME: sepet ve favori uclari kaynak id ALMIYOR - izolasyon tamamen token dan gelir.
        [Fact]
        public async Task Idor_SepetVeFavoriler_MusteriBazinda_Izole()
        {
            if (Skipped()) return;
            var productId = await NewProductAsync(price: 90m, stock: 20);

            var add = await A.Client.PostAsJsonAsync("/api/Cart/add", new CartItemRequestDto
            { customer_id = A.CustomerId, product_id = productId, size = "M", quantity = 1 });
            add.IsSuccessStatusCode.Should().BeTrue($"A sepete ekleyebilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await add.Content.ReadAsStringAsync())}");

            var wish = await A.Client.PostAsync($"/api/Wishlist/toggle?productId={productId}", null);
            wish.IsSuccessStatusCode.Should().BeTrue($"A favoriye ekleyebilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await wish.Content.ReadAsStringAsync())}");

            // POZITIF OLAY: A nin listeleri gercekten dolu (DB seviyesinde).
            await using (var ctx = NewContext())
            {
                var aCartIds = await ctx.Set<Cart>().Where(c => c.customer_id == A.CustomerId)
                    .Select(c => c.id).ToListAsync();
                (await ctx.Set<CartItem>().CountAsync(ci => aCartIds.Contains(ci.cart_id)))
                    .Should().BeGreaterThan(0, "A nin sepetinde satir olusmali");
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId))
                    .Should().Be(1, "A nin favorisinde tam bir satir olmali");
            }

            // B nin listeleri A nin verisini ICERMEZ - hem govde hem DB.
            (await (await B.Client.GetAsync("/api/Cart")).Content.ReadAsStringAsync())
                .Should().NotContain($"\"product_id\":{productId}", "B nin sepetinde A nin urunu GORUNMEMELI");
            (await (await B.Client.GetAsync("/api/Wishlist")).Content.ReadAsStringAsync())
                .Should().NotContain($"\"product_id\":{productId}", "B nin favorilerinde A nin urunu GORUNMEMELI");

            await using (var ctx = NewContext())
            {
                var bCartIds = await ctx.Set<Cart>().Where(c => c.customer_id == B.CustomerId)
                    .Select(c => c.id).ToListAsync();
                (await ctx.Set<CartItem>().CountAsync(ci => bCartIds.Contains(ci.cart_id)))
                    .Should().Be(0, "B nin sepetinde hic satir olusmamali");
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == B.CustomerId))
                    .Should().Be(0, "B nin favorisinde hic satir olusmamali");
            }
        }

        // SPRINT 2 - TEK SOZLESME TARAMASI.
        // Oncesinde sahiplik ihlali uclara gore FARKLI cevap veriyordu: siparis/timeline 404
        // (varligi gizler) ama kalem-iptal / fatura / adres 403 (varligi DOGRULAR). Siparis
        // ucundaki gizleme bu yuzden deliniyordu: id tarayan biri /api/Invoice/order/{id} 403 mu
        // 404 mu donduguna bakarak hangi siparis id'lerinin GERCEK oldugunu ogrenebiliyordu.
        // Artik bes ucun hepsi ayni kodu doner ve govdede sahibe ait hicbir veri bulunmaz.
        [Fact]
        public async Task Idor_BesUcunHEPSI_AYNI_KODU_Doner_VeGovdeSizdirmaz()
        {
            if (Skipped()) return;
            var (orderId, itemId, _) = await PlaceOrderForAAsync();
            var orderNumber = await ReadOrderNumberAsync(orderId);

            // Kalem iptali yalniz Confirmed/Preparing'de acik - sahiplik kontrolu bundan ONCE
            // calisiyor ama testin "sebep sahiplik" oldugunu netlestirmesi icin durumu ayarla.
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().SingleAsync(x => x.id == orderId);
                o.status = (byte)OrderStatusEnum.Confirmed;
                await ctx.SaveChangesAsync();
            }

            string invoiceNumber;
            await using (var ctx = NewContext())
                invoiceNumber = (await ctx.Set<Invoice>().AsNoTracking()
                    .SingleAsync(i => i.order_id == orderId)).invoice_number;

            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "Ev",
                full_name = "A Musteri",
                phone = "5551112233",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "Sozlesme testi adresi",
                is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue("adres eklenebilmeli");
            int addressId;
            await using (var ctx = NewContext())
                addressId = (await ctx.Set<Address>().AsNoTracking().SingleAsync(a => a.customer_id == A.CustomerId)).id;

            var denemeler = new (string ad, Func<Task<HttpResponseMessage>> cagri)[]
            {
                ("siparis detayi",  () => B.Client.GetAsync($"/api/Order/get/{orderId}")),
                ("siparis timeline",() => B.Client.GetAsync($"/api/Order/timeline/{orderId}")),
                ("kalem iptali",    () => B.Client.PostAsync($"/api/Order/{orderId}/cancel-item/{itemId}", null)),
                ("fatura",          () => B.Client.GetAsync($"/api/Invoice/order/{orderId}")),
                ("adres silme",     () => B.Client.DeleteAsync($"/api/Address/delete/{addressId}")),
            };

            foreach (var (ad, cagri) in denemeler)
            {
                var resp = await cagri();
                resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ad}: tek sozlesme 404 olmali");

                var body = await resp.Content.ReadAsStringAsync();
                body.Should().NotContain(A.Email, $"{ad}: govdede sahibin e-postasi SIZMAMALI");
                body.Should().NotContain(orderNumber, $"{ad}: govdede siparis numarasi SIZMAMALI");
                body.Should().NotContain(invoiceNumber, $"{ad}: govdede fatura numarasi SIZMAMALI");
                body.Should().NotContain("Sozlesme testi adresi", $"{ad}: govdede adres metni SIZMAMALI");
            }

            // ISLEM GERCEKTEN OLMADI: hicbir deneme A'nin verisini degistirmemis.
            await using (var son = NewContext())
            {
                (await son.Set<OrderItem>().AsNoTracking().SingleAsync(i => i.id == itemId))
                    .is_cancelled.Should().BeFalse("kalem iptal edilmemis olmali");
                (await son.Set<Address>().AsNoTracking().SingleAsync(a => a.id == addressId))
                    .is_active.Should().BeTrue("adres silinmemis olmali");
            }

            // VAKUM KIRICI: ayni uclar SAHIBI icin calisiyor - yani 404'un sebebi "uc bozuk" degil.
            (await A.Client.GetAsync($"/api/Order/get/{orderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await A.Client.GetAsync($"/api/Invoice/order/{orderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ══ GF-1 / K4 (B-1) - SOZLESMEYI IHLAL EDEN UC NOKTA ══════════════════════════════
        //
        // `SecureControllerBase` "sahiplik ihlalinde tek sozlesme 404" diyor; UC yer 403
        // donuyordu ve ucu de varligi ELE VERIYORDU:
        //   ReturnManager.cs:67          (tarifte anilan)
        //   IyzicoPaymentManager.cs:84   (isim olarak birebir kardes, tarifte YOKTU)
        //   OrderManager.cs:148          (adres sahipligi, tarifte YOKTU)
        // Ucu de bu turda 404'e cekildi. Ustteki bes-uc testinin AYNI kalibi.
        [Fact]
        public async Task K4_UC_SAHIPLIK_NOKTASI_404_DONER_ve_GOVDE_SIZDIRMAZ()
        {
            if (Skipped()) return;
            var (orderId, itemId, urunId) = await PlaceOrderForAAsync();
            var orderNumber = await ReadOrderNumberAsync(orderId);

            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "Ev",
                full_name = "A Musteri",
                phone = "5551112233",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "K4 sahiplik testi adresi",
                is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue("on kosul: A adres ekleyebilmeli");
            int aAdresId;
            await using (var ctx = NewContext())
                aAdresId = (await ctx.Set<Address>().AsNoTracking()
                    .SingleAsync(a => a.customer_id == A.CustomerId)).id;

            var bUrunId = await NewProductAsync();

            var denemeler = new (string ad, Func<Task<HttpResponseMessage>> cagri)[]
            {
                ("iade olusturma (ReturnManager:67)", () => B.Client.PostAsJsonAsync("/api/Return/create", new
                {
                    order_id = orderId, product_id = urunId, size = "M", quantity = 1,
                    reason = (byte)0, description = "", return_type = (byte)0
                })),
                ("odeme baslatma (IyzicoPaymentManager:84)", () => B.Client.PostAsJsonAsync("/api/Payment/initialize", new
                {
                    order_id = orderId
                })),
                ("baskasinin adresine siparis (OrderManager:148)", () => B.Client.PostAsJsonAsync("/api/Order/place", new OrderCreateRequestDto
                {
                    customer_id = B.CustomerId,
                    address_id = aAdresId,
                    coupon_code = "",
                    use_store_credit = 0m,
                    payment_method = 1,
                    items = new() { new OrderItemRequestDto { product_id = bUrunId, size = "M", quantity = 1 } }
                })),
            };

            foreach (var (ad, cagri) in denemeler)
            {
                var resp = await cagri();
                var body = await resp.Content.ReadAsStringAsync();
                resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
                    $"{ad}: sahiplik ihlalinde tek sozlesme 404. Govde: {body}");

                // ALAN BAZLI SIZINTI ASSERT'I (P19 dersi): "403 gelmedi" YETMEZ - govde
                // varligi ELE VERMEMELI. `NotYourOrder` metinleri bu turda SILINDI.
                body.Should().NotContain(orderNumber, $"{ad}: siparis numarasi SIZMAMALI");
                body.Should().NotContain(A.Email, $"{ad}: sahibin e-postasi SIZMAMALI");
                body.Should().NotContain("size ait değil", $"{ad}: 'sahiplik' ima eden metin SIZMAMALI");
                body.Should().NotContain("K4 sahiplik testi adresi", $"{ad}: adres metni SIZMAMALI");
            }

            // ISLEM GERCEKTEN OLMADI
            await using (var son = NewContext())
            {
                (await son.Set<ReturnRequest>().AsNoTracking().CountAsync(r => r.order_id == orderId))
                    .Should().Be(0, "B'nin iade talebi OLUSMAMALI");
                (await son.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == B.CustomerId))
                    .Should().Be(0, "B'nin siparisi OLUSMAMALI");
                (await son.Set<OrderItem>().AsNoTracking().SingleAsync(i => i.id == itemId))
                    .is_cancelled.Should().BeFalse("A'nin kalemi ETKILENMEMELI");
            }

            // VAKUM KIRICI: ayni uclar SAHIBI icin 404 DEGIL - yani 404'un sebebi "uc bozuk" degil.
            // (Sahibi icin 400/409 vb. donebilir; olculen sey 404'UN AYIRT EDICI oldugudur.)
            var aOdeme = await A.Client.PostAsJsonAsync("/api/Payment/initialize", new { order_id = orderId });
            aOdeme.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
                "sahibi icin uc 404 DONMEMELI - aksi halde 404 sahiplikten degil ucun kendisinden gelirdi");

            var aSiparis = await A.Client.PostAsJsonAsync("/api/Order/place", new OrderCreateRequestDto
            {
                customer_id = A.CustomerId,
                address_id = aAdresId,
                coupon_code = "",
                use_store_credit = 0m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = urunId, size = "M", quantity = 1 } }
            });
            aSiparis.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
                "A KENDI adresiyle siparis verebilmeli - adres sahiplik dali sahibini engellememeli");
        }

        // =====================================================================
        // GRUP 2 - Anonim (401) ile kimlikli-yetkisiz (403) AYRI durumlardir
        // =====================================================================

        [Fact]
        public async Task Anonim_401_KimlikliYetkisiz_403_IkisiAyirtEdilir()
        {
            if (Skipped()) return;

            var anon = _hostA!.CreateClient();
            var anonResp = await anon.GetAsync("/api/Account/summary");
            anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "token TASIMAYAN istek 401 almali (kimlik yok)");

            var forbidden = await B.Client.PostAsJsonAsync("/api/Order/admin/list", new { });
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "GECERLI token tasiyan ama yetkisiz istek 403 almali (kimlik var, yetki yok)");

            anonResp.StatusCode.Should().NotBe(forbidden.StatusCode,
                "iki durum AYNI koda dusmemeli - istemci 'giris yap' ile 'yetkin yok' ayrimini yapabilmeli");

            // POZITIF OLAY: ayni token kendi ucunda calisiyor -> 403 token gecersizliginden degil.
            (await B.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.OK, "B kendi hesap ozetini gorebilmeli");
        }

        // =====================================================================
        // GRUP 3 - Musteri token i ile admin / satici uclari
        // =====================================================================

        [Fact]
        public async Task MusteriTokeni_AdminUclarinda_Reddedilir_IslemGERCEKLESMEZ()
        {
            if (Skipped()) return;
            var (orderId, _, _) = await PlaceOrderForAAsync();

            // Fatura siparisle birlikte zaten uretiliyor; olculecek sey "yeni fatura uretilmedi".
            int invoicesBefore;
            await using (var pre = NewContext())
                invoicesBefore = await pre.Set<Invoice>().CountAsync();

            // Sahibi olmak admin yetkisi VERMEZ.
            var byOwner = await A.Client.PatchAsJsonAsync("/api/Order/status",
                new { id = orderId, order_status = (int)OrderStatusEnum.Delivered });
            byOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden, "siparis sahibi de olsa musteri durum degistiremez");

            var byOther = await B.Client.PatchAsJsonAsync("/api/Order/status",
                new { id = orderId, order_status = (int)OrderStatusEnum.Delivered });
            byOther.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var generate = await A.Client.PostAsync($"/api/Invoice/generate/{orderId}", null);
            generate.StatusCode.Should().Be(HttpStatusCode.Forbidden, "fatura uretimi admin isi");

            // ISLEM GERCEKTEN OLMADI: durum degismedi, YENI fatura uretilmedi.
            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId))
                .status.Should().NotBe((byte)OrderStatusEnum.Delivered, "durum degismemis olmali");
            (await ctx.Set<Invoice>().CountAsync())
                .Should().Be(invoicesBefore, "musterinin generate cagrisi YENI fatura uretmemis olmali");
        }

        [Fact]
        public async Task MusteriTokeni_SaticiUclarinda_Reddedilir()
        {
            if (Skipped()) return;

            foreach (var path in new[] { "/api/Seller/dashboard", "/api/Seller/products", "/api/Seller/sales" })
            {
                var r = await A.Client.GetAsync(path);
                r.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{path} musteri token i ile acilmamali");
                (await r.Content.ReadAsStringAsync()).Should().NotContain(A.Email);
            }

            // POZITIF OLAY: ayni istemci musteri ucunda calisiyor.
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // =====================================================================
        // GRUP 4 - Govdedeki customer_id TOKEN tarafindan ezilir
        // =====================================================================

        [Fact]
        public async Task GovdedekiCustomerId_TOKEN_TARAFINDAN_EZILIR_Sepet()
        {
            if (Skipped()) return;
            var productId = await NewProductAsync(price: 70m, stock: 15);

            // A istek atiyor ama govdede B nin id sini soyluyor.
            var add = await A.Client.PostAsJsonAsync("/api/Cart/add", new CartItemRequestDto
            { customer_id = B.CustomerId, product_id = productId, size = "M", quantity = 1 });
            add.IsSuccessStatusCode.Should().BeTrue(
                $"istek reddedilmemeli, govde SESSIZCE yok sayilmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await add.Content.ReadAsStringAsync())}");

            await using var ctx = NewContext();
            var aCartIds = await ctx.Set<Cart>().Where(c => c.customer_id == A.CustomerId)
                .Select(c => c.id).ToListAsync();
            var bCartIds = await ctx.Set<Cart>().Where(c => c.customer_id == B.CustomerId)
                .Select(c => c.id).ToListAsync();

            (await ctx.Set<CartItem>().CountAsync(ci => aCartIds.Contains(ci.cart_id) && ci.product_id == productId))
                .Should().Be(1, "kayit TOKEN sahibine (A) yazilmali");
            (await ctx.Set<CartItem>().CountAsync(ci => bCartIds.Contains(ci.cart_id)))
                .Should().Be(0, "govdede adi gecen B ye HICBIR sey yazilmamali");
        }

        [Fact]
        public async Task GovdedekiCustomerId_TOKEN_TARAFINDAN_EZILIR_Adres()
        {
            if (Skipped()) return;

            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = B.CustomerId,
                title = "Sahte",
                full_name = "Baskasi Adina",
                phone = "5559998877",
                city = "Ankara",
                district = "Cankaya",
                full_address = "Token ezme testi",
                is_default = false
            });
            upsert.IsSuccessStatusCode.Should().BeTrue(
                $"istek reddedilmemeli, govde SESSIZCE yok sayilmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await upsert.Content.ReadAsStringAsync())}");

            await using var ctx = NewContext();
            (await ctx.Set<Address>().CountAsync(a => a.customer_id == A.CustomerId))
                .Should().Be(1, "adres TOKEN sahibine (A) yazilmali");
            (await ctx.Set<Address>().CountAsync(a => a.customer_id == B.CustomerId))
                .Should().Be(0, "govdede adi gecen B ye adres YAZILMAMALI");
        }

        // =====================================================================
        // GRUP 5 - Hesap silme ve silinmis hesabin token i
        // =====================================================================

        // KVKK/GDPR SILME - PII anonimlesir + adres kaskadi calisir.
        //
        // ═══ FIX-1A - PIN ADI DUZELTILDI (davranis DEGISMEDI) ══════════════════════════════
        // Eski ad `DeleteAccount_StepUpISTENMEZ_...` idi ve YANLIS BIR SOZLESME IDDIA EDIYORDU:
        // uc `[RequireRecentAuth]` TASIYOR (FIX-1A'da iki uc da 10 dk'ya hizalandi). Test
        // geciyordu cunku `TestAuthHelper` hemen once giris yapiyor ve `auth_time` TAZE;
        // yani olculen sey "step-up yok" degil, "TAZE token step-up kapisindan gecer".
        // BU PIN NE OLCMEZ: step-up penceresi DOLDUGUNDA reddedildigini olcmez (bunun icin
        // 10 dk beklemek ya da jeton uydurmak gerekirdi - ikisi de bu suitin disinda).
        //
        // Eskiden bu uc HER cagrida 500 doner du: anonimlestirme customers.phone alanina NULL
        // yaziyordu, kolon ise NOT NULL. Ayni tuzak adres defteri kaskadinda da vardi ve yalniz
        // ilki duzeltilseydi 500 oraya kayardi - YARIM SILME (musteri anonim, adresler PII dolu).
        // Bu test ikisini BIRLIKTE dogrular.
        [Fact]
        public async Task DeleteAccount_STEP_UP_TAZE_TOKENLA_GECER_PII_Anonimlesir_AdresKaskadiKorunur()
        {
            if (Skipped()) return;

            // Silinecek hesabin adres defteri OLSUN - kaskad gercekten calisiyor mu gorelim.
            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "Ev",
                full_name = "A Musteri",
                phone = "5551112233",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "Silme testi adresi",
                is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue($"adres eklenebilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await upsert.Content.ReadAsStringAsync())}");

            // POZITIF OLAY: cagridan once hesap gercekten canli ve oturumu var.
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode.Should().Be(HttpStatusCode.OK);
            await using (var pre = NewContext())
                (await pre.Set<UserSession>().CountAsync(s => s.customer_id == A.CustomerId && s.is_active))
                    .Should().BeGreaterThan(0, "giris yapilmis oldugu icin aktif oturum bulunmali");

            // Govde YOK - sifre, ikinci faktor, yeniden kimlik dogrulama hicbiri istenmiyor.
            var del = await A.Client.DeleteAsync("/api/Account/delete");
            del.StatusCode.Should().Be(HttpStatusCode.OK,
                $"silme basarili olmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await del.Content.ReadAsStringAsync())}");

            await using var ctx = NewContext();
            // Musteri satiri: global is_active filtresi pasif satiri gizler - filtresiz okunur.
            var c = await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.id == A.CustomerId);
            c.is_active.Should().BeFalse("hesap pasiflesmeli");
            c.email.Should().NotBe(A.Email.ToLowerInvariant(), "e-posta anonimlestirilmeli");
            c.email.Should().StartWith("deleted_", "anonimlestirme kalibi korunmali");
            c.name.Should().NotBe("Test Musteri", "ad anonimlestirilmeli");
            c.phone.Should().BeNull("telefon NULL yazilmali (Sprint 4: kolon nullable yapildi)");
            c.password_hash.Should().BeEmpty("parola ozeti temizlenmeli");

            // H27 ADRES KASKADI: adres defteri de PII tasir - anonimlesmis ve pasiflesmis olmali.
            var addresses = await ctx.Set<Address>().IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.customer_id == A.CustomerId).ToListAsync();
            addresses.Should().NotBeEmpty("kaskadin uzerinde calistigi adres bulunmali");
            addresses.Should().OnlyContain(a => !a.is_active, "adresler pasiflesmeli");
            addresses.Should().OnlyContain(a => a.phone == null, "adres telefonlari NULL olmali");
            addresses.Should().NotContain(a => a.full_address == "Silme testi adresi", "adres metni temizlenmeli");

            // Oturumlar dusmus olmali.
            (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == A.CustomerId && s.is_active))
                .Should().Be(0, "silme tum oturumlari kapatmali");
        }

        // Silme sonrasi: ayni cagri tekrar edilirse ne olur, ve eski parolayla giris yapilabilir mi.
        [Fact]
        public async Task DeleteAccount_IkinciCagri_Idempotent_VeSilmeSonrasi_LoginREDDEDILIR()
        {
            if (Skipped()) return;

            (await A.Client.DeleteAsync("/api/Account/delete")).StatusCode.Should().Be(HttpStatusCode.OK);

            // IKINCI cagri: hesap artik pasif oldugu icin token hesap-durumu kontrolune takilir.
            // Onemli olan sunucunun COKMEMESI ve verinin bir daha degismemesi.
            string emailAfterFirst;
            await using (var ctx = NewContext())
                emailAfterFirst = (await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(x => x.id == A.CustomerId)).email;

            var ikinci = await A.Client.DeleteAsync("/api/Account/delete");
            ((int)ikinci.StatusCode).Should().BeLessThan(500,
                $"ikinci cagri sunucu hatasi URETMEMELI: {(int)ikinci.StatusCode}");

            await using (var ctx = NewContext())
                (await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(x => x.id == A.CustomerId)).email
                    .Should().Be(emailAfterFirst, "ikinci cagri veriyi TEKRAR degistirmemeli");

            // Silinen hesabin eski parolasiyla giris ARTIK yapilamaz.
            var anon = _hostA!.CreateClient();
            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = A.Email, password = TestAuthHelper.TestPassword });
            login.IsSuccessStatusCode.Should().BeFalse("silinmis hesaba giris yapilamamali");
        }

        // =====================================================================
        // FIX-1A - KVKK & DENETIM IZI EKSENI
        // =====================================================================

        // F1: IKI SILME UCU TEK UYGULAMAYA INDI. Ayni yardimci IKI rota icin de kosar - amac
        // tam olarak budur: rotalar ayrisirsa TEK bir mutasyon (M2) IKISINI BIRDEN kirar.
        //
        // FAZ 1'de OLCULEN ONCE-DURUM: `/api/auth/account` adres defterine HIC dokunmuyordu
        // (silme sonrasi `full_name`/`phone`/`full_address` DOLU, `is_active=TRUE`); ustelik
        // `frontend/api-client.js:258` TAM DA o ucu cagiriyordu. Ayrica IKI UC DE
        // `customer_devices`a dokunmuyor ve `/api/Account/delete` SecurityEvent yazmiyordu.
        [Theory]
        [InlineData("/api/Account/delete")]
        [InlineData("/api/auth/account")]
        public async Task SILME_HANGI_UCTAN_GELIRSE_GELSIN_TUM_PII_KANALLARINI_Kapatir(string yol)
        {
            if (Skipped()) return;

            // ON KOSUL: adres + cihaz GERCEK uclardan olusturulur (elle satir yazilmaz).
            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "Ev",
                full_name = "Fix1A Gercek Ad",
                phone = "5551234567",
                city = "Bursa",
                district = "Nilufer",
                full_address = "Fix1A tam acik adres 45/7",
                zip_code = "16110",
                is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue("adres eklenebilmeli");

            // ── P-H1 (MANTIK-FIX-3 / K1) - PASIF ADRES FIKSTURU ───────────────────────
            // OLCULEN VAKUM: bu pin bugune kadar YALNIZ AKTIF adres yaziyordu, dolayisiyla
            // kusurun ON KOSULU (pasif adres) HIC URETILMIYORDU: global HasQueryFilter
            // hicbir satiri elemiyor, kaskad calisiyor gorunuyor ve pin YESIL kaliyordu.
            // CANLI KANIT (R-H1 once): silinen bir hesabin pasif adresinde ad, telefon,
            // acik adres, sehir, ilce ve posta kodu OKUNABILIR halde KALIYORDU.
            // Ikinci adres GERCEK uctan yazilip GERCEK uctan soft-delete edilir.
            var upsert2 = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "MF3 Pasif",
                full_name = "MF3 Pasif Ad",
                phone = "5559990000",
                city = "Trabzon",
                district = "Ortahisar",
                full_address = "MF3 pasif acik adres 99/1",
                zip_code = "61000",
                is_default = false
            });
            upsert2.IsSuccessStatusCode.Should().BeTrue("ikinci adres eklenebilmeli");

            int pasifAdresId;
            await using (var pre0 = NewContext())
            {
                pasifAdresId = await pre0.Set<Address>().IgnoreQueryFilters().AsNoTracking()
                    .Where(a => a.customer_id == A.CustomerId && a.title == "MF3 Pasif")
                    .Select(a => a.id).SingleAsync();
            }
            (await A.Client.DeleteAsync($"/api/Address/delete/{pasifAdresId}")).IsSuccessStatusCode
                .Should().BeTrue("adres GERCEK uctan soft-delete edilebilmeli");

            var cihazJetonu = $"fix1a-{Guid.NewGuid():N}";
            var dev = await A.Client.PostAsJsonAsync("/api/Device/register", new { device_token = cihazJetonu, platform = (byte)1 });
            dev.IsSuccessStatusCode.Should().BeTrue("cihaz kaydedilebilmeli");

            // VAKUM KIRICI: silmeden ONCE her kanal GERCEKTEN dolu/acik olmali - yoksa
            // asagidaki "kapandi" assert'leri bedava yesil kalirdi.
            await using (var pre = NewContext())
            {
                (await pre.Set<Address>().CountAsync(a => a.customer_id == A.CustomerId && a.is_active))
                    .Should().BeGreaterThan(0, "silmeden once aktif adres bulunmali");
                // P-H1 VAKUM KIRICI: pasif adres GERCEKTEN var ve GERCEKTEN PII tasiyor olmali.
                // Bu iki assert olmadan asagidaki "pasif de anonimlesti" iddiasi bedava dogru olurdu.
                var pasifOnce = await pre.Set<Address>().IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(a => a.id == pasifAdresId);
                pasifOnce.is_active.Should().BeFalse("ikinci adres silmeden once PASIF olmali");
                pasifOnce.full_name.Should().Be("MF3 Pasif Ad", "pasif adres silmeden once PII TASIMALI");
                (await pre.Set<CustomerDevice>().CountAsync(d => d.customer_id == A.CustomerId && d.is_active))
                    .Should().BeGreaterThan(0, "silmeden once aktif cihaz bulunmali");
                (await pre.Set<UserSession>().CountAsync(s => s.customer_id == A.CustomerId && s.is_active))
                    .Should().BeGreaterThan(0, "silmeden once aktif oturum bulunmali");
            }
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode.Should().Be(HttpStatusCode.OK,
                "silmeden once hesap canli olmali");

            var del = await A.Client.DeleteAsync(yol);
            del.StatusCode.Should().Be(HttpStatusCode.OK,
                $"{yol} silme basarili olmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await del.Content.ReadAsStringAsync())}");

            await using var ctx = NewContext();

            // (1) MUSTERI
            var c = await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.id == A.CustomerId);
            c.is_active.Should().BeFalse("hesap pasiflesmeli");
            c.email.Should().NotBe(A.Email.ToLowerInvariant(), "e-posta anonimlestirilmeli");
            c.phone.Should().BeNull("telefon temizlenmeli");
            // F1 - TEK BICIM: iki uctan hangisi cagrilirsa cagrilsin parola alani BOS DIZI olur.
            // AuthManager ikizi buraya RASTGELE bir ozet yaziyordu; ikilik bitti.
            c.password_hash.Should().BeEmpty("parola ozeti TEK BICIMDE (bos dizi) temizlenmeli");
            c.password_salt.Should().BeEmpty("parola tuzu TEK BICIMDE (bos dizi) temizlenmeli");

            // (2) ADRES DEFTERI - F11 dahil (city/district/zip_code de konum verisidir)
            var addresses = await ctx.Set<Address>().IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.customer_id == A.CustomerId).ToListAsync();
            addresses.Should().NotBeEmpty("kaskadin uzerinde calistigi adres bulunmali");
            addresses.Should().OnlyContain(a => !a.is_active, "adresler pasiflesmeli");
            addresses.Should().OnlyContain(a => a.phone == null, "adres telefonlari temizlenmeli");
            addresses.Should().NotContain(a => a.full_address == "Fix1A tam acik adres 45/7", "adres metni temizlenmeli");
            addresses.Should().NotContain(a => a.full_name == "Fix1A Gercek Ad", "adres ad-soyadi temizlenmeli");
            addresses.Should().NotContain(a => a.city == "Bursa", "F11: sehir de konum verisidir, temizlenmeli");
            addresses.Should().NotContain(a => a.district == "Nilufer", "F11: ilce de konum verisidir, temizlenmeli");
            addresses.Should().OnlyContain(a => a.zip_code == null, "F11: posta kodu da temizlenmeli");
            // P-H1: PASIF ADRES DE ANONIMLESMELI. Bu dort assert olmadan pin, kusurun
            // ON KOSULUNU uretse bile onu GORMEZDI - global filtre yalniz OKUMAYI eliyor,
            // yani "pasif satir hic gelmedi" ile "pasif satir temizlendi" AYNI gorunurdu.
            var pasif = addresses.Single(a => a.id == pasifAdresId);
            pasif.full_name.Should().NotBe("MF3 Pasif Ad", "PASIF adresin ad-soyadi da temizlenmeli");
            pasif.phone.Should().BeNull("PASIF adresin telefonu da temizlenmeli");
            pasif.full_address.Should().NotBe("MF3 pasif acik adres 99/1", "PASIF adres metni de temizlenmeli");
            pasif.city.Should().NotBe("Trabzon", "PASIF adresin sehri de temizlenmeli");
            pasif.district.Should().NotBe("Ortahisar", "PASIF adresin ilcesi de temizlenmeli");
            pasif.zip_code.Should().BeNull("PASIF adresin posta kodu da temizlenmeli");

            // (3) CIHAZ - F10: `is_active=false` YETMEZ, kalici tanimlayici YOK EDILMELI
            var devices = await ctx.Set<CustomerDevice>().AsNoTracking()
                .Where(d => d.customer_id == A.CustomerId).ToListAsync();
            devices.Should().NotBeEmpty("cihaz satiri denetim icin KORUNMALI (silinmemeli)");
            devices.Should().OnlyContain(d => !d.is_active, "cihaz baglari kapatilmali");
            devices.Should().NotContain(d => d.device_token == cihazJetonu,
                "F10: device_token KALICI bir cihaz tanimlayicisidir - deger YOK EDILMELI");

            // (4) OTURUM
            (await ctx.Set<UserSession>().CountAsync(s => s.customer_id == A.CustomerId && s.is_active))
                .Should().Be(0, "silme tum oturumlari kapatmali");

            // (5) F12: GUVENLIK DEFTERI - eskiden YALNIZ auth ucu yaziyordu
            (await ctx.Set<SecurityEvent>().CountAsync(e => e.event_type == "AccountDeleted" && e.customer_id == A.CustomerId))
                .Should().Be(1, $"F12: {yol} guvenlik defterine TAM 1 iz birakmali");

            // (6) CACHE: silinen hesabin ELDEKI access token'i ANINDA reddedilmeli (TTL beklenmeden)
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized, "hesap durumu cache'i dusurulmeli");
        }

        // ── P-H2) MANTIK-FIX-3 / K2 - ABONELIK E-POSTASI SILMEYLE GIDER [KVKK] ────────
        //
        // OLCULEN YAPISAL BOSLUK: stock_notification_requests ve price_drop_subscriptions
        // tablolarinda `customer_id` KOLONU YOK, bu yuzden KVKK silme yolu onlari
        // YAPISAL OLARAK bulamiyordu; silinen hesabin GERCEK e-postasi orada KALIYORDU.
        // Iki bagimsiz on olcum ajani ayni sonuca vardi.
        //
        // NEGATIF BACAK PININ ICINDE: FARKLI e-postali bir abone AYNEN KALMALI. Bu olmadan
        // "hepsini sil" gibi bir uygulama da pini gecerdi - ve o uygulama, kanonik eksende
        // BASKA MUSTERILERIN aboneligini silen gercek bir VERI-BOZAN kusur olurdu
        // (bu veritabaninda uc musterinin tek kanonik kutuyu paylastigi olculdu).
        [Fact]
        [Trait("Category", "Sql")]
        public async Task SILME_ABONELIK_KAYITLARINI_DA_Kaldirir_FARKLI_EPOSTALI_ABONE_KALIR()
        {
            if (Skipped()) return;

            var urunId = await UrunIdAsync();
            var kontrolEposta = $"ph2.kontrol.{Guid.NewGuid():N}@example.com".ToLowerInvariant();

            // ON KOSUL: abonelikler GERCEK ANONIM uclardan kurulur (elle satir yazilmaz).
            (await A.Client.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "M", email = A.Email })).IsSuccessStatusCode
                .Should().BeTrue("stok bildirimi aboneligi kurulabilmeli");
            (await A.Client.PostAsJsonAsync("/api/price-drop/subscribe",
                new { product_id = urunId, email = A.Email })).IsSuccessStatusCode
                .Should().BeTrue("fiyat dususu aboneligi kurulabilmeli");
            (await A.Client.PostAsJsonAsync("/api/StockNotification/subscribe",
                new { product_id = urunId, size = "L", email = kontrolEposta })).IsSuccessStatusCode
                .Should().BeTrue("KONTROL abonesi kurulabilmeli");

            var eposta = A.Email.ToLowerInvariant();

            // VAKUM KIRICI: silmeden ONCE her uc kayit da GERCEKTEN var olmali.
            await using (var pre = NewContext())
            {
                (await pre.Set<StockNotificationRequest>().CountAsync(s => s.email == eposta))
                    .Should().Be(1, "silmeden once musterinin stok abonesi bulunmali");
                (await pre.Set<PriceDropSubscription>().CountAsync(s => s.email == eposta))
                    .Should().Be(1, "silmeden once musterinin fiyat abonesi bulunmali");
                (await pre.Set<StockNotificationRequest>().CountAsync(s => s.email == kontrolEposta))
                    .Should().Be(1, "silmeden once KONTROL abonesi bulunmali");
            }

            (await A.Client.DeleteAsync("/api/Account/delete")).StatusCode
                .Should().Be(HttpStatusCode.OK, "KVKK silme basarili olmali");

            await using var ctx = NewContext();

            // ASIL IDDIA: silinen musterinin abonelikleri GITTI.
            (await ctx.Set<StockNotificationRequest>().CountAsync(s => s.email == eposta))
                .Should().Be(0, "silinen musterinin stok aboneligi KALMAMALI");
            (await ctx.Set<PriceDropSubscription>().CountAsync(s => s.email == eposta))
                .Should().Be(0, "silinen musterinin fiyat aboneligi KALMAMALI");

            // NEGATIF BACAK: baskasinin aboneligi DOKUNULMAMIS olmali.
            (await ctx.Set<StockNotificationRequest>().CountAsync(s => s.email == kontrolEposta))
                .Should().Be(1, "FARKLI e-postali abone AYNEN kalmali - temizlik DAR olmali");

            // SIRA KANITI: temizlik e-posta anonimlestirilmeden ONCE kosmus olmali. Sonra
            // kossaydi `deleted_<id>@...` arar, HICBIR SATIR bulamaz ve SESSIZCE gecerdi;
            // o durumda yukaridaki iki assert de kirilirdi - ama bu assert sebebi ADIYLA soyler.
            var c = await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.id == A.CustomerId);
            c.email.Should().NotBe(eposta, "e-posta anonimlestirilmis olmali (temizlik ONDEN kosar)");
        }

        // P-H2 yardimcisi: abonelik uclarinin ihtiyac duydugu GERCEK bir urun kimligi.
        // Bu sinif her testte veritabanini DUSURUP KURUYOR, dolayisiyla urun de burada
        // uretilir (depodaki ortak kurgu yardimcisiyla - elle satir yazilmaz).
        private static async Task<int> UrunIdAsync()
        {
            await using var ctx = NewContext();
            return await TestVeriKurgusu.GercekUrunAsync(ctx);
        }

        // F2: DENETIM IZI SIR TASIMAZ ve YALNIZ DEGISEN ALANI TASIR.
        //
        // BU PIN INTERCEPTOR'LI HOST'TAN KOSAR (bkz. IdorFactory'deki VAKUM KANITI). Duz bir
        // test fabrikasinda `AuditInterceptor` HIC calismaz ve bu assert'ler bedava yesil olurdu.
        //
        // FAZ 1'de OLCULEN ONCE-DURUM: change-password'un urettigi audit satiri 2154 bayt,
        // 35 alan; `password_hash.old` ve `password_hash.new` (88'er karakter) FARKLI degerlerle
        // DOLUYDU, `password_salt` 357 karakterdi.
        [Fact]
        public async Task DENETIM_IZI_SIR_ALANI_TASIMAZ_ve_YALNIZ_DEGISEN_ALANI_Tasir()
        {
            if (Skipped()) return;

            var chg = await A.Client.PostAsJsonAsync("/api/Account/change-password",
                new { current_password = TestAuthHelper.TestPassword, new_password = YeniGecerliSifre });
            chg.StatusCode.Should().Be(HttpStatusCode.OK,
                $"sifre degisimi basarili olmali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await chg.Content.ReadAsStringAsync())}");

            await using var ctx = NewContext();
            var musteriId = A.CustomerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var satirlar = await ctx.Set<AuditLog>().AsNoTracking()
                .Where(a => a.table_name == "Customer" && a.entity_id == musteriId && a.changes != null)
                .ToListAsync();

            // VAKUM KIRICI: interceptor GERCEKTEN kosmus olmali. Bu assert olmadan, denetim
            // kaydi hic uretilmedigi durumda da asagidaki "sir yok" iddiasi yesil kalirdi.
            satirlar.Should().NotBeEmpty("AuditInterceptor bu host'ta kosmali ve denetim satiri uretmeli");

            // SIR ALANI LISTESI PIN'IN KENDISINDE - `DenetimGizlilik.SirMi`e SORULMAZ.
            // GEREKCE (FIX-1A 5. kontrolunde OLCULDU): ilk yazimda pin sir olup olmadigini
            // uretim sinifina soruyordu. M1 mutasyonu (`password_hash` kara listeden cikarildi)
            // TAM 0 kirmizi verdi - cunku alan artik "sir degil" sayilinca assert onu ATLIYORDU.
            // Kendi test ettigi kaynaga soran pin, tam da onemli mutasyonda VAKUMA duser.
            var sirAlanlari = new[]
            {
                "password_hash", "password_salt", "two_factor_secret", "two_factor_code",
                "email_verification_token", "password_reset_token", "refresh_token", "device_token"
            };

            // EN GUCLU, KAYNAKTAN TAMAMEN BAGIMSIZ ASSERT: musterinin GERCEK parola ozeti/tuzu
            // denetim izinde HICBIR bicimde gecmemeli. Liste degisse de bu assert ayakta kalir.
            var c = await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.id == A.CustomerId);
            var ozetB64 = Convert.ToBase64String(c.password_hash);
            var tuzB64 = Convert.ToBase64String(c.password_salt);
            ozetB64.Length.Should().BeGreaterThan(20, "vakum kirici: gercek bir parola ozeti okunmus olmali");
            foreach (var s in satirlar)
            {
                s.changes.Should().NotContain(ozetB64, "GERCEK parola ozeti denetim izinde GECMEMELI");
                s.changes.Should().NotContain(tuzB64, "GERCEK parola tuzu denetim izinde GECMEMELI");
            }

            foreach (var s in satirlar)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(s.changes!);
                var alanlar = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

                // (1) SIR ALANLARI: deger HIC girmez. Alan adi gorunse bile degeri sabit isarettir.
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (!sirAlanlari.Contains(p.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    p.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object,
                        $"'{p.Name}' icin beklenen bicim {{old,new}}");
                    foreach (var cift in p.Value.EnumerateObject())
                        cift.Value.GetString().Should().Be(Divisima.Core.Security.DenetimGizlilik.Isaret,
                            $"'{p.Name}' bir SIR alanidir - degeri (uzunlugu/ozeti/kirpilmisi DAHIL) denetim kaydina GIRMEMELI");
                }

                // (1b) KAYNAK SOZLESMESI (durust etiket: DAVRANIS DEGIL): uretim kara listesi
                // yukaridaki adlarin HEPSINI kapsamali. Liste daraltilirsa BU assert kirilir.
                foreach (var ad in sirAlanlari)
                    Divisima.Core.Security.DenetimGizlilik.SirMi(ad).Should().BeTrue(
                        $"'{ad}' uretim kara listesinden CIKARILAMAZ");

                // (2) YALNIZ DEGISEN ALAN: sifre degisimi ad/e-posta/bakiye alanlarina DOKUNMAZ.
                // Eskiden DAL'in `Update()` cagrisi tum varligi Modified isaretledigi icin 35 alan
                // yaziliyordu; artik olcut `OriginalValue != CurrentValue`.
                alanlar.Should().NotContain("email", "sifre degisimi e-postayi degistirmez - payload'da OLMAMALI");
                alanlar.Should().NotContain("name", "sifre degisimi adi degistirmez - payload'da OLMAMALI");
                alanlar.Should().NotContain("loyalty_points", "sifre degisimi puani degistirmez - payload'da OLMAMALI");
                alanlar.Count.Should().BeLessThan(10,
                    $"tam-varlik payload'i bitmeli (once 35 alandi), bulunan: {string.Join(",", alanlar)}");
            }
        }

        // F3: SILMEDEN SONRA O MUSTERININ DENETIM IZINDE PII KALMAZ - AMA SATIR SILINMEZ.
        // Iz korunur (id/action/entity_id/created_at/user_id + ALAN ADLARI), yalnizca DEGERLER gider.
        //
        // FAZ 1'de OLCULEN ONCE-DURUM: silinen hesabin e-postasi 2, adi 3, telefonu 9, acik adres
        // metni 1 satirda audit_logs'ta KALIYORDU; `DeleteAccount` icinde audit_logs'a dokunan
        // TEK SATIR YOKTU. Ustelik silme isleminin KENDI audit satiri `old` degerlerinde silinen
        // PII'yi yeniden kaydediyordu - bu yuzden redaksiyon anonimlestirmeden SONRA kosar.
        [Fact]
        public async Task SILME_SONRASI_DENETIM_IZINDE_PII_KALMAZ_ama_SATIR_SILINMEZ()
        {
            if (Skipped()) return;

            var acikAdres = $"Fix1A redaksiyon adresi {Guid.NewGuid():N}";
            var upsert = await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = A.CustomerId,
                title = "Ev",
                full_name = "Fix1A Redaksiyon Ad",
                phone = "5559876543",
                city = "Izmir",
                district = "Konak",
                full_address = acikAdres,
                is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue("adres eklenebilmeli");
            // Adresi bir kez GUNCELLE: `Modified` satiri (yani `changes` DOLU olan tur) uretilsin.
            int adresId;
            await using (var adrCtx = NewContext())
                adresId = await adrCtx.Set<Address>().AsNoTracking()
                    .Where(a => a.customer_id == A.CustomerId).Select(a => a.id).FirstAsync();
            (await A.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                id = adresId,
                customer_id = A.CustomerId,
                title = "Ev2",
                full_name = "Fix1A Redaksiyon Ad",
                phone = "5559876543",
                city = "Izmir",
                district = "Konak",
                full_address = acikAdres,
                is_default = true
            })).IsSuccessStatusCode.Should().BeTrue("adres guncellenebilmeli");

            int oncekiSatir;
            await using (var pre = NewContext())
            {
                var tablolar = Divisima.Core.Security.DenetimGizlilik.RedaksiyonTablolari.ToArray();
                oncekiSatir = await pre.Set<AuditLog>().CountAsync(a => tablolar.Contains(a.table_name));
                // VAKUM KIRICI: silmeden ONCE denetim izinde GERCEKTEN redakte edilecek deger olmali.
                var oncekiler = await pre.Set<AuditLog>().AsNoTracking()
                    .Where(a => a.table_name == "Address" && a.changes != null).ToListAsync();
                oncekiler.Should().Contain(a => Divisima.Core.Security.DenetimRedaksiyonu.RedakteEdilmemisDegerVarMi(a.changes),
                    "silmeden once denetim izinde redakte edilmemis kisisel deger BULUNMALI");
            }

            (await A.Client.DeleteAsync("/api/Account/delete")).StatusCode.Should().Be(HttpStatusCode.OK);

            await using var ctx = NewContext();
            var adresIdleri = await ctx.Set<Address>().AsNoTracking()
                .Where(a => a.customer_id == A.CustomerId)
                .Select(a => a.id.ToString()).ToListAsync();
            var oturumIdleri = await ctx.Set<UserSession>().AsNoTracking()
                .Where(s => s.customer_id == A.CustomerId)
                .Select(s => s.id.ToString()).ToListAsync();
            var musteriId = A.CustomerId.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var izler = await ctx.Set<AuditLog>().AsNoTracking()
                .Where(a => a.changes != null &&
                            ((a.table_name == "Customer" && a.entity_id == musteriId)
                             || (a.table_name == "Address" && adresIdleri.Contains(a.entity_id))
                             || (a.table_name == "UserSession" && oturumIdleri.Contains(a.entity_id))))
                .ToListAsync();

            izler.Should().NotBeEmpty("redaksiyon SATIR SILMEZ - iz ayakta kalmali");
            foreach (var iz in izler)
            {
                Divisima.Core.Security.DenetimRedaksiyonu.RedakteEdilmemisDegerVarMi(iz.changes)
                    .Should().BeFalse($"audit id={iz.id} ({iz.table_name}) redakte edilmemis kisisel deger tasiyor");
            }

            // Ham metin duzeyinde de kontrol: acik adres ve ad-soyad HICBIR satirda gecmemeli.
            (await ctx.Set<AuditLog>().CountAsync(a => a.changes != null && a.changes.Contains(acikAdres)))
                .Should().Be(0, "silinen hesabin acik adresi denetim izinde KALMAMALI");
            (await ctx.Set<AuditLog>().CountAsync(a => a.changes != null && a.changes.Contains("Fix1A Redaksiyon Ad")))
                .Should().Be(0, "silinen hesabin ad-soyadi denetim izinde KALMAMALI");

            // CIFT-ANLAM KIRICI: redaksiyon SATIR SILEREK yapilmis olmamali - toplam satir sayisi
            // AZALMAMALI (silme kendi satirlarini da EKLER, bu yuzden ">= onceki").
            var tablolar2 = Divisima.Core.Security.DenetimGizlilik.RedaksiyonTablolari.ToArray();
            (await ctx.Set<AuditLog>().CountAsync(a => tablolar2.Contains(a.table_name)))
                .Should().BeGreaterThanOrEqualTo(oncekiSatir, "redaksiyon satir SILMEMELI");

            // CIFT-ANLAM KIRICI: iz gercekten korunmus olmali - alan ADLARI duruyor.
            izler.Should().Contain(i => i.action == "Modified", "action alani korunmali");
            izler.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.entity_id), "entity_id korunmali");
        }

        // F3 - REDAKSIYONUN IZOLASYONU: yalniz SILINEN musteriye dokunur.
        //
        // A silinir, B SILINMEZ. B'nin denetim izi ONCE/SONRA BIREBIR ayni kalmali.
        // OLCUT SATIR SAYISI DEGIL, `changes` ICERIKLERIDIR: redaksiyon zaten satir silmiyor,
        // dolayisiyla sayi esitligi ZAYIF bir olcuttur - degeri isaretle degistiren bir tasma
        // sayiyi hic degistirmeden B'nin PII'sini yok ederdi ve sayi bazli bir pin bunu GORMEZDI.
        // Karsilastirma ORDINAL ve id bazli (id -> changes haritasi).
        //
        // AYNI TEST TEK KOSUMDA IKI YONU DE GORUR (vakum kirici): A'nin PII'si GITMIS,
        // B'ninki DURUYOR olmali. Yalniz "B bozulmadi" diyen bir pin, redaksiyon HIC
        // CALISMASA da yesil kalirdi.
        [Fact]
        public async Task REDAKSIYON_YALNIZ_SILINEN_MUSTERIYE_DOKUNUR_BASKASININ_IZI_BOZULMAZ()
        {
            if (Skipped()) return;

            const string aAdi = "A Silinecek Ad";
            const string bAdi = "B Korunacak Ad";
            var bAcikAdres = $"B korunacak adres {Guid.NewGuid():N}";
            var bCihazJetonu = $"b-cihaz-{Guid.NewGuid():N}";

            // A: denetim izinde PII olussun (profil adi degisimi -> Customer/Modified + `name`)
            (await A.Client.PutAsJsonAsync("/api/Account/profile", new { name = aAdi, phone = "5550000001" }))
                .IsSuccessStatusCode.Should().BeTrue("A profil guncellemesi basarili olmali");

            // B: DORT eksende de iz birakir - Customer / Address / CustomerDevice / UserSession
            (await B.Client.PutAsJsonAsync("/api/Account/profile", new { name = bAdi, phone = "5559990000" }))
                .IsSuccessStatusCode.Should().BeTrue("B profil guncellemesi basarili olmali");
            (await B.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                customer_id = B.CustomerId,
                title = "Ev",
                full_name = bAdi,
                phone = "5559990000",
                city = "Antalya",
                district = "Muratpasa",
                full_address = bAcikAdres,
                is_default = true
            })).IsSuccessStatusCode.Should().BeTrue("B adresi eklenebilmeli");
            int bAdresId;
            await using (var bc = NewContext())
                bAdresId = await bc.Set<Address>().AsNoTracking()
                    .Where(a => a.customer_id == B.CustomerId).Select(a => a.id).FirstAsync();
            // Adresi GUNCELLE ki Address/Modified satiri (changes DOLU olan tur) olussun.
            (await B.Client.PostAsJsonAsync("/api/Address/upsert", new AddressRequestDto
            {
                id = bAdresId,
                customer_id = B.CustomerId,
                title = "Ev - guncel",
                full_name = bAdi,
                phone = "5559990000",
                city = "Antalya",
                district = "Muratpasa",
                full_address = bAcikAdres,
                is_default = true
            })).IsSuccessStatusCode.Should().BeTrue("B adresi guncellenebilmeli");
            (await B.Client.PostAsJsonAsync("/api/Device/register", new { device_token = bCihazJetonu, platform = (byte)1 }))
                .IsSuccessStatusCode.Should().BeTrue("B cihazi kaydedilebilmeli");
            // Cihaz uzerinde de Modified satiri olussun.
            (await B.Client.PostAsJsonAsync("/api/Device/unregister", new { device_token = bCihazJetonu }))
                .IsSuccessStatusCode.Should().BeTrue("B cihazi pasiflestirilebilmeli");

            // ── ONCE: B'nin denetim izini id -> changes olarak fotografla ────────────────────
            async Task<Dictionary<int, string>> BIziniOkuAsync()
            {
                await using var c = NewContext();
                var adresler = await c.Set<Address>().AsNoTracking()
                    .Where(a => a.customer_id == B.CustomerId).Select(a => a.id.ToString()).ToListAsync();
                var oturumlar = await c.Set<UserSession>().AsNoTracking()
                    .Where(s => s.customer_id == B.CustomerId).Select(s => s.id.ToString()).ToListAsync();
                var cihazlar = await c.Set<CustomerDevice>().AsNoTracking()
                    .Where(d => d.customer_id == B.CustomerId).Select(d => d.id.ToString()).ToListAsync();
                var bId = B.CustomerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return await c.Set<AuditLog>().AsNoTracking()
                    .Where(a => a.changes != null &&
                                ((a.table_name == "Customer" && a.entity_id == bId)
                                 || (a.table_name == "Address" && adresler.Contains(a.entity_id))
                                 || (a.table_name == "UserSession" && oturumlar.Contains(a.entity_id))
                                 || (a.table_name == "CustomerDevice" && cihazlar.Contains(a.entity_id))))
                    .ToDictionaryAsync(a => a.id, a => a.changes!);
            }

            var bOnce = await BIziniOkuAsync();
            // VAKUM KIRICI (1): B'nin izi GERCEKTEN dolu ve GERCEKTEN kisisel deger tasiyor olmali.
            bOnce.Should().NotBeEmpty("B'nin denetim izi silmeden once dolu olmali");
            bOnce.Values.Should().Contain(v => v.Contains(bAdi, StringComparison.Ordinal),
                "B'nin adi silmeden once denetim izinde OKUNABILIR olmali");
            bOnce.Values.Should().Contain(v => Divisima.Core.Security.DenetimRedaksiyonu.RedakteEdilmemisDegerVarMi(v),
                "B'nin izinde redakte EDILMEMIS kisisel deger bulunmali (yoksa 'bozulmadi' iddiasi bedava olurdu)");

            // ── A SILINIR ────────────────────────────────────────────────────────────────────
            (await A.Client.DeleteAsync("/api/Account/delete")).StatusCode.Should().Be(HttpStatusCode.OK);

            // ── SONRA: B'nin izi BIREBIR AYNI olmali ─────────────────────────────────────────
            var bSonra = await BIziniOkuAsync();
            bSonra.Keys.Should().BeEquivalentTo(bOnce.Keys, "B'nin denetim satirlari ne eklenmeli ne silinmeli");
            foreach (var kv in bOnce)
                bSonra[kv.Key].Should().Be(kv.Value,
                    $"B'nin audit id={kv.Key} satirinin `changes` icerigi A'nin silinmesinden ETKILENMEMELI");

            // B'nin PII'si HALA OKUNABILIR - redaksiyon eksen disina TASMADI.
            bSonra.Values.Should().Contain(v => v.Contains(bAdi, StringComparison.Ordinal),
                "B'nin adi A'nin silinmesinden SONRA da denetim izinde okunabilir olmali");

            await using var ctx = NewContext();
            var b = await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.id == B.CustomerId);
            b.name.Should().Be(bAdi, "B'nin adi bozulmamali");
            b.email.Should().Be(B.Email.ToLowerInvariant(), "B'nin e-postasi bozulmamali");
            b.phone.Should().Be("5559990000", "B'nin telefonu bozulmamali");
            b.is_active.Should().BeTrue("B'nin hesabi acik kalmali");

            // AYNI OLCUT entity=Address ve entity=CustomerDevice icin de gecerli.
            var bAdres = await ctx.Set<Address>().IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.id == bAdresId);
            bAdres.full_name.Should().Be(bAdi, "B'nin adres ad-soyadi bozulmamali");
            bAdres.full_address.Should().Be(bAcikAdres, "B'nin acik adresi bozulmamali");
            bAdres.city.Should().Be("Antalya", "B'nin sehri bozulmamali");
            bAdres.district.Should().Be("Muratpasa", "B'nin ilcesi bozulmamali");
            bAdres.phone.Should().Be("5559990000", "B'nin adres telefonu bozulmamali");
            bAdres.is_active.Should().BeTrue("B'nin adresi pasiflestirilmemeli");

            var bCihaz = await ctx.Set<CustomerDevice>().AsNoTracking()
                .SingleAsync(d => d.customer_id == B.CustomerId);
            bCihaz.device_token.Should().Be(bCihazJetonu, "B'nin cihaz jetonu YOK EDILMEMELI");

            // ── VAKUM KIRICI (2): AYNI KOSUMDA A'nin PII'si GITMIS olmali ────────────────────
            var aId = A.CustomerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var aIzler = await ctx.Set<AuditLog>().AsNoTracking()
                .Where(a => a.table_name == "Customer" && a.entity_id == aId && a.changes != null).ToListAsync();
            aIzler.Should().NotBeEmpty("A'nin denetim izi ayakta kalmali (satir silinmez)");
            aIzler.Should().OnlyContain(a => !a.changes!.Contains(aAdi, StringComparison.Ordinal),
                "A'nin adi denetim izinden GITMIS olmali - redaksiyon GERCEKTEN kosmus olmali");
        }
    }
}
