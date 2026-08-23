using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.Bussiness.Concrete;
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
    // ══ LAUNCH-FIX DALGA A / A1 - MAIL ALTYAPISI ═══════════════════════════════════════════
    //
    // UC OLCULEN ONCE-DURUM (kapsama denetimi + canli SMTP yakalayicisi):
    //
    //  (a) SAHTE ALICI. OrderPlacedEmailHandler siparis onay mailini
    //      "customer-{id}@divisima.local" adresine gonderiyordu - yani musteriye HIC gitmiyordu.
    //      ".local" yonlendirilemez bir ust alan adidir; gercek SMTP'de gonderim REDDEDILIR.
    //
    //  (b) HATA SIPARISI DUSURUYORDU. Publish COMMIT'TEN SONRA ve TRY BLOGUNUN DISINDA
    //      cagriliyordu; publisher handler'lari try/catch'siz kosuyor; SmtpMailService hatayi
    //      BILINCLI OLARAK FIRLATIYOR. Sonuc: siparis commit olmus haldeyken uc HTTP 500 doner.
    //      Yerel SMTP yakalayicisi OLU BIR PORTA cevrilerek CANLI olculdu.
    //
    //  (c) BAGLANTI YOKTU. Dogrulama/sifre sifirlama mailleri yalnizca ciplak jeton tasiyordu.
    //
    // Bu sinif ucunu de pinler. Mail gonderimi SAHTE bir IMailService ile surulur - gercek SMTP
    // kanitlari (yakalanan .eml govdeleri) dalga raporundadir.
    [Trait("Category", "Sql")]
    public class LaunchFixMailZinciriTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaLaunchFixMailTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");
        private const string VitrinTabani = "https://vitrin.divisima.test";

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

        // ── Sahte SMTP ──────────────────────────────────────────────────────────────────────
        // STATIK: WebApplicationFactory servisleri kendi konteynerinde uretiyor, testin elindeki
        // ornege ulasmanin tasiyici yolu bu. CLAUDE.md bolum 5'teki tuzak (statik bayrak test
        // sinirini asar) bilerek karsilaniyor: InitializeAsync HER TESTTE sifirliyor.
        private static readonly List<MailMessageDto> Yakalanan = new();
        private static bool Patlasin;

        private sealed class SahteMail : IMailService
        {
            public Task SendAsync(MailMessageDto message)
            {
                lock (Yakalanan) Yakalanan.Add(message);
                if (Patlasin) throw new InvalidOperationException("SMTP sunucusuna ulasilamadi (sahte)");
                return Task.CompletedTask;
            }
        }

        private sealed class MailFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                // A1(c): baglanti origin'inin TEK KAYNAGI. Bos birakilsaydi pin, linkin
                // uretilmedigini degil ORIGIN'IN eksikligini olcerdi.
                builder.UseSetting("Storefront:BaseUrl", VitrinTabani);
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    // IMailService Program.cs'te ServiceCollection'a kayitli (IIyzicoClient gibi) -
                    // buradaki kayit SONRA geldigi icin kazanir. Autofac modulundeki servisler icin
                    // ayni sey GECERLI DEGIL (CLAUDE.md bolum 5).
                    services.AddScoped<IMailService, SahteMail>();
                });
            }
        }

        private MailFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            lock (Yakalanan) Yakalanan.Clear();
            Patlasin = false;   // statik bayrak SIFIRLANIR - test siniri asilmasin
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new MailFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak mail zinciri testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private static MailMessageDto? MailBul(string konuParcasi)
        {
            lock (Yakalanan) return Yakalanan.LastOrDefault(m => (m.Subject ?? "").Contains(konuParcasi));
        }

        // LAUNCH-FIX A1(b) eki: auth mailleri artik "EmailNotification" outbox mesaji olarak
        // yaziliyor (uretimde Cron.Minutely bosaltiyor). Pin, bekleyen mesajlari GERCEK isleyiciyle
        // bosaltir - PaymentCallbackSecurityTests'teki OutboxBosaltAsync kalibiyla ayni.
        private static async Task OutboxBosaltAsync()
        {
            await using var ctx = NewContext();
            var processor = new OutboxProcessor(
                new EfOutboxMessageDal(ctx), new CagrilmayanPublisher(), new SahteMail(),
                new OrderStatusHistoryManager(new EfOrderStatusHistoryDal(ctx), new EfOrderDal(ctx)),
                NullLogger<OutboxProcessor>.Instance, new CagrilmayanScopeFactory());
            await processor.ProcessPendingAsync();
        }

        // Bu bosaltmalarda YALNIZ e-posta mesajlari var; siparis dali cagrilirsa GURULTULU duser
        // (sessiz bir sahte, testin yanlis yolu olctugunu gizlerdi).
        private sealed class CagrilmayanPublisher : IOrderPlacedEventPublisher
        {
            public Task PublishAsync(OrderPlacedEvent evt)
                => throw new NotSupportedException("Bu bosaltmada siparis dali beklenmiyor.");
        }

        // ── A1(c): DOGRULAMA MAILI TIKLANABILIR LINK TASIR ───────────────────────────────
        [Fact]
        public async Task DogrulamaMaili_TIKLANABILIR_LINK_Tasir_ve_ORIGIN_TEK_KAYNAKTAN_Gelir()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();
            var eposta = $"dogrulama-{Guid.NewGuid():N}@example.com";

            var r = await client.PostAsJsonAsync("/api/auth/register", new
            {
                name = "Link Musteri",
                email = eposta,
                phone = "5550000000",
                password = "LinkTest2026x",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
            await OutboxBosaltAsync();   // A1(b) eki: auth mailleri outbox uzerinden gidiyor

            var mail = MailBul("doğrulayın");
            mail.Should().NotBeNull("kayit dogrulama maili gonderilmis olmali");
            mail!.To.Should().Be(eposta.ToLowerInvariant(), "mail GERCEK adrese gitmeli");

            // JETON DB'DEN OKUNUR - maildeki linkin gercekten O jetonu tasidigi dogrulanir.
            string jeton;
            await using (var ctx = NewContext())
                jeton = (await ctx.Set<Customer>().AsNoTracking()
                    .FirstAsync(c => c.email == eposta.ToLowerInvariant())).email_verification_token!;

            mail.Body.Should().Contain($"{VitrinTabani}/#/dogrula/",
                "govde beyan edilen VITRIN origin'ini tasimali - ikinci bir sabit origin YOK");
            mail.Body.Should().Contain(jeton, "link o hesabin GERCEK jetonunu tasimali");

            // CIFT-ANLAM KIRICI: jeton govdede AYRICA duz kod olarak da kalmali. Giris ekranindaki
            // mevcut dogrulama kutusu (E1'den beri calisan yol) buna dayaniyor; link EK bir yoldur.
            mail.Body.Should().Contain("doğrulama kutusuna şu kodu gir",
                "yedek yol (kodu elle girme) KORUNMALI");
        }

        // ── A1(c) + A2: SIFRE SIFIRLAMA MAILI ────────────────────────────────────────────
        [Fact]
        public async Task SifreSifirlamaMaili_TIKLANABILIR_LINK_Tasir_ve_SURE_SINIRINI_Soyler()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();
            var eposta = $"sifirla-{Guid.NewGuid():N}@example.com";
            (await client.PostAsJsonAsync("/api/auth/register", new
            {
                name = "Sifirla Musteri",
                email = eposta,
                phone = "5550000000",
                password = "LinkTest2026x",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            })).StatusCode.Should().Be(HttpStatusCode.Created);

            var r = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = eposta });
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            await OutboxBosaltAsync();

            var mail = MailBul("Şifre sıfırlama");
            mail.Should().NotBeNull("sifre sifirlama maili gonderilmis olmali");
            mail!.To.Should().Be(eposta.ToLowerInvariant());

            string jeton;
            await using (var ctx = NewContext())
                jeton = (await ctx.Set<Customer>().AsNoTracking()
                    .FirstAsync(c => c.email == eposta.ToLowerInvariant())).password_reset_token!;

            mail.Body.Should().Contain($"{VitrinTabani}/#/sifre-sifirla/{Uri.EscapeDataString(jeton)}",
                "link TAM olarak sifirlama rotasina ve o jetona gitmeli");
            mail.Body.Should().Contain("30 dakika", "sure siniri kullaniciya soylenmeli");
        }

        // ── A1(a): SIPARIS ONAY MAILI GERCEK ADRESE ──────────────────────────────────────
        [Fact]
        public async Task SiparisOnayMaili_GERCEK_MUSTERI_ADRESINE_Gider_SahteYerelAdres_YOK()
        {
            if (Skipped()) return;
            var eposta = $"siparis-{Guid.NewGuid():N}@example.com";
            int musteriId;
            await using (var ctx = NewContext())
            {
                var m = new Customer
                {
                    name = "Siparis Musteri",
                    email = eposta,
                    phone = "5550000000",
                    password_hash = new byte[] { 1 },
                    password_salt = new byte[] { 2 },
                    user_type = (byte)UserTypeEnum.Customer,
                    is_active = true,
                    email_verified = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Customer>().Add(m);
                await ctx.SaveChangesAsync();
                musteriId = m.id;
            }

            var mail = new SahteMail();
            await using (var ctx = NewContext())
            {
                var handler = new OrderPlacedEmailHandler(mail, new EfCustomerDal(ctx),
                    new MailLinkBuilder(VitrinYapilandirmasi(), NullLogger<MailLinkBuilder>.Instance),
                    NullLogger<OrderPlacedEmailHandler>.Instance);
                await handler.HandleAsync(new OrderPlacedEvent
                {
                    order_id = 1,
                    customer_id = musteriId,
                    order_number = "DVS-TEST-0001",
                    total = 1049.70m
                });
            }

            var gonderilen = MailBul("Siparişin alındı");
            gonderilen.Should().NotBeNull();
            gonderilen!.To.Should().Be(eposta, "alici SIPARISIN GERCEK MUSTERISI olmali");
            // OLCULEN ONCE-DURUMUN TA KENDISI: "customer-{id}@divisima.local"
            gonderilen.To.Should().NotContain("divisima.local",
                "sahte yerel adres tamamen KALKMALI - o adres gercek SMTP'de REDDEDILIR");
            gonderilen.Body.Should().Contain("DVS-TEST-0001", "siparis numarasi govdede olmali");
            gonderilen.Body.Should().Contain($"{VitrinTabani}/#/hesabim/siparislerim",
                "takip baglantisi TEK KAYNAKTAN gelmeli");
        }

        // ── A1(b): SMTP PATLASA DA SIPARIS UCU 201 DONER ─────────────────────────────────
        [Fact]
        public async Task SMTP_PATLARSA_SiparisUcu_201_Doner_ve_Kayip_OUTBOXTA_GORUNUR()
        {
            if (Skipped()) return;
            Patlasin = true;   // HER gonderim istisna atar

            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var (urunId, beden) = await UrunHazirlaAsync();
            var adresId = await AdresHazirlaAsync(musteri.Client, musteri.CustomerId);

            var r = await musteri.Client.PostAsJsonAsync("/api/order/place", new
            {
                customer_id = musteri.CustomerId,   // CLAUDE.md bolum 5: validator token'dan ONCE kosar
                address_id = adresId,
                coupon_code = "",
                use_store_credit = 0,
                payment_method = (byte)1,           // kapida odeme -> Confirmed
                items = new[] { new { product_id = urunId, size = beden, quantity = 1 } }
            });

            // ASIL IDDIA: mail zinciri tamamen bozukken bile siparis ucu BASARILI doner.
            var yanitGovdesi = await r.Content.ReadAsStringAsync();
            r.StatusCode.Should().Be(HttpStatusCode.Created,
                $"SMTP hatasi siparis yanitini ETKILEMEMELI - onceki halde bu 500 donuyordu. Govde: {yanitGovdesi}");

            // VE KAYIP SESSIZ DEGIL: olay outbox'ta duruyor, yeniden denenecek.
            await using var ctx = NewContext();
            var mesaj = await ctx.Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.event_type == "OrderPlaced").OrderByDescending(m => m.id).FirstOrDefaultAsync();
            mesaj.Should().NotBeNull("siparis olayi outbox'a YAZILMIS olmali (transaction icinde)");

            // CIFT-ANLAM KIRICI: mesaj gercekten bu siparise ait olmali - herhangi bir satir degil.
            var evt = JsonSerializer.Deserialize<OrderPlacedEvent>(mesaj!.payload);
            evt!.customer_id.Should().Be(musteri.CustomerId);
            evt.order_number.Should().NotBeNullOrWhiteSpace();
        }

        // ── A1(b): KALICI HATA ZAMAN CIZELGESINDE GORUNUR ────────────────────────────────
        [Fact]
        public async Task SMTP_KALICI_PATLARSA_ZAMAN_CIZELGESINE_KRITIK_Notu_Duser()
        {
            if (Skipped()) return;

            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var (urunId, beden) = await UrunHazirlaAsync();
            var adresId = await AdresHazirlaAsync(musteri.Client, musteri.CustomerId);
            var r = await musteri.Client.PostAsJsonAsync("/api/order/place", new
            {
                customer_id = musteri.CustomerId,
                address_id = adresId,
                coupon_code = "",
                use_store_credit = 0,
                payment_method = (byte)1,
                items = new[] { new { product_id = urunId, size = beden, quantity = 1 } }
            });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
            var siparisId = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetInt32();

            // GERCEK ZINCIR KOSULUYOR: gercek publisher + gercek OrderPlacedEmailHandler +
            // HER ZAMAN patlayan mail. Stub bir publisher kullanmak kendi sahtemizi pinlemek olurdu.
            var patlayan = new SahteMail();
            Patlasin = true;
            for (int i = 0; i < 5; i++)
            {
                await using var ctx = NewContext();
                var handler = new OrderPlacedEmailHandler(patlayan, new EfCustomerDal(ctx),
                    new MailLinkBuilder(VitrinYapilandirmasi(), NullLogger<MailLinkBuilder>.Instance),
                    NullLogger<OrderPlacedEmailHandler>.Instance);
                var publisher = new OrderPlacedEventPublisher(new IOrderPlacedEventHandler[] { handler });
                var processor = new OutboxProcessor(
                    new EfOutboxMessageDal(ctx), publisher, patlayan,
                    new OrderStatusHistoryManager(new EfOrderStatusHistoryDal(ctx), new EfOrderDal(ctx)),
                    NullLogger<OutboxProcessor>.Instance, new CagrilmayanScopeFactory());
                await processor.ProcessPendingAsync();
            }

            await using (var ctx = NewContext())
            {
                var mesaj = await ctx.Set<OutboxMessage>().AsNoTracking()
                    .Where(m => m.event_type == "OrderPlaced").OrderByDescending(m => m.id).FirstAsync();
                mesaj.status.Should().Be((byte)OutboxStatusEnum.Failed,
                    "5 denemeden sonra mesaj KALICI HATA olmali");

                var notlar = await ctx.Set<OrderStatusHistory>().AsNoTracking()
                    .Where(h => h.order_id == siparisId).Select(h => h.note).ToListAsync();
                notlar.Should().Contain(n => n != null && n.Contains("KRITIK") && n.Contains("onay e-postası"),
                    "kalici hata SESSIZ kalmamali - operator siparis zaman cizelgesinde GORMELI");
            }
        }

        // ── A1(c): ORIGIN YOKSA GURULTULU, VARSA LINK ────────────────────────────────────
        [Fact]
        public void ORIGIN_YOKSA_LINK_URETILMEZ_VARSA_URETILIR()
        {
            var bos = new MailLinkBuilder(new ConfigurationBuilder().Build(),
                NullLogger<MailLinkBuilder>.Instance);
            bos.VitrinBaglantisi("#/dogrula/abc").Should().BeNull(
                "origin yoksa YARIM bir URL uretilmemeli - cagiran yedek yonergeye duser");
            bos.ApiBaglantisi("/api/x/unsubscribe?token=abc").Should().BeNull();

            // VAKUM KIRICI: origin doluyken GERCEKTEN link uretilmeli. Bu olmadan "her zaman null
            // donen" bozuk bir uygulama da yukaridaki asserti gecerdi.
            var dolu = new MailLinkBuilder(VitrinYapilandirmasi(), NullLogger<MailLinkBuilder>.Instance);
            dolu.VitrinBaglantisi("#/dogrula/abc").Should().Be($"{VitrinTabani}/#/dogrula/abc");
            // CIFT-ANLAM KIRICI: API baglantilari AYRI kaynaktan gelir (Sprint 8 madde 10 kalibi),
            // vitrin origin'i onlari doldurmaz.
            dolu.ApiBaglantisi("/api/x?token=abc").Should().BeNull(
                "vitrin origin'i API origin'inin YERINE GECMEZ - iki ayri ayar");
        }

        // ── SUPHELI (DUZELTILMEDI, KARAR KULLANICININ) ───────────────────────────────────
        //
        // OLCULDU: sifre politikasi UC AYRI YERDE UC AYRI: kayit 8+buyuk+kucuk+rakam
        // (CustomerRegisterRequestValidator), ChangePassword yalniz >= 6 karakter,
        // ResetPassword ise HICBIR KONTROL YAPMIYOR - dto.new_password dogrudan hash'leniyor.
        // A2 bu akisi arayuze BAGLADIGI icin kapi artik her musteriye acik: "sifremi unuttum"
        // ile gelen biri kayitta reddedilecek bir sifre koyabilir.
        // Ev kurali geregi DUZELTILMEDI; bugunku davranis ADIYLA sabitleniyor.
        [Fact]
        public async Task SUPHELI_SifreSifirlamada_SUNUCU_TARAFI_SIFRE_POLITIKASI_YOK_PINLENIR()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();
            var eposta = $"politika-{Guid.NewGuid():N}@example.com";
            (await client.PostAsJsonAsync("/api/auth/register", new
            {
                name = "Politika Musteri",
                email = eposta,
                phone = "5550000000",
                password = "GucluSifre2026",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            })).StatusCode.Should().Be(HttpStatusCode.Created);

            // CIFT-ANLAM KIRICI: ayni zayif sifre KAYITTA reddediliyor - yani kural VAR,
            // yalnizca bu ucta UYGULANMIYOR.
            var kayit = await client.PostAsJsonAsync("/api/auth/register", new
            {
                name = "Zayif",
                email = $"zayif-{Guid.NewGuid():N}@example.com",
                phone = "5550000000",
                password = "abc",
                accepted_terms = true,
                accepted_privacy = true,
                accepted_marketing = false
            });
            kayit.StatusCode.Should().Be(HttpStatusCode.BadRequest, "kayit politikasi 'abc' sifresini REDDEDER");

            await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = eposta });
            string jeton;
            await using (var ctx = NewContext())
                jeton = (await ctx.Set<Customer>().AsNoTracking()
                    .FirstAsync(c => c.email == eposta.ToLowerInvariant())).password_reset_token!;

            var sifirla = await client.PostAsJsonAsync("/api/auth/reset-password",
                new { token = jeton, new_password = "abc" });
            sifirla.StatusCode.Should().Be(HttpStatusCode.OK,
                "SUPHELI: sifre sifirlama ucu kayit politikasini UYGULAMIYOR - 'abc' kabul ediliyor");
        }

        // ── Yardimcilar ─────────────────────────────────────────────────────────────────
        private static IConfiguration VitrinYapilandirmasi() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storefront:BaseUrl"] = VitrinTabani
            }).Build();

        private sealed class CagrilmayanScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
                => throw new NotSupportedException("Mail zinciri pinlerinde odeme dali kullanilmaz.");
        }

        // Kategori GERCEKTEN olusturulur ve description/color_hex doldurulur
        // (CLAUDE.md bolum 5'teki iki tuzak).
        private static async Task<(int UrunId, string Beden)> UrunHazirlaAsync()
        {
            await using var ctx = NewContext();
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            // slug NOT NULL (olculdu: ilk kosumda "Cannot insert the value NULL into column 'slug'").
            var kat = new Category
            {
                name = "Mail Kategori " + damga,
                slug = "mail-kategori-" + damga,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Mail Urun " + damga,
                description = "mail zinciri pini icin urun",
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

            // is_active ZORUNLU: olculdu - eksikse stok sorgusu satiri hic gormuyor ve siparis
            // "Yetersiz stok" ile 400 doner (ilk kosumda birebir yasandi).
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
            var r = await client.PostAsJsonAsync("/api/address/upsert", new
            {
                title = "Ev",
                full_name = "Mail Musteri",
                phone = "5550000000",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "Test Mah. 1",
                zip_code = "34710",
                is_default = true
            });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
            await using var ctx = NewContext();
            return (await ctx.Set<Address>().AsNoTracking()
                .Where(a => a.customer_id == musteriId).OrderByDescending(a => a.id).FirstAsync()).id;
        }
    }
}
