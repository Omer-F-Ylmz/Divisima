using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Dtos.Payment;
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
    // ══ SPRINT 8 MADDE 9 - WEBHOOK SOZLESMESI ═══════════════════════════════════════════
    //
    // Bu sinif, GERCEK bir Iyzico bildirimi public tunel uzerinden olculduktan SONRA yazildi.
    // Olculen bildirim (22 Agustos 2026, User-Agent Apache-HttpClient/5.2.3):
    //   govde  : {"paymentConversationId":...,"status":"SUCCESS","token":"76ee5138-...",
    //             "iyziEventType":"CHECKOUT_FORM_AUTH","iyziPaymentId":37415135}  -> "signature" alani YOK
    //   baslik : X-Api-Version: V1        (bizim surum bicimimize ayristirilamiyor)
    //            X-Iyz-Signature:         (VAR ama DEGERI BOS)
    //   yanitimiz: 400
    // Canli zarar: siparis #33 - para Iyzico'da SUCCESS, bizde Pending. "Callback kayboldu"
    // senaryosunda TEK kurtarma yolu calismiyordu.
    //
    // BILINCLI KIRILAN PIN: Webhook_ImzaSIZ_REDDEDILIR_CF_Gevsemesi_SIZMAZ
    // (PaymentCallbackRedirectTests). O pin E2b'de DOGRU bir seyi sabitliyordu - "tarayici
    // yolundaki gevseme sunucu-sunucu yola SIZMAZ". Ama dayandigi VARSAYIM (webhook'ta imza
    // GELIR) gercek bildirimle CURUTULDU: saglayici bu yolda da imza gondermiyor. Pin dogru
    // olmayan bir sozlesmeyi savunur hale geldigi icin KALDIRILDI; yerine bu sinif geldi.
    // Gevsemenin SINIRI burada pinleniyor: imza GELIRSE hala dogrulanir ve tutmazsa reddedilir.
    //
    // MOCK MODU: Iyzico:UseRealSdk=false. Imza dogrulamasi GERCEK algoritma ile calisir
    // (HMAC-SHA256(secretKey, token)) - mock'la olcmek bu yuzden anlamli.
    [Trait("Category", "Sql")]
    public class WebhookContractTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaWebhookContractTest";
        private const string TestSecretKey = "divisima-test-iyzico-secret-key";
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

        // Sunucu-sunucu sorguyu SAYAN istemci. Imza + init GERCEK istemciye devredilir -
        // dogrulama zinciri bozulmasin diye. Sayac, "amplifikasyon" iddiasinin OLCUMU:
        // bizim olmayan bir token Iyzico'ya HIC cikmamali.
        private sealed class SayanIyzicoClient : IIyzicoClient
        {
            private readonly IyzicoClient _real;
            public SayanIyzicoClient(IConfiguration config)
                => _real = new IyzicoClient(config, NullLogger<IyzicoClient>.Instance);

            public static int RetrieveCallCount;
            public static Func<string, IyzicoPaymentResult>? RetrieveOverride { get; set; }

            public static void Sifirla() { RetrieveCallCount = 0; RetrieveOverride = null; }

            public Task<IyzicoCheckoutInitResult> InitializeCheckoutFormAsync(IyzicoCheckoutInitRequest request)
                => _real.InitializeCheckoutFormAsync(request);

            public bool VerifyCallbackSignature(string token, string signature)
                => _real.VerifyCallbackSignature(token, signature);

            public async Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string token)
            {
                System.Threading.Interlocked.Increment(ref RetrieveCallCount);
                return RetrieveOverride != null ? RetrieveOverride(token) : await _real.RetrievePaymentResultAsync(token);
            }

            public Task<IyzicoRefundResult> RefundAsync(string paymentTransactionId, decimal amount)
                => _real.RefundAsync(paymentTransactionId, amount);
        }

        private sealed class WebhookFactory : WebApplicationFactory<Program>
        {
            private readonly bool _limitiYukselt;
            public WebhookFactory(bool limitiYukselt) => _limitiYukselt = limitiYukselt;

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Iyzico:SecretKey", TestSecretKey);
                builder.UseSetting("Iyzico:UseRealSdk", "false");
                // Storefront ADRESI ACIKCA BOSALTILIYOR - OLCUME DAYALI.
                // appsettings.Development.json'da "Storefront:BaseUrl": "http://localhost:5173"
                // TANIMLI; bosaltilmazsa Callback 302 doner ve HATA SEBEBI GOVDEDE GORUNMEZ
                // (olculdu: yonlendirmeyi izleyen istemci test sunucusunda /index.html arayip
                // BOS GOVDELI 404 aliyordu - teshis edilmesi zor bir yanlis kirmizi).
                // Bu sinif YANIT BICIMINI degil KARAR SEBEBINI olcuyor; JSON dali gerekli.
                // Yonlendirme bicimi zaten PaymentCallbackRedirectTests'te pinli.
                builder.UseSetting("Storefront:BaseUrl", "");
                // SOZLESME pinleri limite TAKILMAMALI: bu sinifta on'dan fazla webhook cagrisi var
                // ve test sunucusunda RemoteIpAddress null oldugu icin hepsi AYNI kovaya duser.
                // Limitin KENDISI ayri bir host'ta, URETIM VARSAYILANIYLA pinleniyor (test 8).
                if (_limitiYukselt) builder.UseSetting("RateLimit:PaymentPermitLimit", "1000");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    // IIyzicoClient Program.cs'te ServiceCollection'a kayitli; buradaki kayit SONRA
                    // geldigi icin kazanir (AutofacBusinessModule bu servisi kaydetmiyor).
                    services.AddScoped<IIyzicoClient>(sp =>
                        new SayanIyzicoClient(sp.GetRequiredService<IConfiguration>()));
                });
            }
        }

        private WebhookFactory? _factory;        // sozlesme pinleri (limit yuksek)
        private WebhookFactory? _limitFactory;   // yalniz rate limit pini (uretim varsayilani)
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            // STATIK BAYRAK HIJYENI (CLAUDE.md bolum 5): sinif disindan sizan bir override ya da
            // sayac sonraki testleri SESSIZCE bozar. Her testin basinda ayrica sifirlaniyor.
            SayanIyzicoClient.Sifirla();
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new WebhookFactory(limitiYukselt: true);
                _ = _factory.Services;
                _limitFactory = new WebhookFactory(limitiYukselt: false);
                _ = _limitFactory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak webhook sozlesme testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            SayanIyzicoClient.Sifirla();
            if (_factory != null) await _factory.DisposeAsync();
            if (_limitFactory != null) await _limitFactory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private static string Sign(string token)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestSecretKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        private async Task<T> WithScopeAsync<T>(WebApplicationFactory<Program> f, Func<IServiceProvider, Task<T>> fn)
        {
            using var scope = f.Services.CreateScope();
            return await fn(scope.ServiceProvider);
        }

        // Gercek akis: musteri + urun + stok -> siparis -> odeme baslat -> token.
        private async Task<(int orderId, string token)> NewPendingPaymentAsync(WebApplicationFactory<Program> f)
        {
            int customerId, productId;
            await using (var ctx = NewContext())
            {
                var c = new Customer
                {
                    name = "Webhook Testi",
                    email = $"wh-{Guid.NewGuid():N}@divisima.test",
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
                    name = "WH Kategori",
                    slug = $"wh-{Guid.NewGuid():N}",
                    vat_rate = 0.10m,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "WH Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 300m,
                    description = "webhook testi",
                    color_hex = "#123456",
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
                    stock_quantity = 10,
                    reserved_quantity = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
                customerId = c.id; productId = p.id;
            }

            // GF-6 / K2: `address_id` ARTIK ZORUNLU.
            var adresId = await TestAdresHelper.AdresOlusturAsync(ConnStr, customerId);

            var place = await WithScopeAsync(f, sp => sp.GetRequiredService<IOrderService>().PlaceOrder(
                new OrderCreateRequestDto
                {
                    customer_id = customerId,
                    address_id = adresId,
                    coupon_code = "",
                    use_store_credit = 0m,
                    payment_method = 0,
                    items = new() { new OrderItemRequestDto { product_id = productId, size = "M", quantity = 1 } }
                }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            int orderId;
            await using (var ctx = NewContext())
                orderId = (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.customer_id == customerId)).id;

            var init = await WithScopeAsync(f, sp => sp.GetRequiredService<IPaymentService>()
                .Initialize(new PaymentInitRequestDto { order_id = orderId }, customerId));
            init.Item2.Success.Should().BeTrue($"odeme baslatilmali: {init.Item2.Message}");

            await using (var ctx = NewContext())
            {
                var pay = await ctx.Set<Payment>().AsNoTracking().SingleAsync(p => p.order_id == orderId);
                return (orderId, pay.token!);
            }
        }

        // Iyzico'nun GERCEKTE gonderdigi bicim: JSON govde, imza alani YOK.
        private static HttpRequestMessage WebhookIstegi(string token, string? govdeImzasi = null,
            string? baslikImzasi = null, string? surumBasligi = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/payment/webhook")
            {
                Content = govdeImzasi == null
                    ? JsonContent.Create(new { token })
                    : JsonContent.Create(new { token, signature = govdeImzasi })
            };
            if (baslikImzasi != null) req.Headers.TryAddWithoutValidation("X-Iyz-Signature", baslikImzasi);
            if (surumBasligi != null) req.Headers.TryAddWithoutValidation("X-Api-Version", surumBasligi);
            return req;
        }

        private static async Task<(byte payment, byte order)> DurumAsync(int orderId)
        {
            await using var ctx = NewContext();
            var p = await ctx.Set<Payment>().AsNoTracking().SingleAsync(x => x.order_id == orderId);
            var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.id == orderId);
            return (p.payment_status, o.status);
        }

        // Odemeyi YASLANDIRIR - 30 dk'lik token yasi guard'ini surmek icin.
        // Beklemek yerine created_at geri cekiliyor: testin kendisi 30 dk suremez.
        private static async Task GeriTarihliYapAsync(int orderId, TimeSpan yas)
        {
            await using var ctx = NewContext();
            var p = await ctx.Set<Payment>().SingleAsync(x => x.order_id == orderId);
            p.created_at = DateTime.Now - yas;
            await ctx.SaveChangesAsync();
        }

        // ── 1) ENGEL 1: "X-Api-Version: V1" ARTIK WEBHOOK'U DUSURMEZ ────────────────────────
        //
        // Iyzico bu basligi HER bildirimde yolluyor ve degeri bizim bicimimize ayristirilamiyor.
        // Eskiden istek CONTROLLER'A ULASMADAN bos govdeli 400 yiyordu. [ApiVersionNeutral]
        // sonrasi ulasmali VE islenmelidir - 200 tek basina yetmez, is gercekten yapilmali.
        [Fact]
        public async Task WebhookV1SurumBasligiyla_VERSIYONLAMAYA_TAKILMAZ_ve_ISLENIR()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            var resp = await _factory!.CreateClient()
                .SendAsync(WebhookIstegi(token, surumBasligi: "V1"));

            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                "V1 surum basligi tasiyan GERCEK bildirim artik controller'a ulasmali");
            (await resp.Content.ReadAsStringAsync()).Should().NotBeEmpty(
                "versiyonlama reddi BOS govdeli 400 uretiyordu - govdenin dolu olmasi cevabin UYGULAMADAN geldigini gosterir");

            var (odeme, siparis) = await DurumAsync(orderId);
            odeme.Should().Be((byte)PaymentStatusEnum.Success, "200 kozmetik degil - odeme gercekten islenmeli");
            siparis.Should().Be((byte)OrderStatusEnum.Confirmed);
        }

        // ── 2) KAPSAM DAR KALDI: AYNI BASLIK BASKA UCTA HALA 400 ────────────────────────────
        //
        // [ApiVersionNeutral] YALNIZ webhook action'ina konuldu. Bu pin olmadan "surum okuyucusunu
        // tumden sokmus olabilir miyiz" sorusu acik kalirdi. CIFT-ANLAM KIRICI: ayni uc BASLIKSIZ
        // 200 doner - yani 400 ucun bozuk olmasindan degil, BASLIKTAN geliyor.
        [Fact]
        public async Task AyniV1Basligi_BASKA_BIR_UCTA_HALA_400_KAPSAM_DAR_KALDI()
        {
            if (Skipped()) return;
            var client = _factory!.CreateClient();

            var basliksiz = await client.GetAsync("/api/category/getlist");
            ((int)basliksiz.StatusCode).Should().Be(200,
                "referans olcum: uc BASLIKSIZ calisiyor (aksi halde 400 baslikla ilgili olmazdi)");

            var istek = new HttpRequestMessage(HttpMethod.Get, "/api/category/getlist");
            istek.Headers.TryAddWithoutValidation("X-Api-Version", "V1");
            var baslikli = await client.SendAsync(istek);

            baslikli.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "gevseme YALNIZ webhook icin - surum okuyucusu diger uclarda AYNEN duruyor");
        }

        // ── 3) IMZASIZ GERCEK BILDIRIM RETRIEVE OTORITESIYLE ISLENIR ────────────────────────
        [Fact]
        public async Task ImzasizGercekBildirim_RETRIEVE_OTORITESIYLE_Islenir()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);

            var resp = await _factory!.CreateClient().SendAsync(WebhookIstegi(token));

            resp.StatusCode.Should().Be(HttpStatusCode.OK, "gercek bildirimde imza YOK - reddedilmemeli");
            var (odeme, siparis) = await DurumAsync(orderId);
            odeme.Should().Be((byte)PaymentStatusEnum.Success);
            siparis.Should().Be((byte)OrderStatusEnum.Confirmed);
            SayanIyzicoClient.RetrieveCallCount.Should().Be(1,
                "otorite imza degil SUNUCU-SUNUCU sorgu - tam bir kez sorulmali");
        }

        // ── 4) BIZIM OLMAYAN TOKEN: IYZICO'YA HIC CIKILMAZ ──────────────────────────────────
        //
        // AMPLIFIKASYON SINIRININ OLCUMU. "Her sahte istek bir retrieve" endisesi OLCULDU ve
        // dar cikti: HandleCallback retrieve'e gelmeden ONCE token'i BIZIM tablomuzda ariyor.
        // Bizim olmayan token 404 ile duser ve DISARI HIC CIKILMAZ (sayac 0). Retrieve'e ancak
        // bizim urettigimiz + hala Pending + 30 dk'dan yeni bir token ulasabilir.
        [Fact]
        public async Task BizimOlmayanToken_404_ve_IYZICOYA_HIC_CIKILMAZ_AmplifikasyonDAR()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();

            var resp = await _factory!.CreateClient()
                .SendAsync(WebhookIstegi($"uydurma-{Guid.NewGuid():N}"));

            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, "bizim olmayan token islenmez");
            SayanIyzicoClient.RetrieveCallCount.Should().Be(0,
                "sahte token saglayiciya HIC gitmemeli - amplifikasyon kanali burada daraliyor");
        }

        // ── 5) BIZIM TOKEN AMA RETRIEVE DUSUYOR: YAN ETKISIZ REDDEDILIR ─────────────────────
        //
        // "Sahte bildirim"in ikinci okumasi: token bizim ve Pending, ama Iyzico'da odeme YOK.
        // Sunucu-sunucu sorgu bunu soyler; yan etki uretilmemeli.
        [Fact]
        public async Task TokenBIZIM_ama_RETRIEVE_DUSERSE_YanEtkiSIZ_Reddedilir()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            SayanIyzicoClient.RetrieveOverride = _ => new IyzicoPaymentResult
            {
                Success = false,
                PaidPrice = 0m,
                Currency = "TRY",
                FraudStatus = "1"
            };

            var resp = await _factory!.CreateClient().SendAsync(WebhookIstegi(token));

            ((int)resp.StatusCode).Should().Be(400, "saglayici odemeyi dogrulamiyorsa bildirim kabul edilmez");
            var (odeme, siparis) = await DurumAsync(orderId);
            odeme.Should().Be((byte)PaymentStatusEnum.Failed);
            siparis.Should().Be((byte)OrderStatusEnum.Cancelled);

            await using var ctx = NewContext();
            (await ctx.Set<Invoice>().CountAsync(i => i.order_id == orderId)).Should().Be(0,
                "dogrulanmamis odemeye fatura kesilmez (S7)");
            (await ctx.Set<LoyaltyTransaction>().CountAsync(t => t.order_id == orderId)).Should().Be(0,
                "dogrulanmamis odeme puan kazandirmaz");
            (await ctx.Set<OutboxMessage>().CountAsync(m => m.event_type == "PaymentConfirmed")).Should().Be(0,
                "yan etki mesaji HIC yazilmamali - 400 kozmetik degil");
        }

        // ── 6) AYNI TOKEN TEKRARI: ZATEN ISLENDI, YAN ETKI YOK ──────────────────────────────
        //
        // Webhook at-least-once bir kanaldir; Iyzico ayni bildirimi tekrarlayabilir. Ustelik
        // callback ve webhook AYNI odeme icin birlikte gelebilir. Ikinci teslimat sunucu-sunucu
        // sorguya bile ULASMAMALI (Pending guard'i once eler).
        [Fact]
        public async Task AyniTokenTekrari_ZATEN_ISLENDI_RetrieveARTMAZ_YanEtkiYOK()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            var client = _factory!.CreateClient();

            var ilk = await client.SendAsync(WebhookIstegi(token));
            ilk.StatusCode.Should().Be(HttpStatusCode.OK);
            SayanIyzicoClient.RetrieveCallCount.Should().Be(1, "vakum kirici: ilk teslimat gercekten islendi");

            int mesajSayisiIlk;
            await using (var ctx = NewContext())
                mesajSayisiIlk = await ctx.Set<OutboxMessage>().CountAsync(m => m.event_type == "PaymentConfirmed");
            mesajSayisiIlk.Should().Be(1, "ilk teslimat TEK yan etki mesaji yazmali");

            var ikinci = await client.SendAsync(WebhookIstegi(token));

            ikinci.StatusCode.Should().Be(HttpStatusCode.OK, "tekrar HATA degil - idempotent kabul");
            SayanIyzicoClient.RetrieveCallCount.Should().Be(1,
                "ikinci teslimat sunucu-sunucu sorguya ULASMAMALI");
            await using var son = NewContext();
            (await son.Set<OutboxMessage>().CountAsync(m => m.event_type == "PaymentConfirmed")).Should().Be(1,
                "ikinci teslimat IKINCI bir yan etki mesaji yazmamali");
            (await son.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId))
                .status.Should().Be((byte)OrderStatusEnum.Confirmed);
        }

        // ── 7) IMZA GELIRSE AYNEN DOGRULANIR (GEVSEMENIN SINIRI) ───────────────────────────
        //
        // Gevseme "imzayi yok say" DEGIL, "imza YOKSA retrieve otoritesiyle isle". Uc dal da
        // ayni testte: govdedeki yanlis imza, BASLIKTAKI yanlis imza, ve DOGRU imza.
        // Sonuncusu VAKUM KIRICI - o olmadan "her imzayi reddeden" bozuk bir uygulama da
        // ilk iki asserti gecerdi.
        [Fact]
        public async Task ImzaGELIRSE_DOGRULANIR_Govde_ve_BASLIK_YanlisImzayi_REDDEDER()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var client = _factory!.CreateClient();

            // (a) GOVDEDE yanlis imza
            var (orderA, tokenA) = await NewPendingPaymentAsync(_factory!);
            var govdeYanlis = await client.SendAsync(WebhookIstegi(tokenA, govdeImzasi: "00"));
            govdeYanlis.StatusCode.Should().Be(HttpStatusCode.BadRequest, "govdedeki imza tutmuyorsa reddedilir");
            (await DurumAsync(orderA)).payment.Should().Be((byte)PaymentStatusEnum.Pending,
                "reddedilen bildirim odemeyi ISLEMEMELI");

            // (b) BASLIKTA yanlis imza - baslik yolunun GERCEKTEN bagli oldugunun kaniti
            var (orderB, tokenB) = await NewPendingPaymentAsync(_factory!);
            var baslikYanlis = await client.SendAsync(WebhookIstegi(tokenB, baslikImzasi: "00"));
            baslikYanlis.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "X-Iyz-Signature dolu gelirse dogrulanir - baslik yolu bagli olmasa bu istek 200 olurdu");
            (await DurumAsync(orderB)).payment.Should().Be((byte)PaymentStatusEnum.Pending);

            SayanIyzicoClient.RetrieveCallCount.Should().Be(0,
                "imzasi tutmayan bildirim saglayiciya HIC cikmamali");

            // (c) DOGRU imza - vakum kirici
            var (orderC, tokenC) = await NewPendingPaymentAsync(_factory!);
            var dogru = await client.SendAsync(WebhookIstegi(tokenC, govdeImzasi: Sign(tokenC)));
            dogru.StatusCode.Should().Be(HttpStatusCode.OK, "DOGRU imza reddedilmemeli");
            (await DurumAsync(orderC)).payment.Should().Be((byte)PaymentStatusEnum.Success);
        }

        // ── 8) BEDEL SINIRI: WEBHOOK "payment" KOVASINDA ───────────────────────────────────
        //
        // AYRI HOST (uretim varsayilani PaymentPermitLimit=10). Test sunucusunda RemoteIpAddress
        // null oldugu icin tum istekler ayni partition'a duser - olculen sey limitin DEGERI ve
        // ucun kapsama GIRDIGI. Istekler bizim olmayan token'la yapilir: 404 doner, saglayiciya
        // cikilmaz, yani olcum ucuz ve yan etkisizdir.
        // CIFT-ANLAM KIRICI: ilk on istek 404 aliyor - yani UYGULAMAYA ULASIYORLAR; 429 yalniz
        // on birincide geliyor.
        [Fact]
        public async Task Webhook_PAYMENT_KOVASINDA_OnBirinci_Istek_429()
        {
            if (Skipped()) return;
            var client = _limitFactory!.CreateClient();

            var kodlar = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                var r = await client.SendAsync(WebhookIstegi($"uydurma-{Guid.NewGuid():N}"));
                kodlar.Add((int)r.StatusCode);
            }

            kodlar.Should().AllBeEquivalentTo(404,
                $"ilk on istek uygulamaya ULASMALI (limite takilmadan). Kodlar: {string.Join(",", kodlar)}");

            var onBirinci = await client.SendAsync(WebhookIstegi($"uydurma-{Guid.NewGuid():N}"));
            ((int)onBirinci.StatusCode).Should().Be(429,
                "webhook 'payment' kovasinda (10/dk) - Redis yolundaki /payment/ limitiyle ayni");
        }

        // ── 9) SUPHELI #15: GECIKMIS GERCEK BILDIRIM FAILED'LANMAZ ─────────────────────────
        //
        // 30 dk'lik token yasi guard'i TARAYICI replay'i icin dogru bir savunmadir, ama webhook
        // FARKLI zamanlama karakteristigine sahip bir kanaldir: saglayici bildirimi geciktirebilir
        // ya da saatler sonra yeniden deneyebilir. Sinir burada da uygulaninca GECIKMIS ama
        // GERCEK bir bildirim, parasi ALINMIS bir odemeyi "Failed" diye defterliyordu.
        // CANLI ORNEK: siparis #33 - Iyzico'da SUCCESS, bizde Pending; token 58 dakikalik oldugu
        // icin tekrar tetiklemek onu Failed yapardi.
        [Fact]
        public async Task GECIKMIS_GercekBildirim_WEBHOOKTA_FAILEDLANMAZ_Confirmeda_Tasir()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            await GeriTarihliYapAsync(orderId, TimeSpan.FromHours(2));   // 30 dk siniri ASILDI

            var resp = await _factory!.CreateClient().SendAsync(WebhookIstegi(token));

            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                "gecikmis ama GERCEK bildirim reddedilmemeli - otorite yas degil retrieve");
            var (odeme, siparis) = await DurumAsync(orderId);
            odeme.Should().Be((byte)PaymentStatusEnum.Success,
                "parasi alinmis odeme 'Failed' diye defterlenmemeli");
            odeme.Should().NotBe((byte)PaymentStatusEnum.Failed,
                "SUPHELI #15'in tam zarari buydu - acikca disarida birakiliyor");
            siparis.Should().Be((byte)OrderStatusEnum.Confirmed);
            SayanIyzicoClient.RetrieveCallCount.Should().Be(1,
                "yas guard'i atlandi diye sunucu-sunucu dogrulama atlanmadi");
        }

        // ── 10) GEVSEME KANALA BAGLI: TARAYICI YOLUNDA GUARD AYNEN DURUYOR ─────────────────
        //
        // CIFT-ANLAM KIRICI. Test 9 tek basina "yas guard'i tumden kaldirildi" ile de yesil
        // kalirdi. Burada AYNI yaslandirma TARAYICI callback'ine gonderiliyor ve orada odeme
        // hala Failed olmali - yani gevseyen sey KANAL BAZLI.
        [Fact]
        public async Task AyniGecikme_TARAYICI_CALLBACKINDE_TokenYasi_Guardina_TAKILIR()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            await GeriTarihliYapAsync(orderId, TimeSpan.FromHours(2));

            var govde = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token,
                ["signature"] = ""
            });
            var resp = await _factory!.CreateClient().PostAsync("/api/payment/callback", govde);

            var govdeMetni = await resp.Content.ReadAsStringAsync();
            ((int)resp.StatusCode).Should().Be(400,
                $"tarayici yolunda eski token REDDEDILIR - replay savunmasi burada dogru. Govde: {govdeMetni}");
            govdeMetni.Should().Contain("zaman aşımına",
                "400 SEBEBI yas olmali - imza ya da baska bir sebep degil (cift-anlam kirici)");
            var (odeme, siparis) = await DurumAsync(orderId);
            odeme.Should().Be((byte)PaymentStatusEnum.Failed);
            siparis.Should().Be((byte)OrderStatusEnum.Pending);
            SayanIyzicoClient.RetrieveCallCount.Should().Be(0,
                "yas guard'i retrieve'den ONCE eler - saglayiciya cikilmaz");
        }

        // ── 11) VARSAYILAN KANAL STRICT: FAIL-CLOSED ──────────────────────────────────────
        //
        // Servisi DOGRUDAN, kanal VERMEDEN cagirmak en KATI davranisi almali. Gecerli bir imza
        // gonderiliyor ki 400'un sebebi imza DEGIL, YAS olsun (cift-anlam kirici).
        [Fact]
        public async Task VARSAYILAN_KANAL_STRICT_GecikmisTokeni_REDDEDER_FailClosed()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            await GeriTarihliYapAsync(orderId, TimeSpan.FromHours(2));

            var sonuc = await WithScopeAsync(_factory!, sp => sp.GetRequiredService<IPaymentService>()
                .HandleCallback(new PaymentCallbackRequestDto { token = token, signature = Sign(token) }));

            ((int)sonuc.Item1).Should().Be(400, "varsayilan kanal Strict - gevseme ACIKCA secilir");
            sonuc.Item2.Message.Should().Contain("zaman aşımına",
                "red sebebi YAS olmali; imza GECERLI gonderildi");
            (await DurumAsync(orderId)).payment.Should().Be((byte)PaymentStatusEnum.Failed);
        }

        // Rezervasyonu GERCEK temizlik yolundan gecirerek Expired'a dusurur.
        // Sahte kurgu DEGIL: uretimdeki Hangfire job'inin cagirdigi metodun KENDISI kosuyor.
        private async Task<(int productId, string size, int stokOnce)> RezervasyonuEXPIREEtAsync(int orderId)
        {
            int productId; string size;
            await using (var ctx = NewContext())
            {
                var kalem = await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(i => i.order_id == orderId);
                productId = kalem.product_id; size = kalem.size;
                var rez = await ctx.Set<StockReservation>().SingleAsync(r => r.order_id == orderId);
                rez.expires_at = DateTime.Now.AddMinutes(-1);
                await ctx.SaveChangesAsync();
            }

            await WithScopeAsync(_factory!, sp => sp.GetRequiredService<IStockService>().ReleaseExpiredReservations());

            await using var son = NewContext();
            (await son.Set<StockReservation>().AsNoTracking().SingleAsync(r => r.order_id == orderId))
                .status.Should().Be((byte)ReservationStatusEnum.Expired, "on kosul: rezervasyon expire olmali");
            var stok = await son.Set<ProductStock>().AsNoTracking()
                .SingleAsync(s => s.product_id == productId && s.size == size);
            stok.reserved_quantity.Should().Be(0, "on kosul: temizlik rezerveyi serbest birakmali");
            return (productId, size, stok.stock_quantity);
        }

        // ── 12) SUPHELI #18 DUZELTILDI: EXPIRE OLMUS REZERVASYONDA STOK DUSER ─────────────
        //
        // ══ BILINCLI KIRILAN PIN - KAYIT ══════════════════════════════════════════════════
        // Bu testin adi eskiden
        // SUPHELI_RezervasyonEXPIRE_Olduysa_Onay_STOK_DUSURMUYOR_ve_UYARI_YAZMIYOR_PINLENIR
        // idi ve OLCULEN SUPHELI davranisi sabitliyordu (stok DUSMEZ + hareket kaydi YOK).
        // Kullanici karariyla #18 duzeltildi; eski pin artik envanter sapmasini SAVUNUR hale
        // gelirdi, bu yuzden KIRILDI ve yerini duzeltilmis-davranis pinleri aldi.
        //
        // Senaryo siparis #33'un CANLI kurtarmasindan birebir aliniyor: rezervasyon expire
        // olmus, odeme gecikmis bir webhook bildirimiyle onaylaniyor.
        [Fact]
        public async Task RezervasyonEXPIRE_Olsa_da_Onay_STOK_DUSURUR_ve_HAREKET_YAZAR()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            var (productId, size, stokOnce) = await RezervasyonuEXPIREEtAsync(orderId);

            await GeriTarihliYapAsync(orderId, TimeSpan.FromHours(2));
            var resp = await _factory!.CreateClient().SendAsync(WebhookIstegi(token));

            // Odeme tarafi (vakum kirici): bu test "hicbir sey olmadi" ile yesil kalmaz.
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var (odeme, siparis) = await DurumAsync(orderId);
            odeme.Should().Be((byte)PaymentStatusEnum.Success);
            siparis.Should().Be((byte)OrderStatusEnum.Confirmed);

            await using var son = NewContext();
            var stokSonra = await son.Set<ProductStock>().AsNoTracking()
                .SingleAsync(s => s.product_id == productId && s.size == size);
            stokSonra.stock_quantity.Should().Be(stokOnce - 1,
                "expire olmus rezervasyonda da stok DUSMELI - siparis #33'te dusmuyordu");
            stokSonra.reserved_quantity.Should().Be(0,
                "rezerve ZATEN serbestti; dogrudan dusum onu EKSIYE cekmemeli");

            var hareketler = await son.Set<StockMovement>().AsNoTracking()
                .Where(m => m.reference_id == orderId).ToListAsync();
            hareketler.Should().HaveCount(1, "envanter defterine TEK satir yazilmali");
            hareketler[0].quantity.Should().Be(1);
            hareketler[0].note.Should().Contain("expire",
                "not, stogun NEDEN dogrudan dusuldugunu soylemeli (cift-anlam kirici: normal " +
                "onay notu 'Sipariş - ödeme onaylı stok düşümü' ile karismasin)");

            // Rezervasyon Confirmed'a gecmeli: aksi halde ikinci bir onay cagrisi AYNI satiri
            // TEKRAR dusurebilirdi (Expired artik normal bir yol oldugu icin bu risk gercek).
            (await son.Set<StockReservation>().AsNoTracking().SingleAsync(r => r.order_id == orderId))
                .status.Should().Be((byte)ReservationStatusEnum.Confirmed);
        }

        // ── 13) STOK YETMIYORSA UYARI ZAMAN CIZELGESINE DUSER ────────────────────────────
        //
        // Duzeltmenin ikinci yarisi: SESSIZ HICBIR YOL KALMAZ. Rezervasyon expire olmus VE bu
        // arada stok tukenmisse odeme yine alinmistir - operator bunu GORMELIDIR.
        // Iki kanal: envanter defteri (stock_movements notu) + siparis zaman cizelgesi.
        [Fact]
        public async Task RezervasyonEXPIRE_ve_STOK_TUKENMISSE_UYARI_ZAMAN_CIZELGESINE_Duser()
        {
            if (Skipped()) return;
            SayanIyzicoClient.Sifirla();
            var (orderId, token) = await NewPendingPaymentAsync(_factory!);
            var (productId, size, _) = await RezervasyonuEXPIREEtAsync(orderId);

            // Rezerve serbest kaldiktan SONRA stok baskasina gitti - fiziksel stok 0.
            await using (var ctx = NewContext())
            {
                var stok = await ctx.Set<ProductStock>().SingleAsync(s => s.product_id == productId && s.size == size);
                stok.stock_quantity = 0;
                await ctx.SaveChangesAsync();
            }

            await GeriTarihliYapAsync(orderId, TimeSpan.FromHours(2));
            var resp = await _factory!.CreateClient().SendAsync(WebhookIstegi(token));

            resp.StatusCode.Should().Be(HttpStatusCode.OK, "odeme ALINDI - bildirim reddedilmez");
            (await DurumAsync(orderId)).payment.Should().Be((byte)PaymentStatusEnum.Success);

            await using var son = NewContext();
            (await son.Set<ProductStock>().AsNoTracking()
                .SingleAsync(s => s.product_id == productId && s.size == size))
                .stock_quantity.Should().Be(0, "olmayan stok EKSIYE cekilmemeli");

            var hareket = await son.Set<StockMovement>().AsNoTracking()
                .SingleAsync(m => m.reference_id == orderId);
            hareket.note.Should().Contain("UYARI", "birinci kanal: envanter defteri");

            var notlar = await son.Set<OrderStatusHistory>().AsNoTracking()
                .Where(h => h.order_id == orderId).Select(h => h.note!).ToListAsync();
            notlar.Should().Contain(n => n.Contains("UYARI") && n.Contains("stok yok"),
                "ikinci kanal: siparis zaman cizelgesi - operatorun panelde GORDUGU yer. " +
                "Bu assert olmadan 'sessiz hicbir yol kalmaz' iddiasi kanitlanmis olmaz.");
        }
    }
}
