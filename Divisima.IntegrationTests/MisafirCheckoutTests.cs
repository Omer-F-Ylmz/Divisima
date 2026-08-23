using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ A3 HIBRIT - MISAFIR CHECKOUT (YALNIZ KAPIDA ODEME) ═════════════════════════════════
    //
    // OLCULEN ONCE-DURUM (kapsama denetimi):
    //   - POST /api/guest-checkout/place VARDI ama storefront'ta cagrisi SIFIRDI.
    //   - GuestCheckoutDto'da payment_method YOKTU -> PlaceOrder varsayilani (Online) aliyor;
    //     /api/payment/initialize ise [RequireUserType(Customer)] ve musteriyi TOKEN'dan
    //     okuyor - misafirin token'i YOK. Yani misafir siparisi OLUSTURULABILIYOR ama ASLA
    //     ODENEMIYOR, sonsuza kadar Pending kaliyordu.
    //   - index.html'in ".co-guest" blogu DOM'DA YOKTU (E2 paneli ustune yaziyor); YASAYAN
    //     TEK VAAT SSS'deydi ve YANLISTI.
    //
    // KULLANICI KARARI (secenek iii): misafire YALNIZ KAPIDA ODEME. Misafire OTURUM VERILMEZ,
    // yetki modeline DOKUNULMAZ - bu projenin defalarca bedelini odedigi sinir hic zorlanmaz.
    [Trait("Category", "Sql")]
    public class MisafirCheckoutTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaMisafirCheckoutTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");
        private const string VitrinTabani = "https://vitrin.divisima.test";
        private const byte KapidaOdeme = 1;
        private const byte OnlineOdeme = 0;
        private const byte HavaleOdeme = 2;

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

        private static readonly List<MailMessageDto> Yakalanan = new();

        private sealed class SahteMail : IMailService
        {
            public Task SendAsync(MailMessageDto message)
            {
                lock (Yakalanan) Yakalanan.Add(message);
                return Task.CompletedTask;
            }
        }

        private sealed class MisafirFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Storefront:BaseUrl", VitrinTabani);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    services.AddScoped<IMailService, SahteMail>();
                });
            }
        }

        private MisafirFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            lock (Yakalanan) Yakalanan.Clear();
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new MisafirFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak misafir checkout testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // ── 1) MISAFIR COD SIPARISI UCTAN UCA ────────────────────────────────────────────
        [Fact]
        public async Task MISAFIR_KAPIDA_ODEME_SIPARISI_Olusur_ve_CONFIRMED_Olur()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"misafir-{Guid.NewGuid():N}@example.com";

            var r = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme));

            var govde = await r.Content.ReadAsStringAsync();
            r.StatusCode.Should().Be(HttpStatusCode.Created, $"misafir COD siparisi olusmali. Govde: {govde}");

            await using var ctx = NewContext();
            var musteri = await ctx.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
            var siparis = await ctx.Set<Order>().AsNoTracking()
                .Where(o => o.customer_id == musteri.id).OrderByDescending(o => o.id).FirstAsync();

            siparis.status.Should().Be((byte)OrderStatusEnum.Confirmed,
                "kapida odeme siparisi ANINDA onaylanir - Pending'de asili KALMAZ (eski halde online "
                + "sayilip odenemedigi icin sonsuza kadar Pending kaliyordu)");
            siparis.payment_type.Should().Be(KapidaOdeme, "payment_method DTO'dan TASINMIS olmali");
            musteri.email_verified.Should().BeFalse("misafir DOGRULANMAMIS bir musteridir");
        }

        // ── 2) MISAFIR ONLINE ODEME DENEYEMEZ ────────────────────────────────────────────
        [Theory]
        [InlineData(OnlineOdeme)]
        [InlineData(HavaleOdeme)]
        public async Task MISAFIR_KAPIDA_ODEME_DISINDAKI_YONTEMI_DENEYEMEZ_UC_REDDEDER(byte yontem)
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"misafir-{Guid.NewGuid():N}@example.com";

            var r = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, yontem));

            r.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "misafir yalnizca kapida odeme kullanabilir");
            var govde = await r.Content.ReadAsStringAsync();
            // CIFT-ANLAM KIRICI: 400 iki ayri sebepten gelebilir. Mesaj SEBEBI soylemeli -
            // ve ozellikle "kartla odemek icin uye girisi" yolunu gostermeli.
            govde.Should().Contain("kapıda ödeme");
            govde.Should().Contain("üye girişi");

            // SESSIZCE COD'A DUSURULMEDIGI de kanit: HICBIR siparis olusmamis olmali.
            await using var ctx = NewContext();
            (await ctx.Set<Customer>().AsNoTracking().AnyAsync(c => c.email == eposta))
                .Should().BeFalse("uc reddettiginde misafir musterisi de OLUSTURULMAMALI");
        }

        // ── 3) MISAFIRE OTURUM VERILMEZ ──────────────────────────────────────────────────
        [Fact]
        public async Task MISAFIRE_TOKEN_DONMEZ_ve_OTURUM_ACILMAZ()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"misafir-{Guid.NewGuid():N}@example.com";

            var r = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme));
            r.StatusCode.Should().Be(HttpStatusCode.Created);

            // ASIL IDDIA: yanit bir kimlik bilgisi TASIMAMALI. A3'un tum gerekcesi bu -
            // dogrulanmamis hesaba oturum verme kapisi HIC ACILMASIN.
            var govde = await r.Content.ReadAsStringAsync();
            govde.Should().NotContain("token", "yanitta jeton alani OLMAMALI");
            r.Headers.Contains("Set-Cookie").Should().BeFalse("misafire oturum cerezi YAZILMAMALI");

            // VE veritabaninda da oturum acilmamis olmali.
            await using var ctx = NewContext();
            var musteri = await ctx.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
            (await ctx.Set<UserSession>().AsNoTracking().AnyAsync(s => s.customer_id == musteri.id))
                .Should().BeFalse("misafir icin oturum satiri OLUSMAMALI");
        }

        // ── 4) MISAFIR HESABINI SAHIPLENEBILSIN: DOGRULAMA MAILI TETIKLENIR ─────────────
        [Fact]
        public async Task MISAFIR_CHECKOUTU_DOGRULAMA_MAILINI_TETIKLER_YENI_UC_ACILMADAN()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"misafir-{Guid.NewGuid():N}@example.com";

            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.Created);

            // Jeton URETILMIS olmali - sahiplenme zincirinin ILK adimi budur.
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
                m.email_verification_token.Should().NotBeNullOrWhiteSpace(
                    "misafir, hesabini sonradan sahiplenebilmek icin dogrulama jetonuna ihtiyac duyar");
            }

            await OutboxBosaltAsync();
            var mail = MailBul("doğrulayın", eposta);
            mail.Should().NotBeNull("misafire dogrulama maili gitmeli");
            mail!.Body.Should().Contain($"{VitrinTabani}/#/dogrula/",
                "tiklanabilir baglanti TEK KAYNAKTAN gelmeli");
        }

        // ── 5) SIPARIS ONAY MAILI MISAFIRE YOL GOSTERIR, UYEYE GEREKSIZ SATIR EKLEMEZ ───
        [Fact]
        public async Task SIPARIS_ONAY_MAILI_MISAFIRE_SIFRE_BELIRLEME_YOLUNU_Soyler()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"misafir-{Guid.NewGuid():N}@example.com";
            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.Created);

            await OutboxBosaltAsync();
            var onay = MailBul("Siparişin alındı", eposta);
            onay.Should().NotBeNull();
            onay!.Body.Should().Contain("şifre belirle",
                "misafirin takip baglantisini kullanabilmesi icin ONCE hesabini sahiplenmesi gerekir");
        }

        [Fact]
        public async Task UYE_SIPARISINDE_SIFRE_BELIRLEME_SATIRI_EKLENMEZ()
        {
            if (Skipped()) return;
            // CIFT-ANLAM KIRICI: satiri HER maile eklemek de yukaridaki testi gecerdi ve
            // dogrulanmis uyeye anlamsiz bir yonerge gonderirdi.
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var (urunId, beden) = await UrunHazirlaAsync();
            var adresId = await AdresHazirlaAsync(musteri.Client, musteri.CustomerId);

            (await musteri.Client.PostAsJsonAsync("/api/order/place", new
            {
                customer_id = musteri.CustomerId,
                address_id = adresId,
                coupon_code = "",
                use_store_credit = 0,
                payment_method = KapidaOdeme,
                items = new[] { new { product_id = urunId, size = beden, quantity = 1 } }
            })).StatusCode.Should().Be(HttpStatusCode.Created);

            await OutboxBosaltAsync();
            var onay = MailBul("Siparişin alındı", musteri.Email);
            onay.Should().NotBeNull();
            onay!.Body.Should().NotContain("şifre belirle",
                "dogrulanmis uyenin zaten sifresi var - bu satir ona anlamsiz gelirdi");
            onay.Body.Should().Contain($"{VitrinTabani}/#/hesabim/siparislerim",
                "uye icin takip baglantisi YINE olmali (vakum kirici)");
        }

        // ── Yardimcilar ─────────────────────────────────────────────────────────────────
        private static MailMessageDto? MailBul(string konuParcasi, string alici)
        {
            lock (Yakalanan)
                return Yakalanan.LastOrDefault(m =>
                    (m.Subject ?? "").Contains(konuParcasi) &&
                    string.Equals(m.To, alici, StringComparison.OrdinalIgnoreCase));
        }

        private static object MisafirGovdesi(string eposta, int urunId, string beden, byte yontem) => new
        {
            guest_name = "Misafir Musteri",
            guest_email = eposta,
            guest_phone = "5550000000",
            city = "Istanbul",
            district = "Kadikoy",
            full_address = "Misafir Mah. 1",
            zip_code = "34710",
            coupon_code = "",
            payment_method = yontem,
            items = new[] { new { product_id = urunId, size = beden, quantity = 1 } }
        };

        private static async Task OutboxBosaltAsync()
        {
            await using var ctx = NewContext();
            var mail = new SahteMail();
            var links = new MailLinkBuilder(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Storefront:BaseUrl"] = VitrinTabani }).Build(),
                NullLogger<MailLinkBuilder>.Instance);
            var handler = new OrderPlacedEmailHandler(mail, new EfCustomerDal(ctx), links,
                NullLogger<OrderPlacedEmailHandler>.Instance);
            var publisher = new OrderPlacedEventPublisher(new IOrderPlacedEventHandler[] { handler });
            var processor = new OutboxProcessor(new EfOutboxMessageDal(ctx), publisher, mail,
                new Divisima.Bussiness.Concrete.OrderStatusHistoryManager(
                    new EfOrderStatusHistoryDal(ctx), new EfOrderDal(ctx)),
                NullLogger<OutboxProcessor>.Instance, new CagrilmayanScopeFactory());
            await processor.ProcessPendingAsync();
        }

        private sealed class CagrilmayanScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
                => throw new NotSupportedException("Misafir pinlerinde odeme dali kullanilmaz.");
        }

        private static async Task<(int UrunId, string Beden)> UrunHazirlaAsync()
        {
            await using var ctx = NewContext();
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            var kat = new Category
            {
                name = "Misafir Kategori " + damga,
                slug = "misafir-kategori-" + damga,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Misafir Urun " + damga,
                description = "misafir checkout pini icin urun",
                color_hex = "#111111",
                brand = "Divisima",
                price = 499.90m,
                category_id = kat.id,
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(urun);
            await ctx.SaveChangesAsync();

            ctx.Set<ProductStock>().Add(new ProductStock
            {
                product_id = urun.id,
                size = "M",
                stock_quantity = 20,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return (urun.id, "M");
        }

        private static async Task<int> AdresHazirlaAsync(HttpClient client, int musteriId)
        {
            (await client.PostAsJsonAsync("/api/address/upsert", new
            {
                title = "Ev",
                full_name = "Uye Musteri",
                phone = "5550000000",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "Uye Mah. 1",
                zip_code = "34710",
                is_default = true
            })).StatusCode.Should().Be(HttpStatusCode.Created);
            await using var ctx = NewContext();
            return (await ctx.Set<Address>().AsNoTracking()
                .Where(a => a.customer_id == musteriId).OrderByDescending(a => a.id).FirstAsync()).id;
        }
    }
}
