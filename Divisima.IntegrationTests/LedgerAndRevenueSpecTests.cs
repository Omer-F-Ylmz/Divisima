using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Dashboard;
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
    // ══ DALGA-2-FIX: B11 (stok defteri mutabakati) + B14 (ciro kurali merkezden) ══════════════
    [Trait("Category", "Sql")]
    public class LedgerAndRevenueSpecTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaLedgerRevenueTest";
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

        private sealed class LedgerFactory : WebApplicationFactory<Program>
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

        private LedgerFactory? _factory;
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
                _factory = new LedgerFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak defter/ciro testleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        private static async Task<int> UrunKurAsync(int baslangicStogu)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            await using var ctx = NewContext();
            var kategori = new Category
            {
                name = "Defter Kategori " + damga,
                slug = "defter-" + damga,
                display_order = 1,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Defter Urunu " + damga,
                brand = "Divisima",
                category_id = kategori.id,
                price = 100m,
                description = "Defter pini icin urun.",
                color_hex = "#334455",
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
                stock_quantity = baslangicStogu,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();
            return urun.id;
        }

        // Rezervasyonun baglanacagi GERCEK siparis satiri (FK icin).
        private static async Task<int> SiparisSatiriKurAsync()
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            await using var ctx = NewContext();
            var musteri = new Customer
            {
                name = "Defter Musteri " + damga,
                email = $"defter-{damga}@example.com",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                user_type = (byte)UserTypeEnum.Customer,
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(musteri);
            await ctx.SaveChangesAsync();

            var siparis = new Order
            {
                customer_id = musteri.id,
                order_number = "DVS-DEFTER-" + damga,
                status = (byte)OrderStatusEnum.Pending,
                subtotal = 200m,
                discount_amount = 0m,
                shipping_cost = 0m,
                total_price = 200m,
                currency = "TRY",
                payment_type = 0,
                is_online_payment_done = false,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(siparis);
            await ctx.SaveChangesAsync();
            return siparis.id;
        }

        // DEFTER MUTABAKATI - uretimdeki tek dogru formul.
        // In(1) ve Out(2) satirlarinin yonu `movement_type`tan gelir; Adjustment(3) satirlarinin
        // yonu quantity'nin ISARETINDEDIR (B11).
        private static async Task<int> DefterNetiAsync(int productId, string size)
        {
            await using var ctx = NewContext();
            var hareketler = await ctx.Set<StockMovement>().AsNoTracking()
                .Where(m => m.product_id == productId && m.size == size).ToListAsync();
            return hareketler.Sum(m => m.movement_type == (byte)StockMovementType.Out ? -m.quantity : m.quantity);
        }

        private static async Task<int> FizikselStokAsync(int productId, string size)
        {
            await using var ctx = NewContext();
            return (await ctx.Set<ProductStock>().AsNoTracking()
                .SingleAsync(s => s.product_id == productId && s.size == size)).stock_quantity;
        }

        // ── B11-1) AZALIS YONUNDEKI DUZELTME NEGATIF YAZILIR ─────────────────────────────────
        //
        // OLCULEN ZARAR: yon yalnizca serbest metin `note` icindeydi ("Admin duzeltme (-5)");
        // sayisal defter artis ile azalisi AYIRT EDEMIYORDU.
        [Fact]
        public async Task AzalisYonundekiDuzeltme_NEGATIF_Miktarla_Yazilir()
        {
            if (Skipped()) return;
            var urunId = await UrunKurAsync(baslangicStogu: 10);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                .AdjustStock(urunId, "M", 5, "sayim duzeltmesi - hasarli 5 adet"));
            r.Item2.Success.Should().BeTrue($"duzeltme basarili olmali: {r.Item2.Message}");

            await using var ctx = NewContext();
            var hareket = await ctx.Set<StockMovement>().AsNoTracking()
                .SingleAsync(m => m.product_id == urunId && m.movement_type == (byte)StockMovementType.Adjustment);

            hareket.quantity.Should().Be(-5,
                "10 -> 5 bir AZALISTIR; defterde ISARETLI fark durmali. Mutlak deger yazilsaydi " +
                "(-5 yerine 5) defter, ayni buyuklukteki bir ARTISTAN ayirt edilemezdi ve " +
                "mutabakat sessizce YANLIS sonuc verirdi");
            hareket.note.Should().Contain("-5", "operatorun gordugu yon denetim izinde de okunabilir kalmali");
        }

        // ── B11-2) DEFTER MUTABAKATI = TABLO ─────────────────────────────────────────────────
        //
        // Dalga 2'de olculen canli senaryonun (urun 2 / M) BIREBIR yeniden kurulumu:
        //     baslangic 10, admin +15, admin -5, siparis onayiyla -2  ->  gercek 18? HAYIR: 18
        // Beklenen: defter neti 8 ve product_stocks de 8. Duzeltme oncesi defter 18 veriyordu
        // (isaretsiz -5, +5 gibi toplaniyordu) - tam 10 birimlik HAYALI fark.
        [Fact]
        public async Task DefterMutabakati_TABLOYLA_BIREBIR_TUTAR_HayaliFarkYOK()
        {
            if (Skipped()) return;
            var urunId = await UrunKurAsync(baslangicStogu: 10);

            // Baslangic stogu bir HAREKET DEGILDIR (urun kurulumunda yazilir); mutabakat bu yuzden
            // "baslangic + defter neti" seklinde yapilir.
            const int baslangic = 10;

            var artis = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                .AdjustStock(urunId, "M", 25, "yeni sevkiyat"));            // +15
            artis.Item2.Success.Should().BeTrue();

            var azalis = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                .AdjustStock(urunId, "M", 20, "sayim duzeltmesi"));         // -5
            azalis.Item2.Success.Should().BeTrue();

            // Gercek satis yolu: rezervasyon -> onay (Out hareketi yazar).
            // GERCEK bir siparis satiri kurulur: `stock_reservations.order_id` uzerinde FK var.
            // NOT (D-SEMA-FIX): bu yorum eskiden "yayin semasinda (01_schema.sql) FK var" diyordu -
            // yani kisit YALNIZ o dosyada tanimliydi ve EF tarafinda YOKTU. O ayrisma kapandi:
            // artik tek dogruluk kaynagi EF migrations ve FK HER ORTAMDA yururlukte. Uydurma bir
            // id ile calismak o zaman "ortama gore davranan" bir testti; simdi HER YERDE duser.
            var siparisId = await SiparisSatiriKurAsync();
            var rez = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                .ReserveStock(urunId, "M", 2, siparisId));
            rez.Item1.Should().Be(System.Net.HttpStatusCode.OK, $"rezervasyon basarili olmali: {rez.Item2.Message}");
            var onay = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                .ConfirmReservation(siparisId));
            onay.Item2.Success.Should().BeTrue($"onay basarili olmali: {onay.Item2.Message}");

            var fiziksel = await FizikselStokAsync(urunId, "M");
            var defterNeti = await DefterNetiAsync(urunId, "M");

            fiziksel.Should().Be(18, "10 + 15 - 5 - 2 = 18");
            (baslangic + defterNeti).Should().Be(fiziksel,
                "DEFTER MUTABAKATI: baslangic + SUM(In + Adjustment - Out) tabloyla BIREBIR tutmali. " +
                "Duzeltme oncesi azalis kaydi mutlak deger yazildigi icin defter 10 birim FAZLA " +
                "gosteriyordu - denetim araci sessizce yanlis sayi uretiyordu");
        }

        // ── B11-3) ARTIS YONU BOZULMADI (vakum/cift-anlam kirici) ────────────────────────────
        //
        // "Her duzeltme negatif yazilsin" gibi yanlis bir uygulama, yukaridaki iki testi de
        // gecemez ama bu test onu ayrica ve dogrudan reddeder.
        [Fact]
        public async Task ArtisYonundekiDuzeltme_POZITIF_Kalir()
        {
            if (Skipped()) return;
            var urunId = await UrunKurAsync(baslangicStogu: 10);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IStockService>()
                .AdjustStock(urunId, "M", 25, "yeni sevkiyat"));
            r.Item2.Success.Should().BeTrue();

            await using var ctx = NewContext();
            (await ctx.Set<StockMovement>().AsNoTracking()
                .SingleAsync(m => m.product_id == urunId && m.movement_type == (byte)StockMovementType.Adjustment))
                .quantity.Should().Be(15, "10 -> 25 bir ARTISTIR, isaret POZITIF kalmali");
        }

        // ── B14) CIRO KURALI PaidOrderSpec'I IZLER ───────────────────────────────────────────
        //
        // ONCEKI HALI: DashboardManager kurali KOPYALIYORDU ve DISLAMA ile yaziyordu
        // (`!= Cancelled && != Pending`). Bugun ayni kumeyi veriyordu; ama enum'a eklenecek HER
        // yeni durum ciroya OTOMATIK girerdi - `PaidOrderSpec` (EKLEME ile yazili) ise dislardi.
        //
        // Bu pin beklenen ciroyu SPEC'TEN hesaplar; boylece spec degistiginde beklenti de degisir
        // ve ciro sorgusunun spec'i GERCEKTEN izledigi olculur. Elle yazilmis bir kural yeniden
        // konulursa (spec ile ayrisan herhangi bir liste) test KIRILIR.
        [Fact]
        public async Task Ciro_TANIMLI_HER_DURUM_icin_PaidOrderSpec_i_IZLER()
        {
            if (Skipped()) return;

            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            decimal beklenenCiro = 0m;
            decimal tumSiparislerinToplami = 0m;

            await using (var ctx = NewContext())
            {
                var musteri = new Customer
                {
                    name = "Ciro " + damga,
                    email = $"ciro-{damga}@example.com",
                    phone = "5550000000",
                    password_hash = new byte[] { 1 },
                    password_salt = new byte[] { 2 },
                    user_type = (byte)UserTypeEnum.Customer,
                    is_active = true,
                    email_verified = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Customer>().Add(musteri);
                await ctx.SaveChangesAsync();

                // TANIMLI HER durum icin ayirt edilebilir tutarli birer siparis.
                decimal tutar = 100m;
                foreach (OrderStatusEnum durum in Enum.GetValues(typeof(OrderStatusEnum)))
                {
                    tutar += 100m;
                    ctx.Set<Order>().Add(new Order
                    {
                        customer_id = musteri.id,
                        order_number = $"DVS-CIRO-{damga}-{(byte)durum}",
                        status = (byte)durum,
                        subtotal = tutar,
                        discount_amount = 0m,
                        shipping_cost = 0m,
                        total_price = tutar,
                        currency = "TRY",
                        payment_type = 0,
                        is_online_payment_done = false,
                        created_at = DateTime.Now
                    });

                    // BEKLENTI SPEC'TEN TURETILIR - elle bir durum listesi YAZILMAZ.
                    tumSiparislerinToplami += tutar;
                    if (PaidOrderSpec.IsPaidStatus((byte)durum)) beklenenCiro += tutar;
                }
                await ctx.SaveChangesAsync();
            }

            var ozet = await WithScopeAsync(sp => sp.GetRequiredService<IDashboardService>().GetSummary());
            ozet.Item1.Should().Be(System.Net.HttpStatusCode.OK);
            var data = (ozet.Item2 as Divisima.Core.Utilities.Results.IDataResult<DashboardSummaryDto>)!.Data;

            // VAKUM KIRICI: beklenen ciro 0 OLMAMALI - aksi halde "hicbir siparis sayilmiyor"
            // durumunda da test yesil kalirdi.
            beklenenCiro.Should().BeGreaterThan(0m, "spec en az bir durumu odenmis saymali");
            data.total_revenue.Should().Be(beklenenCiro,
                "ciro, PaidOrderSpec'in odenmis saydigi durumlarin TAM toplami olmali - " +
                "kural kopyalanirsa (or. DISLAMA ile yazilirsa) spec'e eklenen/cikarilan bir durum " +
                "sessizce ayrisir ve ciro yanlis hesaplanir");

            // CIFT-ANLAM KIRICI: iptal ve odenmemis siparisler GERCEKTEN disarida mi?
            data.total_revenue.Should().BeLessThan(tumSiparislerinToplami,
                "ciro TUM siparislerin toplamindan KUCUK olmali (Pending ve Cancelled disarida) - " +
                "yoksa 'her siparis ciroya giriyor' uygulamasi da bu testi gecerdi");
        }
    }
}
