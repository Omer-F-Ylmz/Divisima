using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
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
    // E3'TE BULUNAN VE E3'TE DUZELTILEN URETIM HATASI - UC DUZEYINDE PINLENDI.
    //
    // KOK SEBEP (DUZELTILMEDI - Sprint 8 madde 11): SuccessDataResult<T> dort kurucuya sahip:
    //     (T data, string message) / (T data) / (string message) / ()
    // T = string oldugunda "(T data)" ile "(string message)" AYNI IMZAYA duser. C# asiri yukleme
    // cozumu bu durumda generic OLMAYAN adayi (yani "(string message)") secer. Sonuc: tek argumanli
    // "new SuccessDataResult<string>(x)" cagrisinda x MESSAGE'a gider, DATA null kalir - ve Success
    // yine true oldugu icin HATA SESSIZDIR.
    //
    // OLCULEN ZARAR (canli, E3 elle dogrulamasinda):
    //   1) OrderManager.GetInvoiceHtml -> OrderController.InvoiceHtml "Content(ok.Data, ...)" yazar
    //      -> Data null -> GET /api/order/{id}/invoice-html : HTTP 200 ama Content-Length: 0.
    //         Yani "Faturalarim" ekrani HIC CALISMAMISTI.
    //   2) ReferralManager.GetOrCreateMyCode -> GET /api/referral/my-code :
    //         {"data":null,"success":true,"message":"REF351E93"} - kodu "data"dan okuyan istemci BOS alir.
    //
    // E3 DUZELTMESI (KAPSAM SINIRLI): yalniz bu IKI cagri "data:" ADLANDIRILMIS ARGUMANA cevrildi.
    // Kurucu SETINE DOKUNULMADI - belirsizlik dilde duruyor, YENI yazilacak tek argumanli bir
    // string cagrisi yine sessizce bozuk olur. Kokten cozum (kurucu seti yeniden tasarimi ya da
    // analyzer/kural) SPRINT 8 MADDE 11.
    //
    // ONCEKI PIN BILINCLI KIRILDI: "SuccessDataResultString_TEK_ARGUMAN_MESSAGE_a_GIDER_DATA_NULL_
    // KALIR_PINLENIR" bozuk davranisi KABUL EDILMIS gibi sabitliyordu; duzeltmeyle birlikte
    // yerini asagidaki UC DUZEYI pinler aldi. Kurucu duzeyindeki DOGRU-DAVRANIS ve KARSIT
    // kontrol pinleri korundu.
    [Trait("Category", "Sql")]
    public class ResultOverloadPinTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaResultOverloadTest";
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

        private sealed class OverloadFactory : WebApplicationFactory<Program>
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

        private OverloadFactory? _factory;
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
                _factory = new OverloadFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak sonuc-asiri-yukleme testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // Faturasi cekilecek GERCEK bir siparis kurar. Kategori GERCEKTEN olusturulur, urunun
        // description/color_hex alanlari doldurulur (zorunlu alanlar). Her cagri kendi verisini
        // uretir - var olan satirlara guvenilmez.
        // MANTIK-FIX-1 / K2-A: `magazaKredisi` parametresi EKLENDI (varsayilan 0 - mevcut
        // cagiranlar ETKILENMEZ). Gerekce: kredi tasiyan bir siparis olmadan K2-A'nin
        // davranisi olculemez; depoda kredi POZITIF olan tek bir test fiksturu YOKTU.
        // `total_price` KREDIYI ICERIR (D1/K2-A karari: semantik DEGISMEDI) - fikstur
        // uretimdeki OrderManager.cs:294/:322 kalibiyla AYNI sekilde kurulur.
        // MANTIK-FIX-2R / K2: KARISIK ORANLI sepet kurgusu (%10 giyim + %20 aksesuar).
        // Gerekce: canli veride karisik oranli fatura VAR (fatura 55 -> agirlikli oran 0,1416)
        // ama o kayit BASKA bir musteriye ait ve uc SAHIPLIK kontrollu, admin gecisi YOK -
        // yani o satirla canli kanit URETILEMEZ. Kirilim sozlesmesi bu yuzden KURGUYLA pinlenir.
        private static async Task<(int OrderId, string OrderNumber)> SeedKarisikOranliSiparisAsync(int customerId, bool bedavaKargoTekOran = false)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            await using var ctx = NewContext();

            async Task<int> UrunAsync(decimal oran, decimal fiyat, string ad)
            {
                var k = new Category
                {
                    name = ad + " Kat " + damga,
                    slug = (ad + "-kat-" + damga).ToLowerInvariant(),
                    vat_rate = oran,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(k);
                await ctx.SaveChangesAsync();

                var u = new Product
                {
                    name = ad + " " + damga,
                    brand = "Divisima",
                    category_id = k.id,
                    price = fiyat,
                    description = "Karisik oran kurgusu.",
                    color_hex = "#334455",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Product>().Add(u);
                await ctx.SaveChangesAsync();
                return u.id;
            }

            var giyim = await UrunAsync(0.10m, 1100m, "Giyim");
            var aksesuar = await UrunAsync(0.20m, 240m, "Aksesuar");

            var siparis = new Order
            {
                customer_id = customerId,
                order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-K" + damga.Substring(0, 7),
                status = (byte)3,
                subtotal = (bedavaKargoTekOran ? 1100.00m : 1340.00m),
                discount_amount = 0m,
                shipping_cost = (bedavaKargoTekOran ? 0m : 49.90m),
                total_price = (bedavaKargoTekOran ? 1100.00m : 1389.90m),
                currency = "TRY",
                payment_type = 0,
                is_online_payment_done = true,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(siparis);
            await ctx.SaveChangesAsync();

            ctx.Set<OrderItem>().Add(new OrderItem { order_id = siparis.id, product_id = giyim, size = "M", quantity = 1, unit_price = 1100m, is_cancelled = false, created_at = DateTime.Now });
            // bedavaKargoTekOran: SADECE %10 urun kalir. Kargo kalemi (K1/D1) yine yazilir ama
            // tutari 0,00'dir; boylece B1'in hayalet %20 grubu URETILEBILIR bir kosul olur.
            if (!bedavaKargoTekOran)
                ctx.Set<OrderItem>().Add(new OrderItem { order_id = siparis.id, product_id = aksesuar, size = "TEK", quantity = 1, unit_price = 240m, is_cancelled = false, created_at = DateTime.Now });
            await ctx.SaveChangesAsync();

            return (siparis.id, siparis.order_number);
        }

        private static async Task<(int OrderId, string OrderNumber, string ProductName)> SeedOrderAsync(
            int customerId, decimal magazaKredisi = 0m)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            await using var ctx = NewContext();

            var kategori = new Category
            {
                name = "Fatura Kategori " + damga,
                slug = "fatura-kat-" + damga.ToLowerInvariant(),
                display_order = 1,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Fatura Urunu " + damga,
                brand = "Divisima",
                category_id = kategori.id,
                price = 250.00m,
                description = "Fatura pini icin urun.",
                color_hex = "#112233",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(urun);
            await ctx.SaveChangesAsync();

            var siparis = new Order
            {
                customer_id = customerId,
                order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + damga,
                status = (byte)3,                  // Shipped - fatura ucu durum guard'i TASIMIYOR (bkz. SUPHELI)
                subtotal = 500.00m,
                discount_amount = 0m,
                shipping_cost = 49.90m,
                total_price = 549.90m,
                store_credit_used = magazaKredisi,
                currency = "TRY",
                payment_type = 0,
                is_online_payment_done = true,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(siparis);
            await ctx.SaveChangesAsync();

            ctx.Set<OrderItem>().Add(new OrderItem
            {
                order_id = siparis.id,
                product_id = urun.id,
                size = "M",
                quantity = 2,
                unit_price = 250.00m,
                is_cancelled = false,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            return (siparis.id, siparis.order_number, urun.name);
        }

        // ── 1) UC DUZEYI PIN: FATURA GOVDESI BOS DEGIL ─────────────────────────────────
        //
        // Bu pin, olculen zarari (HTTP 200 + Content-Length: 0) DOGRUDAN uctan olcer. Kurucu
        // duzeyinde degil UC duzeyinde durmasinin sebebi: zarar Content(ok.Data) satirinda
        // gorunur hale geliyor; istemcinin gordugu sey budur.
        //
        // VAKUM KIRICI: yalniz "bos degil" demiyoruz - govdenin GERCEKTEN o siparisin faturasi
        // oldugunu (siparis numarasi + urun adi + toplam) dogruluyoruz. Aksi halde tek bosluk
        // karakteri donduren bir uc de testi gecerdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task FaturaHTML_Ucu_DOLU_GOVDE_Doner_ContentLength_SIFIR_DEGIL()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var seed = await SeedOrderAsync(user.CustomerId);

            // MANTIK-FIX-2R / K2: KURGU DURUSTLESTI. Onceden bu bacak da YALNIZ bir Order
            // satiri yaziyordu (olculdu: "new Invoice" 0, "GenerateForOrder" 0) - uc faturayi
            // SIPARISTEN YENIDEN HESAPLADIGI icin fatura OLMADAN da belge uretiyordu.
            // Artik uc KAYITTAN besleniyor; fatura URETIM YOLUNDAN kesilir.
            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                var gen = await inv.GenerateForOrder(seed.OrderId);
                gen.Item1.Should().Be(HttpStatusCode.OK, $"fatura uretilmeli: {gen.Item2.Message}");
            }

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            // OLCULEN BELIRTININ KENDISI: zarar sirasinda uc "200 + Content-Length: 0" donuyordu
            // (curl ile sunucu tarafinda olculdu). Basligi DOGRUDAN pinliyoruz - bu iddia
            // yanit HTML iken de JSON iken de AYNI seyi korur: uc BOS GOVDE donmemeli.
            resp.Content.Headers.ContentLength.Should().BeGreaterThan(0,
                "Content-Length: 0 tam olarak E3'te olculen zarardir");

            var govde = await resp.Content.ReadAsStringAsync();
            govde.Should().NotBeNullOrWhiteSpace(
                "tek argumanli SuccessDataResult<string> kullanilirsa Data null kalir ve uc " +
                "200 + Content-Length: 0 doner - fatura ekrani BOS gelir (E3'te canli olculdu)");

            // VAKUM KIRICI (KORUNDU, KAYNAGI DEGISTI): govde GERCEKTEN bu siparisin faturasi
            // olmali. Eskiden bu bilgi siparisten yeniden hesaplanmis HTML'den okunuyordu;
            // artik KAYITTAN gelen yapilandirilmis veriden.
            using var belge = JsonDocument.Parse(govde);
            var veri = belge.RootElement.GetProperty("data");
            veri.GetProperty("has_invoice").GetBoolean().Should().BeTrue();
            veri.GetProperty("order_number").GetString().Should().Be(seed.OrderNumber,
                "yanit GERCEKTEN bu siparisin faturasi olmali");
            veri.GetProperty("items").EnumerateArray()
                .Any(x => x.GetProperty("product_name").ValueKind == JsonValueKind.String
                       && x.GetProperty("product_name").GetString() == seed.ProductName)
                .Should().BeTrue("urun kalemi yanitta bulunmali");

            // KULTUR SOZLESMESI - YENI BICIMI (MANTIK-FIX-2R / K2).
            // ESKI hali "govde tr bicimli '549,90' ICERMELI" idi; o assert sunucunun parayi
            // BICIMLEDIGINI varsayiyordu ve dogru bicim tek bir kulture kilitliydi.
            // ARTIK: uc SAYI BICIMLEMEZ - alan HAM decimal gelir, bicimleme istemcide.
            // CIFT-ANLAM KIRICI: HER IKI bicim de yasak (yalniz invariant'i yasaklamak
            // sunucunun tr-TR'ye geri donmesini gormezdi).
            // OLCUM NOTU (yanlis pozitif ONLENDI): bu fiksturun toplami 549,90 ve o degerin
            // INVARIANT N2 bicimi "549.90" - yani HAM JSON sayisiyla KARAKTER KARAKTER AYNI.
            // Binlik ayraci olmadigi icin invariant NotContain'i burada AYIRT EDICI DEGIL;
            // konsaydi uc dogru davransa bile KIRMIZI verirdi (denendi ve birebir bu oldu).
            // Bu yuzden burada AYIRT EDEN olcutler kullanilir; CulturePinTests'te toplam
            // 1049,70 oldugu icin (tr "1.049,70" / invariant "1,049.70" - IKISI DE binlik
            // ayracli) HER IKI bicim de orada anlamli ve orada IKISI DE yasakli.
            var trBicim = 549.90m.ToString("N2", new CultureInfo("tr-TR"));   // "549,90" - JSON sayisinda VIRGUL ondalik OLAMAZ
            govde.Should().NotContain(trBicim,
                $"uc SAYI BICIMLEMEMELI - tr bicimli para dizgesi ('{trBicim}') yanitta bulunmamali");
            govde.Should().NotContain(" TL",
                "para birimi SONEKI de sunucuda eklenmemeli - eski HTML 'Genel Toplam: 549,90 TL' basiyordu");
            veri.GetProperty("total").ValueKind.Should().Be(JsonValueKind.Number,
                "toplam SAYI olmali; dizge olsaydi bicimleme sunucuda yapilmis olurdu");
            veri.GetProperty("total").GetDecimal().Should().Be(549.90m, "kayittan gelen brut");
        }

        // ── P-F2a) MANTIK-FIX-2R / K2 - FATURASIZ SIPARIS: BOS DURUM, BELGE UYDURULMAZ ──
        //
        // OLCULEN ONCE-DURUM: 143 siparisin 47'sinin faturasi YOK (45 Pending + 2 Cancelled)
        // ve "Fatura Goruntule" butonu KOSULSUZ ciziliyor. Eski uc faturayi SIPARISTEN
        // yeniden hesapladigi icin bu siparisler icin de TAM GORUNUMLU belge donduruyordu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task FaturasizSiparis_BOS_DURUM_Doner_BELGE_UYDURULMAZ()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var seed = await SeedOrderAsync(user.CustomerId);   // FATURA URETILMEDI - kasitli

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            resp.StatusCode.Should().Be(HttpStatusCode.OK, "bos durum bir HATA degil - uc 200 doner");

            using var belge = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var veri = belge.RootElement.GetProperty("data");

            veri.GetProperty("has_invoice").GetBoolean().Should().BeFalse("bu siparisin faturasi YOK");
            veri.GetProperty("items").GetArrayLength().Should().Be(0, "kalem UYDURULMAZ");
            veri.GetProperty("total").GetDecimal().Should().Be(0m, "faturasiz sipariste fatura toplami YOKTUR");
            veri.GetProperty("invoice_number").ValueKind.Should().Be(JsonValueKind.Null);

            // VAKUM KIRICI: bos durum "hicbir sey donme" DEGIL - siparis kimligi gelir ki
            // ekran "bu siparisin faturasi yok" diyebilsin.
            veri.GetProperty("order_number").GetString().Should().Be(seed.OrderNumber);
        }

        // ── P-F2b) MANTIK-FIX-2R / K2 - IPTALLI FATURA: BELGE GELIR, IPTAL ISARETIYLE ───
        //
        // OLCULEN ONCE-DURUM (canli, siparis 268): siparis IPTAL (status 5) ve faturasi IPTAL
        // (status 3) oldugu halde eski uc HTTP 200 ile TAM GORUNUMLU fatura donduruyordu -
        // govdede "iptal" gecisi 0 idi (negatif kontrol: "Toplam" 2). Ayni sinif canli veride
        // 8 sipariste daha var (2-8 ve 28).
        [Fact]
        [Trait("Category", "Sql")]
        public async Task IptalliFatura_BELGE_GELIR_ama_IPTAL_ISARETI_Tasir()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var seed = await SeedOrderAsync(user.CustomerId);

            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                (await inv.GenerateForOrder(seed.OrderId)).Item1.Should().Be(HttpStatusCode.OK);
            }

            // Siparis + fatura URETIM YOLUNDAN iptal edilir (elle satir yazilmaz).
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().SingleAsync(x => x.id == seed.OrderId);
                o.status = (byte)OrderStatusEnum.Cancelled;
                await ctx.SaveChangesAsync();
            }
            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                (await inv.CancelForOrder(seed.OrderId)).Item1.Should().Be(HttpStatusCode.OK);
            }

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            using var belge = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var veri = belge.RootElement.GetProperty("data");

            // IPTAL ISARETI - eski ekranda HIC YOKTU.
            veri.GetProperty("invoice_is_cancelled").GetBoolean().Should().BeTrue(
                "iptal edilmis fatura ekranda gecerliden AYIRT EDILEBILMELI");
            veri.GetProperty("order_is_cancelled").GetBoolean().Should().BeTrue();

            // CIFT-ANLAM KIRICI: "iptalliyse hicbir sey gosterme" YANLIS duzeltmedir -
            // fatura MALI BIR BELGEDIR, iptal edilse de tutarlariyla gorunur olmalidir.
            veri.GetProperty("has_invoice").GetBoolean().Should().BeTrue();
            veri.GetProperty("total").GetDecimal().Should().Be(549.90m, "belge tutarlariyla GELIR");
            veri.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        }

        // ── P-F2c) MANTIK-FIX-2R / D2 - ODEME OZETININ KAYNAGI SIPARIS VERISIDIR ────────
        //
        // D2 KARARI: `invoices` krediyi KAYDETMEZ (kredi bir ODEME ARACIDIR, belge BRUTTUR;
        // migration YOK). Dolayisiyla ekranin kredi/odeme ozeti SIPARIS verisinden gelir ve
        // bu KAYNAK pinlenir - fatura kalemlerinden AYRI bir "odeme" bolumudur.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task OdemeOzeti_KAYNAGI_SIPARIS_VERISI_Fatura_BRUT_KALIR()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var seed = await SeedOrderAsync(user.CustomerId, magazaKredisi: 100.00m);

            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                (await inv.GenerateForOrder(seed.OrderId)).Item1.Should().Be(HttpStatusCode.OK);
            }

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            using var belge = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var veri = belge.RootElement.GetProperty("data");
            var odeme = veri.GetProperty("payment");

            odeme.GetProperty("store_credit_used").GetDecimal().Should().Be(100.00m,
                "kredi SIPARIS verisinden gelmeli - invoices onu kaydetmiyor (D2)");
            odeme.GetProperty("order_total").GetDecimal().Should().Be(549.90m);
            odeme.GetProperty("remaining").GetDecimal().Should().Be(449.90m, "549,90 - 100,00");

            // CIFT-ANLAM KIRICI: BELGE BRUT KALIR - kredi matrahi/toplami DUSURMEZ.
            veri.GetProperty("total").GetDecimal().Should().Be(549.90m,
                "fatura BRUTTUR; kredi bir odeme aracidir, belgeyi kucultmez");
            (veri.GetProperty("subtotal").GetDecimal() + veri.GetProperty("tax_amount").GetDecimal())
                .Should().Be(549.90m, "matrah + KDV = brut");
        }

        // ── P-F2d) MANTIK-FIX-2R / K2 - KDV KIRILIMI: TEK ORAN GOSTERILMEZ ─────────────
        //
        // OLCULEN ONCE-DURUM: ekran sabit "KDV (%20)" yaziyordu ve `invoices.tax_rate` HIC
        // okunmuyordu. Baslik tax_rate artik AGIRLIKLI ORTALAMA - ekrana oran olarak cikarsa
        // Turkiye'de var olmayan bir deger beyan edilirdi (canli ornek: fatura 55 -> 0,1416).
        // Bu yuzden ekran oran BAZINDA kirilim gosterir.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task KarisikOranliFatura_KDV_KIRILIMI_Doner_TEK_ORAN_DEGIL()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var seed = await SeedKarisikOranliSiparisAsync(user.CustomerId);

            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                (await inv.GenerateForOrder(seed.OrderId)).Item1.Should().Be(HttpStatusCode.OK);
            }

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            using var belge = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var veri = belge.RootElement.GetProperty("data");

            var kirilim = veri.GetProperty("vat_breakdown").EnumerateArray().ToList();
            kirilim.Count.Should().BeGreaterThan(1,
                "karisik oranli sepette kirilim BIRDEN FAZLA grup icermeli - tek oran YANLIS beyandir");
            kirilim.Select(g => g.GetProperty("vat_rate").GetDecimal()).Should().Contain(0.10m);
            kirilim.Select(g => g.GetProperty("vat_rate").GetDecimal()).Should().Contain(0.20m);

            // Kirilim TOPLAMI belgeyle tutarli olmali.
            kirilim.Sum(g => g.GetProperty("vat_amount").GetDecimal())
                .Should().Be(veri.GetProperty("tax_amount").GetDecimal(), "kirilim KDV toplami = belge KDV'si");
            kirilim.Sum(g => g.GetProperty("gross_amount").GetDecimal())
                .Should().Be(veri.GetProperty("total").GetDecimal(), "kirilim brut toplami = belge brutu");

            // GOMULU ORAN 0 GECIS: yanit hicbir yerde sabit bir oran ETIKETI tasimamali.
            var govde = belge.RootElement.GetRawText();
            govde.Should().NotContain("KDV (%", "sabit oran etiketi ARTIK URETILMEMELI");
        }

        // ── P-F5) MK-4b DENETIM BULGUSU B1 - SIFIR KATKILI GRUP KIRILIMDA GORUNMEZ ────
        //
        // OLCULEN KUSUR: D1 sozlesmesi geregi BEDAVA kargoda da TAM 1 kargo kalemi yazilir
        // (tutar 0,00) ve K1 onu KOSULSUZ TaxRate ile damgalar. Kirilim kalemleri oran
        // BAZINDA gruplayinca, urunleri %10 olan bir sipariste ekrana
        // "KDV %20 (Matrah 0,00 TL) - 0,00 TL" satiri girerdi: TURKIYE'DE O SIPARIS ICIN
        // VAR OLMAYAN bir oran BEYAN EDILIRDI - K2'nin acildigi kusurun TAM AYNI SINIFI.
        //
        // SUZGEC KAYITTA DEGIL GORUNTULEMEDE: fatura kalemi (D1) AYNEN durur; yalniz hicbir
        // seye katki vermeyen grup kirilimda gosterilmez.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task BedavaKargo_KIRILIMDA_HAYALET_ORAN_GRUBU_URETMEZ_ama_KALEM_KAYITTA_KALIR()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var seed = await SeedKarisikOranliSiparisAsync(user.CustomerId, bedavaKargoTekOran: true);

            using (var scope = _factory!.Services.CreateScope())
            {
                var inv = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                (await inv.GenerateForOrder(seed.OrderId)).Item1.Should().Be(HttpStatusCode.OK);
            }

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            using var belge = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var veri = belge.RootElement.GetProperty("data");

            // VAKUM KIRICI: kosul GERCEKTEN kurulmus olmali - kargo 0,00 ve kalem KAYITTA VAR.
            var kalemler = veri.GetProperty("items").EnumerateArray().ToList();
            var kargoKalemi = kalemler.Where(k => k.GetProperty("is_shipping").GetBoolean()).ToList();
            kargoKalemi.Should().HaveCount(1,
                "D1: bedava kargoda da TAM 1 kargo kalemi YAZILIR - suzgec KAYDA dokunmaz");
            kargoKalemi[0].GetProperty("line_total").GetDecimal().Should().Be(0m,
                "bedava kargo kaleminin tutari 0,00 olmali - hayalet grubu URETEN kosul budur");

            // ASIL IDDIA: kirilimda sifir katkili grup YOK.
            var kirilim = veri.GetProperty("vat_breakdown").EnumerateArray().ToList();
            foreach (var g in kirilim)
            {
                var matrah = g.GetProperty("base_amount").GetDecimal();
                var kdv = g.GetProperty("vat_amount").GetDecimal();
                var brut = g.GetProperty("gross_amount").GetDecimal();
                (matrah != 0m || kdv != 0m || brut != 0m).Should().BeTrue(
                    "hicbir seye katki vermeyen bir oran grubu BEYAN EDILEMEZ");
            }

            // CIFT-ANLAM KIRICI: suzgec "her seyi ele" DEGIL - gercek oran GORUNMEYE DEVAM EDER.
            kirilim.Should().HaveCount(1, "tek gercek oran kalmali - hayalet %20 grubu GITMELI");
            kirilim[0].GetProperty("vat_rate").GetDecimal().Should().Be(0.10m);
            kirilim.Sum(g => g.GetProperty("vat_amount").GetDecimal())
                .Should().Be(veri.GetProperty("tax_amount").GetDecimal(),
                    "suzgecten sonra da kirilim KDV toplami = belge KDV'si");
            kirilim.Sum(g => g.GetProperty("gross_amount").GetDecimal())
                .Should().Be(veri.GetProperty("total").GetDecimal(),
                    "suzgecten sonra da kirilim brut toplami = belge brutu");
        }

        // ── P20) MANTIK-FIX-1 / K2-A - MAGAZA KREDISI SIPARIS DETAYINDA GORUNUR ────────
        // DAVRANIS pini (durust etiket): gercek HTTP ucu, gercek DB fiksturu.
        //
        // OLCULEN ONCE-DURUM (R-M2): OrderDetailResponseDto `store_credit_used` TASIMIYORDU
        // ([YOKLUK] taramasi: Dtos/ genelinde 0 satir; negatif kontrol shipping_cost BULUNUYOR).
        // Checkout krediyi DUSUYOR (api-bridge.js:1697) ama sonuc/detay ekranlari DTO'nun
        // `total` alanini basiyor ve o alan krediyi ICERIYOR -> AYNI SIPARIS icin ardisik
        // IKI EKRAN FARKLI TOPLAM gosteriyordu (849,80 <-> 949,80).
        //
        // D1 KARARI K2-A: alan EKLENIR, `total` SEMANTIGI DEGISMEZ. Bu pin IKISINI DE tutar -
        // cunku K2-B'ye kaymak PaymentRefundTests.cs:20'yi YESIL BIRAKARAK uretimi tersine
        // cevirir (tam-cuzdan sipariste tum iade OLMAYAN KARTA gider).
        [Fact]
        [Trait("Category", "Sql")]
        public async Task SiparisDetayi_MAGAZA_KREDISINI_Doner_ve_TOTAL_SEMANTIGI_DEGISMEZ()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var krediliSiparis = await SeedOrderAsync(user.CustomerId, magazaKredisi: 100.00m);
            var kredisizSiparis = await SeedOrderAsync(user.CustomerId);   // VAKUM KIRICI

            var resp = await user.Client.GetAsync($"/api/order/get/{krediliSiparis.OrderId}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");

            // (1) ASIL IDDIA: kredi alani DONER ve DOGRU degeri tasir.
            data.TryGetProperty("store_credit_used", out var kredi).Should().BeTrue(
                "siparis detayi magaza kredisini BILDIRMELI - musteri iki ekranda iki farkli " +
                "toplam gormemeli (R-M2'de olculen zarar buydu)");
            kredi.GetDecimal().Should().Be(100.00m);

            // (2) CIFT-ANLAM KIRICI: `total` SEMANTIGI DEGISMEDI - krediyi ICERIR.
            // K2-B'ye kayan bir uygulama bu asserti GECEMEZ. Semantik sessizce kaymasin diye
            // beklenen deger ACIKCA yaziliyor (D1 karari: MF-2'ye kadar boyle kalir).
            data.GetProperty("total").GetDecimal().Should().Be(549.90m,
                "K2-A karari: total_price KREDIYI ICERMEYE DEVAM EDER - semantik MF-2'ye ait");

            // (3) MUHASEBE KIMLIGI KORUNUR: subtotal - indirim + kargo = total.
            var subtotal = data.GetProperty("subtotal").GetDecimal();
            var indirim = data.GetProperty("discount_amount").GetDecimal();
            var kargo = data.GetProperty("shipping_cost").GetDecimal();
            (subtotal - indirim + kargo).Should().Be(data.GetProperty("total").GetDecimal(),
                "OrderCancellationMoneyTests'teki MUHASEBE KIMLIGI pini ile AYNI iddia - " +
                "K2-A bu kimligi BOZMAMALI");

            // (4) VAKUM KIRICI: kredi KULLANILMAYAN sipariste alan 0 doner ve `total` DEGISMEZ.
            // Bu olmadan "her siparise 100,00 yaz" diyen bir uygulama da (1)'i gecerdi.
            var r2 = await user.Client.GetAsync($"/api/order/get/{kredisizSiparis.OrderId}");
            using var doc2 = JsonDocument.Parse(await r2.Content.ReadAsStringAsync());
            var d2 = doc2.RootElement.GetProperty("data");
            d2.GetProperty("store_credit_used").GetDecimal().Should().Be(0m,
                "kredi kullanilmayan sipariste alan 0 olmali");
            d2.GetProperty("total").GetDecimal().Should().Be(549.90m,
                "kredi yokken de toplam AYNI - alan eklemek tutari DEGISTIRMEZ");
        }

        // ── 2) UC DUZEYI PIN: REFERANS KODU "data" ALANINDA DONER ──────────────────────
        //
        // Ayni kok sebebin ikinci cagri yeri. Burada zarar HTTP koduna YANSIMIYOR (200 + success
        // true), yalniz zarfin "data" alani bos kaliyor - bu yuzden CIFT-ANLAM kirici olarak
        // hem "data" dolu hem de DB'deki kodla AYNI oldugu dogrulanir.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task ReferansKodu_Ucu_KODU_data_ALANINDA_Doner_message_te_DEGIL()
        {
            if (Skipped()) return;

            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            var resp = await user.Client.GetAsync("/api/referral/my-code");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var kok = doc.RootElement;

            kok.TryGetProperty("data", out var dataAlani).Should().BeTrue("zarfta 'data' alani olmali");
            dataAlani.ValueKind.Should().NotBe(JsonValueKind.Null,
                "tek argumanli SuccessDataResult<string> kullanilirsa kod MESSAGE'a gider ve " +
                "'data' null kalir - kodu 'data'dan okuyan istemci BOS alir (E3'te canli olculdu)");

            var kod = dataAlani.GetString();
            kod.Should().StartWith("REF", "uretilen kod REF onekli olmali - POZITIF OLAY kosulu");

            await using var ctx = NewContext();
            var dbKod = (await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == user.CustomerId)).referral_code;
            kod.Should().Be(dbKod, "uctan donen kod DB'ye yazilan kodun AYNISI olmali");
        }

        // ── 3) KURUCU DUZEYI: IKI ARGUMANLI KURUCU DOGRU CALISIR ───────────────────────
        //
        // CIFT-ANLAM KIRICI: sorun "SuccessDataResult hep bozuk" degil; YALNIZ tek argumanli
        // string cagrisinda. Bu pin olmadan yukaridaki uc pinleri yanlis okunabilirdi.
        [Fact]
        public void SuccessDataResultString_IKI_ARGUMAN_DOGRU_CALISIR_DATA_DOLAR()
        {
            var r = new SuccessDataResult<string>("VERI", "mesaj");

            r.Data.Should().Be("VERI", "iki argumanli kurucu (T data, string message) ile eslesir");
            r.Message.Should().Be("mesaj");
        }

        // ── 4) KURUCU DUZEYI KARSIT KONTROL: T string DEGILSE BELIRSIZLIK YOK ──────────
        //
        // Belirsizligin KAYNAGINI sabitler: eskiden yalniz T = string oldugunda iki kurucu ayni
        // imzaya duserdi. Sprint 8 madde 11 tam olarak o daralmis yuzeyi hedefledi.
        [Fact]
        public void SuccessDataResult_StringOLMAYAN_TipTe_TEK_ARGUMAN_DATAYA_GIDER()
        {
            var r = new SuccessDataResult<int>(42);

            r.Data.Should().Be(42, "tek argumanli kurucu HER ZAMAN veri alir");
            r.Message.Should().BeNullOrEmpty();
        }

        // ── 5) SPRINT 8 MADDE 11: BELIRSIZLIK KOKTEN KALKTI ───────────────────────────
        //
        // E3'te bu cagri dizeyi MESSAGE'a yaziyor, Data'yi null birakiyordu ve Success true
        // oldugu icin hata SESSIZ kaliyordu. Cagri yerleri o zaman "data:" adlandirilmis
        // argumanla kurtarilmisti; belirsizligin KENDISI dilde duruyordu.
        // Sprint 8'de `SuccessDataResult<T>(string message)` kurucusu KALDIRILDI (depo tarandi:
        // 0 cagri). Artik tek argumanli cagrinin baska gidecek yeri YOK.
        //
        // BU PIN E3'TEKININ TERSIDIR - ayni ifade, ZITINI bekliyor. E3 pini bozuk davranisi
        // sabitliyordu; bu pin duzeltilmis davranisi sabitler.
        [Fact]
        public void SuccessDataResultString_TEK_ARGUMAN_ARTIK_DATAYA_GIDER_BELIRSIZLIK_KALKTI()
        {
            var r = new SuccessDataResult<string>("FATURA-HTML");

            r.Data.Should().Be("FATURA-HTML",
                "T=string olsa bile tek argumanli kurucu VERI alir - (string message) kurucusu artik YOK");
            r.Message.Should().BeNullOrEmpty("dize MESSAGE'a KAYMAMALI - E3'teki sessiz hatanin ta kendisiydi");
            r.Success.Should().BeTrue();
        }

        // CIFT-ANLAM KIRICI: ErrorDataResult'ta cozum TERS yonde - orada TUM cagrilar mesaj
        // niyetli oldugu icin (olculdu: 23 cagri, hepsi Messages.X) veri tasiyan kurucular
        // kaldirildi. Yani ayni "tek arguman" ifadesi iki sinifta FARKLI ve KESIN anlam tasiyor;
        // ikisi de artik belirsiz DEGIL.
        [Fact]
        public void ErrorDataResultString_TEK_ARGUMAN_HER_ZAMAN_MESAJDIR()
        {
            var r = new ErrorDataResult<string>("Bir hata olustu.");

            r.Message.Should().Be("Bir hata olustu.");
            r.Data.Should().BeNull("hata sonucu veri tasimaz - veri tasiyan kurucular kaldirildi");
            r.Success.Should().BeFalse();
        }
    }
}
