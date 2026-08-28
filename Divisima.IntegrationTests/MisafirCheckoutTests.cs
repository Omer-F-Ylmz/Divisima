using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Divisima.Bussiness.Concrete;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Orders;
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
        // GUVENLIK-FIX-4: AYIRT EDICI esik - `GuestCheckoutManager.VarsayilanEsik` (3) DEGIL.
        // Ayar okunmasaydi ucuncu istek 201 gecerdi; 429 almasi ayarin OKUNDUGUNUN kanitidir.
        private const int TestEsigi = 2;

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
                // GUVENLIK-FIX-4: esik AYIRT EDICI bir degere cekilir (2), varsayilan 3 DEGIL.
                // Boylece "esik yapilandirmadan okunuyor" iddiasi DAVRANISLA kanitlanir:
                // ayar okunmasaydi ucuncu istek 201 gecerdi. (RateLimitTekKaynakTests kalibi.)
                builder.UseSetting(GuestCheckoutManager.EsikAnahtari, TestEsigi.ToString());
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
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
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
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
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

        // ── P21) MANTIK-FIX-1 / K3 - MISAFIR KUPONU SUNUCUYA TASINIR ───────────────────
        // DAVRANIS pini (durust etiket): gercek HTTP ucu, gercek DB dogrulamasi.
        //
        // OLCULEN ONCE-DURUM (R-M3): api-bridge.js misafir govdesinde `coupon_code: ""`
        // SABITTI; uye govdesi kuponu GONDERIYORDU. Musteri cekmecede indirimi GORUYOR
        // (kupon kutusu misafire ACIK - index.html:2610 kosulsuz) ve TAM FIYAT oduyordu.
        // Sunucu tarafi ZATEN calisiyordu: GuestCheckoutDto.coupon_code VAR ve
        // GuestCheckoutManager.cs:220 onu PlaceOrder'a TASIYOR - yani K3 SAF ISTEMCI duzeltmesi.
        [Fact]
        public async Task MISAFIR_KUPONU_SUNUCUYA_TASINIR_GECERSIZ_KUPON_400_DONER()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();   // 499,90 TL

            // (1) ASIL IDDIA: gecerli kupon UYGULANIR ve DB'ye YAZILIR.
            var kod = await KuponHazirlaAsync(deger: 10m);
            var eposta = $"misafir-kupon-{Guid.NewGuid():N}@example.com";
            var r = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme, kod));
            r.StatusCode.Should().Be(HttpStatusCode.Created,
                "gecerli kuponlu misafir siparisi olusmali. Govde: " +
                Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await r.Content.ReadAsStringAsync()));

            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == eposta);
                var o = await ctx.Set<Order>().AsNoTracking()
                    .Where(x => x.customer_id == m.id).OrderByDescending(x => x.id).FirstAsync();

                // VAKUM KIRICI: indirim GERCEKTEN uygulanmis olmali - alan tasinip yok
                // sayilsaydi siparis yine 201 doner ama discount_amount 0 kalirdi.
                o.discount_amount.Should().BeGreaterThan(0m,
                    "kupon SUNUCUYA TASINMIS ve UYGULANMIS olmali - eski halde coupon_code sabit "
                    + "bos dizgeydi ve musteri cekmecede indirimi gorup TAM FIYAT oduyordu");
                o.coupon_code.Should().Be(kod, "kullanilan kupon kodu siparise YAZILMALI");
                o.discount_amount.Should().Be(49.99m, "yuzde 10 indirim 499,90 uzerinden hesaplanmali");
            }

            // (2) ZORUNLU BACAK / CIFT-ANLAM KIRICI: GECERSIZ kupon 400 + KENDI MESAJI.
            // MFIX-B/K2 sunucu tarafinda "gecersiz kupon SESSIZCE yok sayilmaz" sozlesmesini
            // kurmustu; K3 kuponu misafir yolunda tasimaya baslayinca o sozlesmenin misafirde
            // de GECERLI oldugu KANITLANMALI. Bu assert olmadan "misafirde kuponu yine sessizce
            // yut" diyen bir uygulama (1)'i gecerdi.
            var eposta2 = $"misafir-kupon-{Guid.NewGuid():N}@example.com";
            var r2 = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta2, urunId, beden, KapidaOdeme, "MFXYOKBOYLEKOD"));
            r2.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "gecersiz kupon SESSIZCE yok sayilmaz - MFIX-B/K2 sozlesmesi misafir yolunda da gecerli");
            (await r2.Content.ReadAsStringAsync()).Should().Contain("upon",
                "yanit sebebi ADIYLA soylemeli - musteri neden reddedildigini bilmeli");

            // (3) SIPARIS OLUSMAZ - MFIX-B/K2'nin uye yolundaki ikizi.
            (await SiparisSayisiAsync(eposta2)).Should().Be(0,
                "gecersiz kupon reddedilen istekte SIPARIS olusmamali");

            // (4) SUPHELI DAVRANIS - BUGUNKU HALI PINLENIR, DEGISTIRILMEZ (ev kurali).
            // OLCULDU: reddedilen istek MUSTERI SATIRI BIRAKIYOR. Kok sebep
            // GuestCheckoutManager.cs:173 - musteri (ve :190 adres) PlaceOrder'a DEVRETMEDEN
            // ONCE yaziliyor, kupon dogrulamasi ise PlaceOrder'in ICINDE.
            // CANLI ZARAR ZINCIRI (uctan uca olculdu):
            //   1) misafir gecersiz kupon girer      -> 400 "Gecersiz kupon kodu."
            //   2) DB'de musteri satiri OLUSMUS olur (email_verified=0, siparis YOK)
            //   3) ayni misafir KUPONSUZ tekrar dener -> 409 "Bu e-posta kayitli. Lutfen giris yapin."
            //   -> TEK BIR YANLIS KUPON KODU, o e-postayi misafir checkout'a KALICI KAPATIYOR
            //      (ustelik musteri giris de yapamaz: parola rastgele uretildi, kendisi bilmiyor).
            // K3 BU TUZAGI YARATMADI ama ULASILABILIR KILDI: K3 oncesi misafir kuponu
            // sunucuya HIC gitmiyordu, dolayisiyla bu 400 dali misafirde HIC ATESLEYEMEZDI.
            // NEDEN BU DALGADA DUZELTILMEDI: cozum ya GuestCheckoutManager'da ikinci bir kupon
            // dogrulama noktasi acar (bu depoda YEDI kez bedeli odenen "ayni kuralin ikinci
            // kopyasi" sinifi), ya da 409 semantigine dokunur - o ise GUVENLIK DALGASI 2 / #1'de
            // MERKEZIN KABUL ETTIGI bir risk kararidir. Karar MERKEZIN.
            // HAFIFLETICI (olculdu): istemci kuponu cekmecede /api/coupon/validate ile ONCEDEN
            // dogruluyor, dolayisiyla DUZ YAZIM HATASI bu dala normalde ULASMAZ; dal ancak kupon
            // dogrulama ile siparis arasinda GECERSIZLESIRSE (limit/sure) atesler.
            (await MusteriSayisiAsync(eposta2)).Should().Be(1,
                "SUPHELI (MANTIK-FIX-1'de olculdu, DUZELTILMEDI): reddedilen misafir istegi " +
                "MUSTERI SATIRI BIRAKIYOR ve ayni e-posta ikinci denemede 409 aliyor. Bu assert " +
                "bugunku davranisi PINLER; duzeltildigi gun KIRILIR ve o zaman 0'a cevrilir.");
        }

        // ── Yardimcilar ─────────────────────────────────────────────────────────────────
        private static MailMessageDto? MailBul(string konuParcasi, string alici)
        {
            lock (Yakalanan)
                return Yakalanan.LastOrDefault(m =>
                    (m.Subject ?? "").Contains(konuParcasi) &&
                    string.Equals(m.To, alici, StringComparison.OrdinalIgnoreCase));
        }

        // ══ GUVENLIK-FIX-4 / SUPHELI #22 - IDEMPOTENCY GOVDE BAGI ve BAYT-BIREBIR REPLAY ══
        //
        // OLCULEN ONCE-DURUM (canli, /api/guest-checkout/place):
        //   anahtar K + govde(E2) -> 201 siparis 179
        //   anahtar K + govde(E3) -> 201 "Idempotency-Replayed: true", GOVDEDE 179
        //   E3 icin musteri 0, siparis 0        (istek SESSIZCE dustu)
        //   ve replay govdesi {"Data":179,...} iken orijinal {"data":179,...} idi.
        [Fact]
        public async Task IDEMPOTENCY_AYNI_ANAHTAR_FARKLI_GOVDE_422_Doner_ve_IKINCI_SIPARIS_OLUSMAZ()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var damga = Guid.NewGuid().ToString("N").Substring(0, 10);
            var ilkEposta = $"gf4a-{damga}@example.com";
            var farkliEposta = $"gf4b-{damga}@example.com";
            var anahtar = "gf4-" + Guid.NewGuid().ToString("N");

            var ilk = await MisafirIstekAsync(anahtar, MisafirGovdesi(ilkEposta, urunId, beden, KapidaOdeme));
            ilk.StatusCode.Should().Be(HttpStatusCode.Created,
                $"on kosul: ilk istek islenmeli. Govde: {await ilk.Content.ReadAsStringAsync()}");

            var ikinci = await MisafirIstekAsync(anahtar, MisafirGovdesi(farkliEposta, urunId, beden, KapidaOdeme));

            ikinci.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                "ayni anahtar FARKLI govdeyle kullanildiginda istek ISLENMEZ ve BASKASININ yaniti REPLAY EDILMEZ");
            ikinci.Headers.Contains("Idempotency-Replayed").Should().BeFalse(
                "422 bir replay DEGILDIR - istemci baskasinin sonucunu almamali");

            // CIFT-ANLAM KIRICI: 422 KOZMETIK DEGIL - istek SESSIZCE de DUSMEMELI,
            // yani ikinci e-posta icin hicbir sey olusmamali AMA istemci bunu OGRENMELI.
            (await MusteriSayisiAsync(farkliEposta)).Should().Be(0,
                "farkli govdeli istek ISLENMEMELI - ikinci musteri olusmamali");
            (await MusteriSayisiAsync(ilkEposta)).Should().Be(1,
                "ilk istek etkilenmemeli");
        }

        [Fact]
        public async Task IDEMPOTENCY_AYNI_GOVDE_REPLAY_GOVDESI_ORIJINALLE_BAYT_BIREBIR()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"gf4c-{Guid.NewGuid():N}@example.com";
            var anahtar = "gf4-" + Guid.NewGuid().ToString("N");
            var govde = MisafirGovdesi(eposta, urunId, beden, KapidaOdeme);

            var ilk = await MisafirIstekAsync(anahtar, govde);
            ilk.StatusCode.Should().Be(HttpStatusCode.Created);
            var ilkGovde = await ilk.Content.ReadAsStringAsync();

            var ikinci = await MisafirIstekAsync(anahtar, govde);
            var ikinciGovde = await ikinci.Content.ReadAsStringAsync();

            ikinci.Headers.Contains("Idempotency-Replayed").Should().BeTrue("ayni govde REPLAY almali");
            ikinci.StatusCode.Should().Be(ilk.StatusCode, "replay ILK yanitin durum kodunu tasimali");

            // ASIL OLCUM: bicim hakkinda hicbir varsayim YOK - baytlar BIREBIR ayni olmali.
            // (Once: orijinal camelCase "data", replay PascalCase "Data".)
            string.Equals(ikinciGovde, ilkGovde, StringComparison.Ordinal).Should().BeTrue(
                "ORDINAL karsilastirma: buyuk/kucuk harf farki bile KABUL EDILEMEZ");

            // CIFT-ANLAM KIRICI: replay KOZMETIK DEGIL - ikinci siparis OLUSMAMIS olmali.
            (await SiparisSayisiAsync(eposta)).Should().Be(1, "cift siparis ENGELLENMIS olmali");
        }

        // ══ GUVENLIK-FIX-4 / DALGA-2 #2 - COP MISAFIR SIPARISI GUARD'I ════════════════════
        //
        // SPEC OLCUMLE DUZELTILDI: "Pending + SAKLANAN e-posta basina" yuklemi HIC
        // ATESLEMEZDI (misafir COD siparisi Confirmed dogar; ayni saklanan e-postaya ikinci
        // siparis zaten 409 alir -> n<=1). GERCEK VEKTOR: `+etiket` varyanti 409'u asip AYNI
        // fiziksel kutuya yigiliyor. Sayac ekseni KANONIK POSTA KUTUSU.
        [Fact]
        public async Task MISAFIR_GUARD_ESIK_ALTI_201_ESIKTE_429_KANONIK_KUTU_EKSENINDE()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var yerel = "gf4guard-" + Guid.NewGuid().ToString("N").Substring(0, 10);

            // Esik altindaki her istek ISLENIR (vakum kirici: guard "her seyi reddet" DEGIL).
            for (var i = 0; i < TestEsigi; i++)
            {
                var varyant = i == 0 ? $"{yerel}@example.com" : $"{yerel}+{i}@example.com";
                var yanit = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                    MisafirGovdesi(varyant, urunId, beden, KapidaOdeme));
                yanit.StatusCode.Should().Be(HttpStatusCode.Created,
                    $"esik altindaki {i + 1}. istek islenmeli. Govde: {await yanit.Content.ReadAsStringAsync()}");
            }

            // ESIKTE: AYNI kanonik kutuya bir varyant daha -> 429.
            // ESIK YAPILANDIRMADAN OKUNUYOR: test host'u AYIRT EDICI bir deger veriyor
            // (varsayilan 3 DEGIL); ayar okunmasaydi bu istek 201 gecerdi.
            var esikte = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi($"{yerel}+{TestEsigi}@example.com", urunId, beden, KapidaOdeme));

            esikte.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
                $"kanonik kutuda {TestEsigi} acik siparis varken yenisi REDDEDILMELI");
            TestEsigi.Should().NotBe(GuestCheckoutManager.VarsayilanEsik,
                "test esigi varsayilandan FARKLI olmali - yoksa 'yapilandirmadan okunuyor' iddiasi kanitlanmaz");

            var govde = await esikte.Content.ReadAsStringAsync();
            govde.Should().NotContain("kayıtlı",
                "429 govdesi NOTR olmali - adresin kayit durumunu IMA ETMEMELI (409 ile karismamali)");
        }

        [Fact]
        public async Task MISAFIR_GUARD_REDDINDE_HICBIR_YAN_ETKI_OLUSMAZ()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var yerel = "gf4yan-" + Guid.NewGuid().ToString("N").Substring(0, 10);

            for (var i = 0; i < TestEsigi; i++)
            {
                var varyant = i == 0 ? $"{yerel}@example.com" : $"{yerel}+{i}@example.com";
                (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                    MisafirGovdesi(varyant, urunId, beden, KapidaOdeme))).StatusCode
                    .Should().Be(HttpStatusCode.Created);
            }

            var oncesi = await KutuSayaclariAsync(yerel);
            oncesi.Musteri.Should().Be(TestEsigi, "on kosul: esik GERCEKTEN dolmus olmali");

            var reddedilen = $"{yerel}+{TestEsigi}@example.com";
            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(reddedilen, urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.TooManyRequests);

            var sonrasi = await KutuSayaclariAsync(yerel);
            sonrasi.Should().BeEquivalentTo(oncesi,
                "guard TUM yan etkilerden ONCE calisir - reddedilen istek musteri/adres/siparis/"
                + "rezervasyon/outbox satiri BIRAKMAMALI");
            (await MusteriSayisiAsync(reddedilen)).Should().Be(0,
                "reddedilen adres icin musteri satiri HIC olusmamali");
        }

        // CIFT-ANLAM KIRICI: guard 409'u EZMEDI. "Her seyi 429'a cevir" YANLIS duzeltmedir -
        // hesap ele gecirme korumasi (kabul edilen risk, GUVENLIK DALGASI 2 / #1) DURUYOR.
        [Fact]
        public async Task MISAFIR_GUARD_409_SEMANTIGINI_DEGISTIRMEZ()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var eposta = $"gf4dokuz-{Guid.NewGuid():N}@example.com";

            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.Created);

            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.Conflict, "AYNI SAKLANAN adres HALA 409 almali");

            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta.ToUpperInvariant(), urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.Conflict,
                    "BUYUK HARF varyanti HALA 409 almali - Dalga 1 kanoniklestirmesi korunuyor");
        }

        // ══ GUVENLIK DALGASI 2 / #1 - KABUL EDILEN RISK: 409 YOLU HICBIR SATIR YAZMAZ ═════
        //
        // Kod DEGISMEDI (409 + "giris yapin" kalir). Bu pin, kabul edilen riskin SINIRINI
        // sabitler: enumeration kanali aciktir AMA o yanit hicbir yan etki URETMEZ - yani
        // kanal bir kaynak tuketimi/taciz vektorune DONUSEMEZ.
        [Fact]
        public async Task KAYITLI_EPOSTAYA_MISAFIR_SIPARISI_409_ve_HICBIR_SATIR_YAZMAZ()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();
            var yerel = "gf4kabul-" + Guid.NewGuid().ToString("N").Substring(0, 10);
            var eposta = $"{yerel}@example.com";

            (await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme))).StatusCode
                .Should().Be(HttpStatusCode.Created, "on kosul: hesap GERCEKTEN olusmali");

            var oncesi = await KutuSayaclariAsync(yerel);

            var ikinci = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme));
            ikinci.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var sonrasi = await KutuSayaclariAsync(yerel);
            sonrasi.Should().BeEquivalentTo(oncesi,
                "409 yolu musteri/adres/siparis/rezervasyon/outbox satiri YAZMAMALI");
        }

        // "ACIK" kume ELLE YAZILMAZ - durum makinesinden turetilir. Makine degisirse bu pin
        // de kendiliginde degisir; sabit bir liste yazsaydik makine ile SESSIZCE ayrisirdi.
        // VERITABANI ACMAZ.
        [Fact]
        public void ACIK_DURUM_KUMESI_DURUM_MAKINESINDEN_TURETILIR_ELLE_YAZILMAZ()
        {
            var beklenen = Enum.GetValues(typeof(OrderStatusEnum))
                .Cast<OrderStatusEnum>()
                .Where(d => d != OrderStatusEnum.Cancelled
                            && OrderStatusMachine.IsValidTransition(d, OrderStatusEnum.Cancelled))
                .Select(d => (byte)d)
                .ToArray();

            GuestCheckoutManager.AcikDurumlar.Should().BeEquivalentTo(beklenen);

            // CIFT-ANLAM KIRICI: kume ne BOS ne de HER SEY. Kapanmis durumlar DISARIDA olmali -
            // Shipped yalniz Delivered'a gider (iptal edilemez), Delivered/Cancelled terminal.
            GuestCheckoutManager.AcikDurumlar.Should().Contain((byte)OrderStatusEnum.Pending);
            GuestCheckoutManager.AcikDurumlar.Should().Contain((byte)OrderStatusEnum.Confirmed);
            GuestCheckoutManager.AcikDurumlar.Should().Contain((byte)OrderStatusEnum.Preparing);
            GuestCheckoutManager.AcikDurumlar.Should().NotContain((byte)OrderStatusEnum.Shipped);
            GuestCheckoutManager.AcikDurumlar.Should().NotContain((byte)OrderStatusEnum.Delivered);
            GuestCheckoutManager.AcikDurumlar.Should().NotContain((byte)OrderStatusEnum.Cancelled);
        }

        private async Task<HttpResponseMessage> MisafirIstekAsync(string anahtar, object govde)
        {
            var istek = new HttpRequestMessage(HttpMethod.Post, "/api/guest-checkout/place")
            {
                Content = JsonContent.Create(govde)
            };
            istek.Headers.Add("Idempotency-Key", anahtar);
            return await _factory!.CreateClient().SendAsync(istek);
        }

        private static async Task<int> MusteriSayisiAsync(string eposta)
        {
            await using var ctx = NewContext();
            var kanonik = eposta.Trim().ToLowerInvariant();
            return await ctx.Set<Customer>().AsNoTracking().CountAsync(c => c.email == kanonik);
        }

        private static async Task<int> SiparisSayisiAsync(string eposta)
        {
            await using var ctx = NewContext();
            var kanonik = eposta.Trim().ToLowerInvariant();
            var musteri = await ctx.Set<Customer>().AsNoTracking().FirstAsync(c => c.email == kanonik);
            return await ctx.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == musteri.id);
        }

        // Bir KANONIK KUTUYA ait tum yan etki sayaclari. `yerel` benzersiz bir onek oldugu
        // icin baska testlerin verisiyle karismaz.
        private static async Task<KutuSayaclari> KutuSayaclariAsync(string yerel)
        {
            await using var ctx = NewContext();
            var kimlikler = await ctx.Set<Customer>().AsNoTracking()
                .Where(c => c.email.StartsWith(yerel)).Select(c => c.id).ToListAsync();
            var siparisler = await ctx.Set<Order>().AsNoTracking()
                .Where(o => kimlikler.Contains(o.customer_id)).Select(o => o.id).ToListAsync();

            return new KutuSayaclari
            {
                Musteri = kimlikler.Count,
                Adres = await ctx.Set<Address>().AsNoTracking().CountAsync(a => kimlikler.Contains(a.customer_id)),
                Siparis = siparisler.Count,
                Rezervasyon = await ctx.Set<StockReservation>().AsNoTracking()
                    .CountAsync(r => siparisler.Contains(r.order_id)),
                Outbox = await ctx.Set<OutboxMessage>().AsNoTracking()
                    .CountAsync(m => m.payload.Contains(yerel))
            };
        }

        private sealed class KutuSayaclari
        {
            public int Musteri { get; set; }
            public int Adres { get; set; }
            public int Siparis { get; set; }
            public int Rezervasyon { get; set; }
            public int Outbox { get; set; }
        }

        // ── P13 (MFIX-B / K3) ────────────────────────────────────────────────────────────
        // OLCULEN ONCE-DURUM (canli, ESKI ikili): POST /api/order/place -> {"data":224,...}
        // yani `data` CIPLAK INT idi. Istemci musteriye gosterecek GERCEK siparis numarasini
        // elde edemiyordu: uye yolunda order_number icin IKINCI bir /api/order/get cagrisi
        // yapiliyor, MISAFIR yolunda ise o uc Customer'a kilitli (anonim 401) oldugu icin
        // numara HIC alinamiyor ve ekranda veritabani kimligi "Referans: 224" gosteriliyordu.
        [Fact]
        public async Task Place_Yaniti_Id_ve_OrderNumber_Tasir()
        {
            if (Skipped()) return;
            var (urunId, beden) = await UrunHazirlaAsync();

            // ── MISAFIR YOLU (K3'un asil kazanani: order_number BASKA yerden alinamaz) ──
            var eposta = $"p13-{Guid.NewGuid():N}@example.com";
            var mr = await _factory!.CreateClient().PostAsJsonAsync("/api/guest-checkout/place",
                MisafirGovdesi(eposta, urunId, beden, KapidaOdeme));
            var mGovde = await mr.Content.ReadAsStringAsync();
            mr.StatusCode.Should().Be(HttpStatusCode.Created, $"misafir siparisi olusmali. Govde: {mGovde}");

            var mData = (await mr.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("data");
            var mId = mData.GetProperty("id").GetInt32();
            var mNo = mData.GetProperty("order_number").GetString();

            await using (var ctx = NewContext())
            {
                var s = await ctx.Set<Order>().AsNoTracking().FirstAsync(o => o.id == mId);
                mNo.Should().Be(s.order_number, "yanittaki numara VERITABANINDAKI numara ile BIREBIR olmali");
                // CIFT-ANLAM KIRICI: numara, kimligin metne cevrilmis hali DEGIL.
                mNo.Should().NotBe(mId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "order_number bir SIPARIS NUMARASIDIR, veritabani kimligi degil");
                mNo.Should().StartWith("DVS", "uretilen numara bicimi korunmali");
            }

            // ── UYE YOLU: ayni sozlesme (istemci artik ikinci bir orders.get cagirmiyor) ──
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var adresId = await AdresHazirlaAsync(musteri.Client, musteri.CustomerId);
            var ur = await musteri.Client.PostAsJsonAsync("/api/order/place", new
            {
                customer_id = musteri.CustomerId,
                address_id = adresId,
                coupon_code = "",
                use_store_credit = 0m,
                payment_method = KapidaOdeme,
                items = new[] { new { product_id = urunId, size = beden, quantity = 1 } }
            });
            var uGovde = await ur.Content.ReadAsStringAsync();
            ur.StatusCode.Should().Be(HttpStatusCode.Created, $"uye siparisi olusmali. Govde: {uGovde}");

            var uData = (await ur.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("data");
            uData.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object,
                "data artik CIPLAK INT degil, { id, order_number } nesnesi");
            var uId = uData.GetProperty("id").GetInt32();
            var uNo = uData.GetProperty("order_number").GetString();
            uId.Should().BeGreaterThan(0, "sayisal kimlik KALDI - payment/initialize ve order/get onu kullanir");

            await using (var ctx = NewContext())
            {
                var s = await ctx.Set<Order>().AsNoTracking().FirstAsync(o => o.id == uId);
                uNo.Should().Be(s.order_number);
            }
        }

        // MANTIK-FIX-1 / K3: `kuponKodu` parametresi EKLENDI (varsayilan "" - mevcut
        // cagiranlarin HICBIRI etkilenmez). Depoda misafir + kupon tasiyan tek bir fikstur
        // YOKTU; K3'un davranisi onsuz olculemezdi.
        private static object MisafirGovdesi(string eposta, int urunId, string beden, byte yontem,
            string kuponKodu = "") => new
            {
                guest_name = "Misafir Musteri",
                guest_email = eposta,
                guest_phone = "5550000000",
                city = "Istanbul",
                district = "Kadikoy",
                full_address = "Misafir Mah. 1",
                zip_code = "34710",
                coupon_code = kuponKodu,
                payment_method = yontem,
                items = new[] { new { product_id = urunId, size = beden, quantity = 1 } }
            };

        // MANTIK-FIX-1 / K3: kupon fiksturu (uretimdeki alan adlariyla; kaynaktan okundu).
        private static async Task<string> KuponHazirlaAsync(decimal deger, decimal minTutar = 0m)
        {
            await using var ctx = NewContext();
            var kod = "MFXK3" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            ctx.Set<Coupon>().Add(new Coupon
            {
                code = kod,
                discount_type = 0,          // Yuzde
                value = deger,
                min_amount = minTutar,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return kod;
        }

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
