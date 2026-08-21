using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
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
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // Faturasi cekilecek GERCEK bir siparis kurar. Kategori GERCEKTEN olusturulur, urunun
        // description/color_hex alanlari doldurulur (zorunlu alanlar). Her cagri kendi verisini
        // uretir - var olan satirlara guvenilmez.
        private static async Task<(int OrderId, string OrderNumber, string ProductName)> SeedOrderAsync(int customerId)
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

            var resp = await user.Client.GetAsync($"/api/order/{seed.OrderId}/invoice-html");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            // OLCULEN BELIRTININ KENDISI: zarar sirasinda uc "200 + Content-Length: 0" donuyordu
            // (curl ile sunucu tarafinda olculdu). Basligi DOGRUDAN pinliyoruz.
            resp.Content.Headers.ContentLength.Should().BeGreaterThan(0,
                "Content-Length: 0 tam olarak E3'te olculen zarardir");

            var govde = await resp.Content.ReadAsStringAsync();

            govde.Should().NotBeNullOrWhiteSpace(
                "tek argumanli SuccessDataResult<string> kullanilirsa Data null kalir ve uc " +
                "200 + Content-Length: 0 doner - fatura ekrani BOS gelir (E3'te canli olculdu)");
            govde.Should().Contain(seed.OrderNumber, "govde GERCEKTEN bu siparisin faturasi olmali");
            govde.Should().Contain(seed.ProductName, "kalem satirlari cizilmis olmali");
            // KULTUR BAGIMLI LITERAL YASAK (CI'da BIR KEZ KIRDI - olculdu).
            // Ilk hali "549,90" yaziyordu. Yerel makine tr-TR oldugu icin YESIL, GitHub kosucusu
            // invariant kultur oldugu icin KIRMIZI.
            // SPRINT 8 MADDE 13'ten SONRA beklenti SERTLESTI: uygulama artik kulturu tr-TR'ye
            // PINLIYOR (Program.cs), dolayisiyla fatura govdesi KOSUCU KULTURUNDEN BAGIMSIZ
            // olarak tr bicimiyle cikmali. Assert bu yuzden CurrentCulture'a degil ACIKCA
            // tr-TR'ye bakiyor - CI'da (invariant kosucu) da ayni sonucu bekler.
            var beklenenToplam = 549.90m.ToString("N2", new CultureInfo("tr-TR"));
            govde.Should().Contain(beklenenToplam,
                $"genel toplam tr bicimiyle govdede yer almali (beklenen: {beklenenToplam}) - " +
                "uygulama kulturu pinlemezse kosucu yerelinde '549.90' cikar ve bu assert kirilir");
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
