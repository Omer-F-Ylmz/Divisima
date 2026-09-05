using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Coupon;
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
    // Aciklayici yorum: D3 KALBI - kuponun SON HAKKINA eszamanli yaris. OrderManager check-then-act
    // yapiyor ve bunu coupon:{code} dagitik kilidiyle koruyor. Kilit implementasyonu Redis:Enabled
    // bayragina bagli; TEST ortaminda Redis kapali oldugu icin InMemoryDistributedLock devrede
    // (Program.cs else dali) - tek process icinde gercek serilestirme saglar.
    // PREMIS DEGISTI - MFIX-B / K2 (merkez onayina sunuldu):
    // ESKI BEKLENTI: "limit asilinca siparis yine BASARILI olur ama kupon UYGULANMAZ; 8 siparisin
    // hepsi gecer, coupon_code YALNIZ birinde dolu olur."
    // YENI BEKLENTI: gecersiz/limiti dolmus kupon ARTIK SESSIZCE YOK SAYILMAZ - kaybedenler
    // HTTP 400 + "Bu kupon kullanim limitine ulasmis." alir ve SIPARIS SATIRI OLUSMAZ
    // (ret, transaction ve stok rezervasyonundan ONCE kosar). Kilit hala isini yapar:
    // TAM BIR kazanan kuponu alir.
    // NOT: bu degisiklik olmadan satir sonundaki "kuponsuz" asserti VAKUMA duserdi (bos liste).
    [Trait("Category", "Sql")]
    public class CouponRaceTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCouponRaceTest";
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

        private sealed class RaceFactory : WebApplicationFactory<Program>
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

        private RaceFactory? _factory;
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
                _factory = new RaceFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException("DIVISIMA_TEST_SQL verildi ancak kupon yaris testi ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        [Fact]
        public async Task SonHakkaSekizParalelIstek_KuponYALNIZ_BIRINDE_Uygulanir()
        {
            if (Skipped()) return;
            const int callers = 8;

            var productIds = new List<int>();
            string couponCode;
            var customerIds = new List<int>();

            await using (var ctx = NewContext())
            {
                var cat = new Category { name = "K", slug = $"k{Guid.NewGuid():N}", is_active = true, created_at = DateTime.Now };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                // HER cagriya AYRI urun: ayni ProductStock satirina eszamanli yazma, ReserveStock
                // optimistic concurrency retry limitini asip Conflict uretiyor ve KUPON yarisini
                // maskeliyordu (bkz. rapor). Ayri urunle olculen sey yalnizca kupon kilidi olur.
                for (int i = 0; i < callers; i++)
                {
                    var p = new Product
                    {
                        name = "Yaris Urun",
                        brand = "T",
                        category_id = cat.id,
                        price = 100m,
                        description = "d",
                        color_hex = "#000",
                        product_type = 0,
                        is_active = true,
                        created_at = DateTime.Now
                    };
                    ctx.Products.Add(p);
                    await ctx.SaveChangesAsync();
                    productIds.Add(p.id);
                    ctx.ProductStocks.Add(new ProductStock
                    {
                        product_id = p.id,
                        size = "M",
                        stock_quantity = 50,
                        reserved_quantity = 0,
                        is_active = true,
                        created_at = DateTime.Now
                    });
                    await ctx.SaveChangesAsync();
                }

                var cpn = new Coupon
                {
                    code = ("R" + Guid.NewGuid().ToString("N").Substring(0, 11)).ToUpperInvariant(),
                    discount_type = (byte)DiscountTypeEnum.Fixed,
                    value = 30m,
                    min_amount = 0m,
                    usage_limit = 1,
                    per_user_limit = 0,
                    first_order_only = false,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Coupon>().Add(cpn);
                await ctx.SaveChangesAsync();
                couponCode = cpn.code;

                for (int i = 0; i < callers; i++)
                {
                    var c = new Customer
                    {
                        name = "Yarisci",
                        email = $"race-{Guid.NewGuid():N}@divisima.test",
                        phone = "5550000000",
                        password_hash = new byte[] { 1 },
                        password_salt = new byte[] { 2 },
                        is_active = true,
                        email_verified = true,
                        store_credit = 0m,
                        created_at = DateTime.Now
                    };
                    ctx.Set<Customer>().Add(c);
                    await ctx.SaveChangesAsync();
                    customerIds.Add(c.id);
                }
            }

            // GF-6 / K2: `address_id` ARTIK ZORUNLU - her musteri kendi adresini alir
            // (eszamanlilik olcumu ADRES YAZIMIYLA kirlenmesin diye TURDAN ONCE hazirlanir).
            var adresIds = new List<int>();
            foreach (var cid in customerIds)
                adresIds.Add(await TestAdresHelper.AdresOlusturAsync(ConnStr, cid));

            // 8 AYRI musteri, 8 AYRI DI scope -> gercek eszamanlilik. Kupon usage_limit = 1.
            var tasks = customerIds.Select((cid, idx) => Task.Run(async () =>
            {
                using var scope = _factory!.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
                {
                    customer_id = cid,
                    address_id = adresIds[idx],
                    coupon_code = couponCode,
                    use_store_credit = 0m,
                    payment_method = 1,
                    items = new() { new OrderItemRequestDto { product_id = productIds[idx], size = "M", quantity = 1 } }
                });
            }));
            var results = await Task.WhenAll(tasks);

            var basarili = results.Count(r => r.Item2.Success);
            basarili.Should().BeGreaterThan(0, "en az bir siparis gecmeli (vakum engeli)");

            // MFIX-B / K2: kaybedenler ARTIK GORUNUR sekilde reddedilir.
            var reddedilen = results.Where(r => !r.Item2.Success).ToList();
            reddedilen.Should().HaveCount(8 - basarili, "basarisiz her cagri bir RET olmali");
            // CIFT-ANLAM KIRICI: ret KUPON limitinden gelmeli, baska bir dogrulamadan degil.
            reddedilen.Should().OnlyContain(r => r.Item2.Message == Messages.CouponUsageLimitReached,
                "ret sebebi kupon kullanim limiti olmali - stok/adres gibi baska bir sebep DEGIL");

            await using (var ctx = NewContext())
            {
                var kuponluSiparis = await ctx.Set<Order>().AsNoTracking()
                    .CountAsync(o => o.coupon_code == couponCode);
                var toplamSiparis = await ctx.Set<Order>().AsNoTracking().CountAsync();

                toplamSiparis.Should().Be(basarili, "her basarili cagri bir siparis satiri yazmali");
                kuponluSiparis.Should().Be(1,
                    "usage_limit=1 kupon coupon lock sayesinde YALNIZ BIR siparise uygulanmali");

                var kuponlu = await ctx.Set<Order>().AsNoTracking().FirstAsync(o => o.coupon_code == couponCode);
                kuponlu.discount_amount.Should().Be(30m, "kazanan siparis indirimi gercekten almali");

                // MFIX-B / K2 PREMIS DEGISIKLIGI: eskiden burada `kuponsuz` listesi DOLU olur ve
                // "indirim 0" assert edilirdi. Artik kaybedenler 400 aldigi icin O LISTE BOS -
                // eski assert (OnlyContain) bos koleksiyonda YANLIS SEBEPTEN kirilir, bu yuzden
                // iddia YENI SOZLESMEYE cevrildi: kuponsuz siparis HIC OLUSMAMALI.
                var kuponsuz = await ctx.Set<Order>().AsNoTracking().Where(o => o.coupon_code == null).ToListAsync();
                kuponsuz.Should().BeEmpty(
                    "gecersiz kupon artik SESSIZCE dusurulmuyor - kuponsuz bir siparis OLUSMAMALI");
            }
        }

        // ── P12 (MFIX-B / K2) ────────────────────────────────────────────────────────────
        // OLCULEN ONCE-DURUM (canli, gercek JWT, ESKI ikili): var olmayan bir kodla
        //   POST /api/order/place -> HTTP 201 + {"data":224,...}
        //   DB: siparis 224 discount_amount 0.00, coupon_code NULL
        // Yani kupon SESSIZCE yutuluyordu; musteri odeme ekraninda indirimli tutar gorup
        // FARKLI tutar oduyor ve sebebi HICBIR YERDE yazmiyordu.
        //
        // IKINCI YARI - ASIMETRI: per_user_limit PlaceOrder'da ZATEN uygulaniyordu ama onizleme
        // ucu (ValidateCoupon) onu HIC kontrol etmiyordu. Yani onizleme "gecerli" derken siparis
        // kuponu dusuruyordu; K2 ile siparis 400 donmeye basladigi icin bu asimetri kapatilmasaydi
        // hakkini doldurmus musterinin checkout'u KALICI olarak 400 verirdi.
        [Fact]
        public async Task KuponGecersizse_Place_400_ve_Validate_PerUserLimit_Reddeder()
        {
            if (!_sqlAvailable) return;

            int urunId, musteriId;
            string gecerliKod;
            await using (var ctx = NewContext())
            {
                var cat = new Category { name = "P12", slug = $"p12{Guid.NewGuid():N}", is_active = true, created_at = DateTime.Now };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "P12 Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 100m,
                    description = "d",
                    color_hex = "#000",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Products.Add(p);
                await ctx.SaveChangesAsync();
                urunId = p.id;
                ctx.ProductStocks.Add(new ProductStock
                {
                    product_id = p.id,
                    size = "M",
                    stock_quantity = 50,
                    reserved_quantity = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });

                var cpn = new Coupon
                {
                    code = ("P" + Guid.NewGuid().ToString("N").Substring(0, 11)).ToUpperInvariant(),
                    discount_type = (byte)DiscountTypeEnum.Fixed,
                    value = 25m,
                    min_amount = 0m,
                    usage_limit = 0,
                    per_user_limit = 1,          // ASIMETRININ olculdugu kural
                    first_order_only = false,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Coupon>().Add(cpn);

                var c = new Customer
                {
                    name = "P12",
                    email = $"p12-{Guid.NewGuid():N}@example.com",
                    phone = "5550000000",
                    password_hash = new byte[] { 1 },
                    password_salt = new byte[] { 2 },
                    is_active = true,
                    email_verified = true,
                    store_credit = 0m,
                    created_at = DateTime.Now
                };
                ctx.Set<Customer>().Add(c);
                await ctx.SaveChangesAsync();
                gecerliKod = cpn.code;
                musteriId = c.id;
            }

            // GF-6 / K2: adres ARTIK ZORUNLU. CIFT-ANLAM KIRICI olarak da SART - adressiz
            // istek 400'u KUPONDAN degil ADRESTEN alirdi ve bu testin iddiasi curur.
            var p12Adres = await TestAdresHelper.AdresOlusturAsync(ConnStr, musteriId);

            OrderCreateRequestDto Istek(int musteri, int urun, string kod) => new()
            {
                customer_id = musteri,
                address_id = p12Adres,
                coupon_code = kod,
                use_store_credit = 0m,
                payment_method = 1,
                items = new() { new OrderItemRequestDto { product_id = urun, size = "M", quantity = 1 } }
            };

            // (a) VAR OLMAYAN KOD -> 400 + ADIYLA sebep (once: 201, sessizce yutuluyordu)
            using (var scope = _factory!.Services.CreateScope())
            {
                var r = await scope.ServiceProvider.GetRequiredService<IOrderService>()
                    .PlaceOrder(Istek(musteriId, urunId, "BOYLEBIRKUPONYOK"));
                r.Item1.Should().Be(HttpStatusCode.BadRequest, "gecersiz kupon SESSIZCE yok sayilmamali");
                r.Item2.Message.Should().Be(Messages.CouponInvalid,
                    "cift-anlam kirici: 400 kupon kodundan gelmeli, stok/adres gibi baska bir sebepten DEGIL");
            }
            await using (var ctx = NewContext())
            {
                (await ctx.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == musteriId))
                    .Should().Be(0, "reddedilen istek SIPARIS SATIRI BIRAKMAMALI - 400 kozmetik degil");
                (await ctx.Set<StockReservation>().AsNoTracking().CountAsync())
                    .Should().Be(0, "ret transaction ve rezervasyondan ONCE kosmali");
            }

            // (b) VAKUM KIRICI: GECERLI kod AYNEN calisir (kural "her seyi reddet" degil)
            using (var scope = _factory!.Services.CreateScope())
            {
                var ok = await scope.ServiceProvider.GetRequiredService<IOrderService>()
                    .PlaceOrder(Istek(musteriId, urunId, gecerliKod));
                ok.Item2.Success.Should().BeTrue($"gecerli kupon kabul edilmeli: {ok.Item2.Message}");
            }
            await using (var ctx = NewContext())
            {
                var s = await ctx.Set<Order>().AsNoTracking().FirstAsync(o => o.customer_id == musteriId);
                s.discount_amount.Should().Be(25m, "gecerli kuponun indirimi GERCEKTEN uygulanmali");
                s.coupon_code.Should().Be(gecerliKod);
            }

            // (c) ASIMETRI KAPANDI: hak dolunca ONIZLEME de reddeder (once: "gecerli" diyordu)
            using (var scope = _factory!.Services.CreateScope())
            {
                var v = await scope.ServiceProvider.GetRequiredService<ICouponService>()
                    .ValidateCoupon(new CouponValidateRequestDto { code = gecerliKod, cart_total = 100m, customer_id = musteriId });
                v.Item1.Should().Be(HttpStatusCode.BadRequest,
                    "per_user_limit dolu - onizleme ARTIK reddetmeli (PlaceOrder zaten reddediyordu)");
                v.Item2.Message.Should().Be(Messages.CouponPerUserLimitReached);
            }

            // (d) CIFT-ANLAM KIRICI: ayni kuponu HIC kullanmamis BASKA musteri icin onizleme GECERLI
            int digerMusteri;
            await using (var ctx = NewContext())
            {
                var c2 = new Customer
                {
                    name = "P12b",
                    email = $"p12b-{Guid.NewGuid():N}@example.com",
                    phone = "5550000001",
                    password_hash = new byte[] { 1 },
                    password_salt = new byte[] { 2 },
                    is_active = true,
                    email_verified = true,
                    store_credit = 0m,
                    created_at = DateTime.Now
                };
                ctx.Set<Customer>().Add(c2);
                await ctx.SaveChangesAsync();
                digerMusteri = c2.id;
            }
            using (var scope = _factory!.Services.CreateScope())
            {
                var v2 = await scope.ServiceProvider.GetRequiredService<ICouponService>()
                    .ValidateCoupon(new CouponValidateRequestDto { code = gecerliKod, cart_total = 100m, customer_id = digerMusteri });
                v2.Item1.Should().Be(HttpStatusCode.OK,
                    "kural KULLANICI BASINA - baskasinin hakkini tuketmesi bu musteriyi engellememeli");
            }
        }
    }
}
