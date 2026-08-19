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

        private sealed class IdorFactory : WebApplicationFactory<Program>
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
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
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
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
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
                name = "IDOR Urun", brand = "T", category_id = cat.id, price = price,
                description = "idor testi urunu", color_hex = "#101010",
                product_type = 0, is_active = true, created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            ctx.ProductStocks.Add(new ProductStock
            {
                product_id = p.id, size = "M", stock_quantity = stock, reserved_quantity = 0,
                is_active = true, created_at = DateTime.Now
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
                $"A kendi siparisini verebilmeli: {(int)place.StatusCode} {placeBody}");

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
            theirs.StatusCode.Should().Be(HttpStatusCode.Forbidden, "yabanci siparisin kalemi iptal edilemez");
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
                $"sahibi kendi kalemini iptal edebilmeli: {await mine.Content.ReadAsStringAsync()}");
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
            theirs.StatusCode.Should().Be(HttpStatusCode.Forbidden);

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
                customer_id = A.CustomerId, title = "Ev", full_name = "A Musteri", phone = "5551112233",
                city = "Istanbul", district = "Kadikoy", full_address = "IDOR test adresi", is_default = true
            });
            upsert.IsSuccessStatusCode.Should().BeTrue(
                $"A adres ekleyebilmeli: {await upsert.Content.ReadAsStringAsync()}");

            int addressId;
            await using (var ctx = NewContext())
                addressId = (await ctx.Set<Address>().AsNoTracking()
                    .SingleAsync(a => a.customer_id == A.CustomerId)).id;

            var theirs = await B.Client.DeleteAsync($"/api/Address/delete/{addressId}");
            theirs.StatusCode.Should().Be(HttpStatusCode.Forbidden, "yabanci adres silinemez");

            await using (var ctx = NewContext())
            {
                var addr = await ctx.Set<Address>().AsNoTracking().SingleAsync(a => a.id == addressId);
                addr.is_active.Should().BeTrue("B nin denemesi adresi pasiflestirMEMELI");
                addr.customer_id.Should().Be(A.CustomerId);
            }

            // POZITIF OLAY: sahibi silebiliyor.
            var mine = await A.Client.DeleteAsync($"/api/Address/delete/{addressId}");
            mine.IsSuccessStatusCode.Should().BeTrue(
                $"sahibi kendi adresini silebilmeli: {await mine.Content.ReadAsStringAsync()}");
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
            add.IsSuccessStatusCode.Should().BeTrue($"A sepete ekleyebilmeli: {await add.Content.ReadAsStringAsync()}");

            var wish = await A.Client.PostAsync($"/api/Wishlist/toggle?productId={productId}", null);
            wish.IsSuccessStatusCode.Should().BeTrue($"A favoriye ekleyebilmeli: {await wish.Content.ReadAsStringAsync()}");

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
                $"istek reddedilmemeli, govde SESSIZCE yok sayilmali: {await add.Content.ReadAsStringAsync()}");

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
                customer_id = B.CustomerId, title = "Sahte", full_name = "Baskasi Adina", phone = "5559998877",
                city = "Ankara", district = "Cankaya", full_address = "Token ezme testi", is_default = false
            });
            upsert.IsSuccessStatusCode.Should().BeTrue(
                $"istek reddedilmemeli, govde SESSIZCE yok sayilmali: {await upsert.Content.ReadAsStringAsync()}");

            await using var ctx = NewContext();
            (await ctx.Set<Address>().CountAsync(a => a.customer_id == A.CustomerId))
                .Should().Be(1, "adres TOKEN sahibine (A) yazilmali");
            (await ctx.Set<Address>().CountAsync(a => a.customer_id == B.CustomerId))
                .Should().Be(0, "govdede adi gecen B ye adres YAZILMAMALI");
        }

        // =====================================================================
        // GRUP 5 - Hesap silme ve silinmis hesabin token i
        // =====================================================================

        // URETIM HATASI PINLENIR - DUZELTILMEDI, RAPOR EDILDI.
        // DELETE /api/Account/delete step-up (sifre/2FA) ISTEMIYOR; ama istek 500 ile dusuyor:
        // AccountManager.DeleteAccount anonimlestirme sirasinda customers.phone alanina NULL
        // yaziyor, kolon ise NOT NULL. Sonuc: KVKK/GDPR silme hakki UCTAN UCA CALISMIYOR.
        // Bu test mevcut davranisi kilitler; hata duzeltilince KIRMIZI olur ve guncellenmesi gerekir.
        [Fact]
        public async Task DeleteAccount_500_DONUYOR_HESAP_SILINMIYOR_URETIM_HATASI_PINLENIR()
        {
            if (Skipped()) return;

            // POZITIF OLAY: cagridan once hesap gercekten canli.
            (await A.Client.GetAsync("/api/Account/summary")).StatusCode.Should().Be(HttpStatusCode.OK);

            var del = await A.Client.DeleteAsync("/api/Account/delete");
            del.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
                "mevcut uretim davranisi: silme 500 veriyor (phone NOT NULL ihlali)");

            // Silme GERCEKTEN olmadi - hata kozmetik degil, veri hic degismemis.
            await using var ctx = NewContext();
            var c = await ctx.Set<Customer>().AsNoTracking().SingleAsync(x => x.id == A.CustomerId);
            c.is_active.Should().BeTrue("hesap hala aktif - silme islemedi");
            c.email.Should().Be(A.Email.ToLowerInvariant(), "e-posta anonimlestirilMEMIS");
            c.name.Should().Be("Test Musteri", "ad anonimlestirilMEMIS");
        }

        // MEVCUT DAVRANIS PINLENIR: pasiflestirilmis (askiya alinmis) musterinin ESKI Bearer
        // token i sonraki isteklerde ne oluyor. Silme ucu 500 verdigi icin pasiflestirme
        // dogrudan veritabanindan yapiliyor - admin askiya almasiyla ayni son durum.
        [Fact]
        public async Task PasiflestirilmisMusterinin_ESKI_TOKENI_DAVRANIS_PINLENIR()
        {
            if (Skipped()) return;

            var before = await A.Client.GetAsync("/api/Account/summary");
            before.StatusCode.Should().Be(HttpStatusCode.OK, "pasiflestirmeden once token calisiyor olmali");

            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == A.CustomerId);
                c.is_active = false;
                await ctx.SaveChangesAsync();

                foreach (var s in await ctx.Set<UserSession>().Where(u => u.customer_id == A.CustomerId).ToListAsync())
                    s.is_active = false;
                await ctx.SaveChangesAsync();
            }

            // POZITIF OLAY: pasiflestirme gercekten yazildi.
            await using (var ctx = NewContext())
                (await ctx.Set<Customer>().IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(x => x.id == A.CustomerId)).is_active.Should().BeFalse();

            // AYNI token, pasiflestirme SONRASI.
            // PINLENEN DAVRANIS: token REDDEDILMIYOR. 401 degil 404 geliyor - yani istek kimlik
            // dogrulamasindan GECIYOR, sadece Customer uzerindeki global is_active sorgu filtresi
            // satiri gizledigi icin "bulunamadi" ile bitiyor. Engel yetkilendirme degil, tesadufi.
            var after = await A.Client.GetAsync("/api/Account/summary");
            after.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "pasif hesabin token i 401 ile REDDEDILMIYOR - istek isleniyor, veri filtresine takiliyor");
            after.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
                "guvenli davranis 401 olurdu; mevcut sozlesme bu degil");

            // SIVRI UC: Customer satirini okumayan bir YAZMA ucu pasif token ile hala isliyor mu.
            var productId = await NewProductAsync(price: 55m, stock: 5);
            var write = await A.Client.PostAsync($"/api/Wishlist/toggle?productId={productId}", null);
            write.IsSuccessStatusCode.Should().BeTrue(
                $"pasiflestirilmis musteri hala YAZMA yapabiliyor (mevcut davranis): {await write.Content.ReadAsStringAsync()}");
            await using (var ctx = NewContext())
                (await ctx.Set<WishlistItem>().CountAsync(w => w.customer_id == A.CustomerId))
                    .Should().Be(1, "yazma gercekten veritabanina islendi - reddedilmedi");
        }
    }
}
