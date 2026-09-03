using System.Net;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Integrations.EInvoice;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 4 - KALEM BAZLI KDV
    //
    // ONCEKI DAVRANIS: fatura BASLIK duzeyinde tek oran tasiyordu ve KDV
    // "subtotal = total / (1 + oran)" ile ayristiriliyordu. Karisik sepette
    // (giyim %10 + aksesuar %20) bu matematiksel olarak YANLIS bir beyandi.
    //
    // YENI: her kalem kendi EFEKTIF orani ile hesaplanir
    //     Product.vat_rate ?? Category.vat_rate ?? EInvoice:KdvRate
    // ve bu oran faturaya KOPYALANIR (snapshot). Invoice.tax_rate'in anlami degisti:
    // artik kalemlerin AGIRLIKLI ORTALAMASI.
    [Trait("Category", "Sql")]
    public class InvoiceLineVatTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaInvoiceVatTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private static readonly string ConnStr =
            new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn)
            { InitialCatalog = TestDbAdi.Cozumle(DbName) }.ConnectionString;

        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using var ctx = NewContext();
                await TestDbKurulum.SilAsync(ctx.Database);
                await TestDbKurulum.OlusturAsync(ctx.Database);
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak kalem KDV testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await TestDbKurulum.SilAsync(ctx.Database); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // Saglayiciya giden satirlari yakalar - VatRate/VatAmount dolduruluyor mu gorulsun.
        private sealed class CapturingEInvoiceProvider : IEInvoiceProvider
        {
            public EInvoiceRequest? LastRequest { get; private set; }
            public Task<EInvoiceResult> SendInvoiceAsync(EInvoiceRequest request)
            {
                LastRequest = request;
                return Task.FromResult(new EInvoiceResult { Success = false, ErrorMessage = "test" });
            }
            public Task<EInvoiceResult> CancelInvoiceAsync(string providerInvoiceId, string reason) =>
                Task.FromResult(new EInvoiceResult { Success = true });
        }

        // EInvoice:KdvRate varsayilani 0.20 - oran tanimsiz kalirsa buraya duser.
        private static IConfiguration Config() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["EInvoice:KdvRate"] = "0.20" })
                .Build();

        private static InvoiceManager NewManager(DivisimaDbContext ctx, IEInvoiceProvider provider) =>
            new InvoiceManager(
                new EfInvoiceDal(ctx), new EfOrderDal(ctx), new EfOrderItemDal(ctx),
                new EfProductDal(ctx), provider, Config(),
                new EfInvoiceItemDal(ctx), new EfCategoryDal(ctx));

        private static async Task<int> NewCategoryAsync(decimal? vatRate)
        {
            await using var ctx = NewContext();
            var c = new Category
            {
                name = "KDV Kategori",
                slug = $"kdv-{Guid.NewGuid():N}",
                vat_rate = vatRate,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(c);
            await ctx.SaveChangesAsync();
            return c.id;
        }

        private static async Task<int> NewProductAsync(int categoryId, decimal price, decimal? productVatRate = null)
        {
            await using var ctx = NewContext();
            var p = new Product
            {
                name = "KDV Urun",
                brand = "T",
                category_id = categoryId,
                price = price,
                description = "kdv testi urunu",
                color_hex = "#111111",
                product_type = 0,
                vat_rate = productVatRate,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();
            return p.id;
        }

        // Siparis + kalemler. total KDV DAHIL kabul edilir (fiyat politikasi).
        private static Task<int> NewOrderAsync(params (int productId, int qty, decimal unitPrice)[] items)
            => NewOrderAsync(0m, items);

        // MANTIK-FIX-2R / K1: kargolu siparis kurgusu. Onceden bu sinifin HICBIR kurgusu
        // shipping_cost ATAMIYORDU (olculdu: 0 gecis) - bu yuzden "kargo son kaleme gomuluyor"
        // kusuru bu pin setiyle YAPISAL OLARAK yakalanamiyordu.
        private static Task<int> NewOrderAsync(decimal shipping, params (int productId, int qty, decimal unitPrice)[] items)
            => NewOrderAsync(shipping, 0m, items);

        // MANTIK-FIX-2R / K4: KREDI-ODEMELI kurgu. Depoda bu sinifta kredi tasiyan tek bir
        // fikstur YOKTU; kredi olmadan "fatura BRUTTEN kesilir" sozlesmesi OLCULEMEZ (pin VAKUM).
        private static async Task<int> NewOrderAsync(decimal shipping, decimal magazaKredisi, params (int productId, int qty, decimal unitPrice)[] items)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "KDV Testi",
                email = $"kdv-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            await ctx.SaveChangesAsync();

            var toplam = items.Sum(i => i.unitPrice * i.qty);
            var o = new Order
            {
                customer_id = c.id,
                order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                status = (byte)OrderStatusEnum.Confirmed,
                subtotal = toplam,
                shipping_cost = shipping,
                total_price = toplam + shipping,
                store_credit_used = magazaKredisi,
                discount_amount = 0m,
                is_online_payment_done = true,
                currency = "TRY",
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();

            foreach (var it in items)
            {
                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = o.id,
                    product_id = it.productId,
                    size = "M",
                    quantity = it.qty,
                    unit_price = it.unitPrice,
                    is_cancelled = false,
                    created_at = DateTime.Now
                });
            }
            await ctx.SaveChangesAsync();
            return o.id;
        }

        private static async Task<(Invoice invoice, List<InvoiceItem> items)> ReadInvoiceAsync(int orderId)
        {
            await using var ctx = NewContext();
            var inv = await ctx.Set<Invoice>().AsNoTracking().SingleAsync(i => i.order_id == orderId);
            var items = await ctx.Set<InvoiceItem>().AsNoTracking()
                .Where(x => x.invoice_id == inv.id).OrderBy(x => x.id).ToListAsync();
            return (inv, items);
        }

        // (a) KARISIK SEPET: giyim %10 + aksesuar %20.
        // 1100 TL giyim (KDV dahil) -> matrah 1000, KDV 100
        //  240 TL aksesuar (KDV dahil) -> matrah 200, KDV  40
        // Toplam: matrah 1200, KDV 140, genel 1340. Tek oranla (%20) hesaplansaydi
        // matrah 1116.67 / KDV 223.33 cikardi - yani YANLIS beyan.
        [Fact]
        public async Task KarisikSepet_KalemKDVleri_AYRI_Hesaplanir_ToplamTutarli()
        {
            if (Skipped()) return;
            var giyimKat = await NewCategoryAsync(0.10m);
            var aksesuarKat = await NewCategoryAsync(0.20m);
            var giyim = await NewProductAsync(giyimKat, 1100m);
            var aksesuar = await NewProductAsync(aksesuarKat, 240m);
            var orderId = await NewOrderAsync((giyim, 1, 1100m), (aksesuar, 1, 240m));

            var provider = new CapturingEInvoiceProvider();
            await using (var ctx = NewContext())
            {
                var r = await NewManager(ctx, provider).GenerateForOrder(orderId);
                r.Item1.Should().Be(HttpStatusCode.OK, $"fatura uretilmeli: {r.Item2.Message}");
            }

            var (invoice, items) = await ReadInvoiceAsync(orderId);

            // MANTIK-FIX-2R / K1 PREMIS GUNCELLEMESI: eskiden "items.HaveCount(2)" idi.
            // Artik HER faturada TAM 1 kargo kalemi var (D1 karari), yani toplam 3 satir.
            // OLCULEN SEY DEGISMEDI - urun kalemlerinin KDV'si; yalnizca kapsam URUN
            // kalemlerine daraltildi ve kargo sozlesmesi AYRICA assert edildi.
            var urunSatirlari = items.Where(i => i.product_id != null).ToList();
            urunSatirlari.Should().HaveCount(2, "iki urun icin iki urun kalemi olmali");
            items.Where(i => i.product_id == null).Should().HaveCount(1, "kargo kalemi HER faturada TAM 1");

            var giyimSatir = items.Single(i => i.product_id == giyim);
            giyimSatir.vat_rate.Should().Be(0.1000m, "giyim kategorisi orani kaleme kopyalanmali");
            giyimSatir.line_subtotal.Should().Be(1000.00m, "1100 / 1.10 = 1000");
            giyimSatir.vat_amount.Should().Be(100.00m);
            giyimSatir.line_total.Should().Be(1100.00m);

            var aksesuarSatir = items.Single(i => i.product_id == aksesuar);
            aksesuarSatir.vat_rate.Should().Be(0.2000m, "aksesuar kategorisi orani kaleme kopyalanmali");
            aksesuarSatir.line_subtotal.Should().Be(200.00m, "240 / 1.20 = 200");
            aksesuarSatir.vat_amount.Should().Be(40.00m);

            // KURUS KACAGI YOK: baslik degerleri kalem toplamlarina BIREBIR esit.
            invoice.subtotal.Should().Be(items.Sum(i => i.line_subtotal), "matrah kalem toplami olmali");
            invoice.tax_amount.Should().Be(items.Sum(i => i.vat_amount), "KDV kalem toplami olmali");
            invoice.total.Should().Be(items.Sum(i => i.line_total), "genel toplam kalem toplami olmali");
            invoice.total.Should().Be(1340.00m, "1100 + 240");

            // Baslik orani artik AGIRLIKLI ORTALAMA: 140 / 1200 = 0.1167
            invoice.tax_rate.Should().Be(0.1167m, "kalemlerin agirlikli ortalamasi");
            invoice.tax_rate.Should().NotBe(0.2000m, "tek oran varsayimi ARTIK GECERLI DEGIL");
        }

        // P-F1 (MANTIK-FIX-2R / K1): KARGO AYRI FATURA KALEMI.
        //
        // OLCULEN ONCE-DURUM: order.total_price kargoyu ICERIR ve kurus-kacagi kurali son URUN
        // kalemine "total - toplananBrut" yaziyordu -> kargo bedeli sessizce bir URUN kalemine
        // gomuluyordu. Canli olcum: 89 kalemli faturanin 89'unda
        // SUM(line_total) - (subtotal - indirim) = shipping_cost, ISTISNASIZ.
        //
        // VAKUM KIRICI: kargo GERCEKTEN 49,90 olmali (0 kargolu bir kurgu bu pini bedava gecerdi).
        // CIFT-ANLAM KIRICI: yalniz "NULL kalem var" YETMEZ - urun kaleminin kargoyu EMMEDIGI de
        // assert edilir; kargo son kaleme geri gomulurse urun kalemi 1149,90 olur ve pin kirilir.
        [Fact]
        public async Task KargoluFatura_KARGO_AYRI_KALEM_UrunKalemi_KargoyuEMMEZ()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.20m);
            var urunId = await NewProductAsync(katId, 1100m);
            var orderId = await NewOrderAsync(49.90m, (urunId, 1, 1100m));   // brut 1149,90

            await using (var ctx = NewContext())
            {
                var r = await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(orderId);
                r.Item1.Should().Be(HttpStatusCode.OK, $"fatura uretilmeli: {r.Item2.Message}");
            }

            var (invoice, items) = await ReadInvoiceAsync(orderId);

            // (1) KARGO KALEMI: TAM 1 tane, product_id NULL, line_total = shipping_cost.
            var kargoSatirlari = items.Where(i => i.product_id == null).ToList();
            kargoSatirlari.Should().HaveCount(1, "kargo kalemi HER faturada TAM 1 olmali");
            kargoSatirlari[0].line_total.Should().Be(49.90m, "kargo kaleminin tutari shipping_cost olmali");
            kargoSatirlari[0].quantity.Should().Be(1);

            // (2) CIFT-ANLAM KIRICI: urun kalemi kargoyu EMMEMELI.
            var urunSatirlari = items.Where(i => i.product_id != null).ToList();
            urunSatirlari.Should().HaveCount(1);
            urunSatirlari[0].line_total.Should().Be(1100.00m,
                "urun kalemi KENDI brutu olmali - kargo buraya AKMAMALI (once-durum: 1149,90)");

            // (3) ZINCIR KURUSU KURUSUNA: kalemler + kargo = brut.
            items.Sum(i => i.line_total).Should().Be(1149.90m, "kalemler + kargo = order.total_price");
            invoice.total.Should().Be(1149.90m);
            invoice.subtotal.Should().Be(items.Sum(i => i.line_subtotal), "matrah kalem toplami");
            invoice.tax_amount.Should().Be(items.Sum(i => i.vat_amount), "KDV kalem toplami");
            (invoice.subtotal + invoice.tax_amount).Should().Be(invoice.total, "matrah + KDV = brut");
        }

        // P-F4 (MANTIK-FIX-2R / K4): FATURA URETIM ANINDA BRUTTEN KESILIR.
        //
        // SOZLESME: magaza kredisi bir ODEME ARACIDIR, fiyat indirimi DEGILDIR - matrahi
        // DUSURMEZ. Fatura mali bir beyandir ve BRUT tutar uzerinden duzenlenir; kredi
        // yalnizca "bu belge nasil odendi" sorusunun yanitidir (D2: invoices krediyi
        // KAYDETMEZ; ekranin odeme ozeti SIPARIS verisinden gelir - P-F2c).
        //
        // KANIT EMSALI (MF-1'de canli olculdu): kredi tasiyan dort tarihsel siparisin
        // gercek faturalari (invoices 81-84) matrah 863,45 + KDV 86,35 = 949,80 tasiyor -
        // yani belge kredi DUSULMEDEN uretilmis.
        //
        // KAPSAM (C6): bu pin URETIM ANI sozlesmesidir, GLOBAL bir DB esitligi DEGIL.
        // "invoice.total == order.total_price" her satir icin DOGRU DEGILDIR: iptal sonrasi
        // order.total_price MUTASYONA ugruyor (canli olcum: 7 satirda i.total 949,80 iken
        // o.total_price 0,00). O yuzden burada URETIM ANINDAKI zincir olculur.
        [Fact]
        public async Task KrediliSiparis_FATURA_BRUTTEN_Kesilir_Kredi_MATRAHI_DUSURMEZ()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.20m);
            var urunId = await NewProductAsync(katId, 1000m);
            // 1000,00 urun + 49,90 kargo = 1.049,90 BRUT; bunun 200,00'i magaza kredisiyle odenir.
            var orderId = await NewOrderAsync(49.90m, 200.00m, (urunId, 1, 1000m));

            await using (var ctx = NewContext())
            {
                var r = await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(orderId);
                r.Item1.Should().Be(HttpStatusCode.OK, $"fatura uretilmeli: {r.Item2.Message}");
            }

            var (invoice, items) = await ReadInvoiceAsync(orderId);

            // (1) BELGE BRUTTUR - kredi DUSULMEMIS.
            invoice.total.Should().Be(1049.90m,
                "fatura BRUT tutardan kesilir; kredi bir ODEME ARACIDIR, belgeyi kucultmez " +
                "(kredi dusulseydi 849,90 cikardi)");
            (invoice.subtotal + invoice.tax_amount).Should().Be(1049.90m, "matrah + KDV = brut");

            // (2) KREDI BELGEYE HIC GIRMEZ (D2: invoices krediyi kaydetmiyor).
            items.Sum(i => i.line_total).Should().Be(1049.90m, "kalemler toplami da BRUT olmali");

            // (3) CIFT-ANLAM KIRICI + K1 KALEM YAPISI: kargo AYRI kalem olarak duruyor.
            items.Where(i => i.product_id == null).Should().HaveCount(1, "NULL kargo kalemi");
            items.Single(i => i.product_id == null).line_total.Should().Be(49.90m);
            items.Single(i => i.product_id != null).line_total.Should().Be(1000.00m,
                "urun kalemi kargoyu da krediyi de EMMEMELI");

            // (4) VAKUM KIRICI: kurgu GERCEKTEN kredi tasiyor olmali - aksi halde bu pin
            // kredisiz bir sipariste de bedava gecerdi.
            await using var ctx2 = NewContext();
            (await ctx2.Set<Order>().SingleAsync(o => o.id == orderId)).store_credit_used
                .Should().Be(200.00m, "fikstur kredi tasimali");
        }

        // P-F1b (D1 karari): BEDAVA kargoda kalem YAZILIR - 0,00 ile. Tek bicim, dalsiz ekran.
        [Fact]
        public async Task BedavaKargo_KARGO_KALEMI_YINE_YAZILIR_SifirTutarla()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.20m);
            var urunId = await NewProductAsync(katId, 1200m);
            var orderId = await NewOrderAsync(0m, (urunId, 1, 1200m));

            await using (var ctx = NewContext())
                await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(orderId);

            var (invoice, items) = await ReadInvoiceAsync(orderId);
            var kargo = items.Where(i => i.product_id == null).ToList();
            kargo.Should().HaveCount(1, "bedava kargoda da kalem YAZILIR - kalem YOKLUGU degil 0,00");
            kargo[0].line_total.Should().Be(0.00m);
            kargo[0].vat_amount.Should().Be(0.00m);
            // VAKUM KIRICI: urun tarafi bozulmadi.
            items.Single(i => i.product_id != null).line_total.Should().Be(1200.00m);
            invoice.total.Should().Be(1200.00m);
        }

        // (b) SNAPSHOT: fatura kesildikten SONRA kategori orani degisirse ESKI fatura degismez.
        [Fact]
        public async Task OranSonradanDegisirse_ESKI_Fatura_DEGISMEZ_YeniFatura_YeniOranla()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.10m);
            var urunId = await NewProductAsync(katId, 1100m);
            var ilkOrderId = await NewOrderAsync((urunId, 1, 1100m));

            await using (var ctx = NewContext())
                await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(ilkOrderId);

            var (ilkFatura, ilkKalemler) = await ReadInvoiceAsync(ilkOrderId);
            ilkKalemler.Single(i => i.product_id != null).vat_rate.Should().Be(0.1000m);

            // Kategori orani %10 -> %20 degistirilir.
            await using (var ctx = NewContext())
            {
                var kat = await ctx.Set<Category>().SingleAsync(c => c.id == katId);
                kat.vat_rate = 0.20m;
                await ctx.SaveChangesAsync();
            }

            // ESKI fatura AYNEN kalmali - yasal belge geriye donuk degismez.
            var (ilkFaturaSonra, ilkKalemlerSonra) = await ReadInvoiceAsync(ilkOrderId);
            ilkKalemlerSonra.Single(i => i.product_id != null).vat_rate.Should().Be(0.1000m, "kesilmis faturanin orani DONDURULMUS olmali");
            ilkFaturaSonra.tax_amount.Should().Be(ilkFatura.tax_amount, "eski faturanin KDV'si degismemeli");

            // YENI siparisin faturasi YENI oranla kesilir (vakum kirici: oran degisikligi
            // gercekten etkili, yalnizca eskiyi korumakla kalmiyoruz).
            var yeniOrderId = await NewOrderAsync((urunId, 1, 1200m));
            await using (var ctx = NewContext())
                await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(yeniOrderId);

            var (_, yeniKalemler) = await ReadInvoiceAsync(yeniOrderId);
            yeniKalemler.Single(i => i.product_id != null).vat_rate.Should().Be(0.2000m, "yeni fatura GUNCEL oranla kesilmeli");
            yeniKalemler.Single(i => i.product_id != null).line_subtotal.Should().Be(1000.00m, "1200 / 1.20 = 1000");
        }

        // (c) REGRESYON: tek oranli sepette eski davranisla ayni sonuc.
        [Fact]
        public async Task TekOranliSepet_EskiDavranisla_UYUMLU()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.20m);
            var urunId = await NewProductAsync(katId, 1200m);
            var orderId = await NewOrderAsync((urunId, 2, 1200m));   // toplam 2400

            await using (var ctx = NewContext())
                await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(orderId);

            var (invoice, items) = await ReadInvoiceAsync(orderId);
            invoice.total.Should().Be(2400.00m);
            invoice.subtotal.Should().Be(2000.00m, "2400 / 1.20 = 2000 - eski formulle ayni");
            invoice.tax_amount.Should().Be(400.00m);
            invoice.tax_rate.Should().Be(0.2000m, "tek oranli sepette agirlikli ortalama = o oran");
            items.Single(i => i.product_id != null).vat_rate.Should().Be(0.2000m);
        }

        // Urun orani kategoriyi EZER; ikisi de yoksa config varsayilanina (0.20) duser.
        [Fact]
        public async Task EfektifOranZinciri_Urun_Kategori_Varsayilan()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.10m);
            var oranssizKat = await NewCategoryAsync(null);
            var ezenUrun = await NewProductAsync(katId, 200m, productVatRate: 0.20m); // urun ezer
            var kategoridenUrun = await NewProductAsync(katId, 110m);                  // kategoriden
            var varsayilanUrun = await NewProductAsync(oranssizKat, 120m);             // config'ten

            var orderId = await NewOrderAsync((ezenUrun, 1, 200m), (kategoridenUrun, 1, 110m), (varsayilanUrun, 1, 120m));
            await using (var ctx = NewContext())
                await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(orderId);

            var (_, items) = await ReadInvoiceAsync(orderId);
            items.Single(i => i.product_id == ezenUrun).vat_rate.Should().Be(0.2000m, "urun orani kategoriyi EZMELI");
            items.Single(i => i.product_id == kategoridenUrun).vat_rate.Should().Be(0.1000m, "urun orani yoksa kategoriden");
            items.Single(i => i.product_id == varsayilanUrun).vat_rate.Should().Be(0.2000m, "ikisi de yoksa EInvoice:KdvRate");
        }

        // (d) Saglayiciya giden satirlarda VatRate/VatAmount DOLU olmali.
        [Fact]
        public async Task SaglayiciSatirlari_VatRate_VeVatAmount_Dolu()
        {
            if (Skipped()) return;
            var giyimKat = await NewCategoryAsync(0.10m);
            var aksesuarKat = await NewCategoryAsync(0.20m);
            var giyim = await NewProductAsync(giyimKat, 1100m);
            var aksesuar = await NewProductAsync(aksesuarKat, 240m);
            var orderId = await NewOrderAsync((giyim, 1, 1100m), (aksesuar, 1, 240m));

            var provider = new CapturingEInvoiceProvider();
            await using (var ctx = NewContext())
                await NewManager(ctx, provider).GenerateForOrder(orderId);

            provider.LastRequest.Should().NotBeNull("saglayiciya istek gitmis olmali");
            var lines = provider.LastRequest!.Lines;

            // MANTIK-FIX-2R / K1 PREMIS GUNCELLEMESI: eskiden HaveCount(2) idi ve
            // "her satirda VatAmount > 0" deniyordu. Artik saglayici payloadinda kargo satiri
            // da var (bu kurguda 0,00 - siparis kargosuz). OLCULEN SEY DEGISMEDI: urun
            // satirlarinin oran/tutar dolulugu. AYIRT EDICI: EInvoiceLine URUN KIMLIGI TASIMAZ,
            // dolayisiyla payloadta kargoyu ayirt eden TEK sey addir (E'nin bulgusu - saglayici
            // sozlesmesi acilirsa burasi yeniden dusunulmeli).
            lines.Should().HaveCount(3, "iki urun + kargo");
            var urunSatirlari = lines.Where(l => l.ProductName != "Kargo").ToList();
            urunSatirlari.Should().HaveCount(2);
            urunSatirlari.Should().OnlyContain(l => l.VatRate > 0m, "her urun satirinda oran bulunmali");
            urunSatirlari.Should().OnlyContain(l => l.VatAmount > 0m, "her urun satirinda KDV tutari bulunmali");
            urunSatirlari.Single(l => l.VatRate == 0.10m).VatAmount.Should().Be(100.00m);
            urunSatirlari.Single(l => l.VatRate == 0.20m).VatAmount.Should().Be(40.00m);
        }

        // (e) NULL TELEFONLU musteri: siparis + fatura akisi kirilmamali.
        // Sprint 4'te customers.phone / addresses.phone nullable yapildi ve KVKK silmesi
        // artik NULL yaziyor. Silinmis bir musterinin siparis gecmisi faturalanabilir olmali.
        [Fact]
        public async Task NullTelefonluMusteri_SiparisVeFatura_Akisi_KIRILMAZ()
        {
            if (Skipped()) return;
            var katId = await NewCategoryAsync(0.10m);
            var urunId = await NewProductAsync(katId, 1100m);

            int orderId;
            await using (var ctx = NewContext())
            {
                var c = new Customer
                {
                    name = "Silinmis Kullanici",
                    email = $"deleted_{Guid.NewGuid():N}@divisima.invalid",
                    phone = null,                       // <- KVKK anonimlestirmesi
                    password_hash = Array.Empty<byte>(),
                    password_salt = Array.Empty<byte>(),
                    is_active = true,
                    email_verified = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Customer>().Add(c);
                await ctx.SaveChangesAsync();

                var o = new Order
                {
                    customer_id = c.id,
                    order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                    status = (byte)OrderStatusEnum.Confirmed,
                    subtotal = 1100m,
                    total_price = 1100m,
                    discount_amount = 0m,
                    store_credit_used = 0m,
                    is_online_payment_done = true,
                    currency = "TRY",
                    created_at = DateTime.Now
                };
                ctx.Set<Order>().Add(o);
                await ctx.SaveChangesAsync();
                orderId = o.id;

                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = o.id,
                    product_id = urunId,
                    size = "M",
                    quantity = 1,
                    unit_price = 1100m,
                    is_cancelled = false,
                    created_at = DateTime.Now
                });

                // Adres de NULL telefonlu olabilir (silme kaskadi).
                ctx.Set<Address>().Add(new Address
                {
                    customer_id = c.id,
                    title = "-",
                    full_name = "Silinmiş",
                    phone = null,
                    city = "-",
                    district = "-",
                    full_address = "-",
                    is_default = false,
                    is_active = false,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            await using (var ctx = NewContext())
            {
                var r = await NewManager(ctx, new CapturingEInvoiceProvider()).GenerateForOrder(orderId);
                r.Item1.Should().Be(HttpStatusCode.OK, $"NULL telefonlu musteri faturalanabilmeli: {r.Item2.Message}");
            }

            var (invoice, items) = await ReadInvoiceAsync(orderId);
            invoice.total.Should().Be(1100.00m);
            items.Single(i => i.product_id != null).vat_rate.Should().Be(0.1000m);
        }
    }
}
