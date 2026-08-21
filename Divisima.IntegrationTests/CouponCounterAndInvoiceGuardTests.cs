using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Abstract;
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
    // SPRINT 8 MADDE 1 (kupon sayaci idempotency) + MADDE 2 (fatura durum guard'i)
    //
    // MADDE 1 - OLCULEN SORUN: `used_count` duz bir "+= 1" ile artiriliyordu. Bugun zararsizdi
    // cunku callback tam bir kez kosuyor; ama B bolgesi at-least-once bir mekanizmaya (outbox -
    // madde 3) tasindiginda ayni siparis icin sayac BIRDEN FAZLA artardi ve kupon limiti
    // gercekte dolmadan "dolmus" gorunurdu. Yeniden deneme bunu kurtaramaz: ikinci artis bir
    // HATA degil, basarili bir yazma olarak gorunur.
    // NOT (olculdu): eski kodun ESZAMANLILIK yonu DOGRUYDU - `coupons.row_version` gercek bir
    // rowversion token ve kayip guncelleme istisnaya donusuyordu. Sorun yalnizca idempotency'di.
    //
    // MADDE 2 - OLCULEN SORUN: `InvoiceManager.GenerateForOrder` siparis DURUMUNU kontrol
    // etmiyordu; var olan herhangi bir siparis id'si icin fatura kesiyordu. Sprint 7'de odeme
    // akisindaki cagri onay dalina tasindi ve o YOL duzeldi, ama UCUN KENDISI korumasiz kaldi.
    // Fatura mali bir beyandir: iptal edilmis siparise kesilen fatura ciroyu sisirir, odenmemis
    // siparise kesilen fatura musteriye olmayan bir borc gonderir.
    [Trait("Category", "Sql")]
    public class CouponCounterAndInvoiceGuardTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaCouponInvoiceGuardTest";
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

        private sealed class GuardFactory : WebApplicationFactory<Program>
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

        private GuardFactory? _factory;
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
                _factory = new GuardFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak kupon/fatura guard testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> f)
        {
            using var scope = _factory!.Services.CreateScope();
            return await f(scope.ServiceProvider);
        }

        // Her test kendi kuponunu/siparisini Guid ile uretir - var olan satirlara guvenilmez.
        private static string Damga() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

        private static async Task<int> KuponEkleAsync(string kod)
        {
            await using var ctx = NewContext();
            var c = new Coupon
            {
                code = kod,
                discount_type = 0,
                value = 10m,
                min_amount = 0m,
                usage_limit = 100,
                used_count = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Coupon>().Add(c);
            await ctx.SaveChangesAsync();
            return c.id;
        }

        private static async Task<int> SiparisEkleAsync(byte status)
        {
            var damga = Damga();
            await using var ctx = NewContext();
            var musteri = new Customer
            {
                name = "Guard Musteri " + damga,
                email = $"guard-{damga.ToLowerInvariant()}@example.com",
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

            var o = new Order
            {
                customer_id = musteri.id,
                order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + damga,
                status = status,
                subtotal = 100m,
                discount_amount = 0m,
                shipping_cost = 0m,
                total_price = 100m,
                currency = "TRY",
                payment_type = 1,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(o);
            await ctx.SaveChangesAsync();
            return o.id;
        }

        // ── MADDE 1 / 1) SAYAC IDEMPOTENT: AYNI ADIM IKI KEZ KOSSA DA FAZLA SAYMAZ ────
        //
        // Bu, outbox'a (madde 3) gecisin ON KOSULU. Turetme tanimi geregi idempotent oldugu icin
        // "iki kez kos" senaryosu SAYIYI DEGISTIRMEMELI.
        // VAKUM KIRICI: once sayacin GERCEKTEN 1 oldugu dogrulanir (hicbir sey olmadan da 0
        // kalsaydi test yesil gorunurdu).
        [Fact]
        [Trait("Category", "Sql")]
        public async Task KuponSayaci_TURETILIR_AyniAdim_IKI_KEZ_Kossa_da_FAZLA_SAYMAZ()
        {
            if (Skipped()) return;

            var kuponId = await KuponEkleAsync("IDEM" + Damga());
            var siparisId = await SiparisEkleAsync((byte)OrderStatusEnum.Confirmed);

            await using (var ctx = NewContext())
            {
                ctx.Set<CouponUsage>().Add(new CouponUsage
                {
                    coupon_id = kuponId,
                    customer_id = (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == siparisId)).customer_id,
                    order_id = siparisId,
                    discount_applied = 10m,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            // BIRINCI kosum
            await WithScopeAsync(sp => sp.GetRequiredService<ICouponDal>().SyncUsedCountAsync(kuponId));
            await using (var ctx = NewContext())
                (await ctx.Set<Coupon>().AsNoTracking().SingleAsync(c => c.id == kuponId)).used_count
                    .Should().Be(1, "POZITIF OLAY: sayac gercekten yazilmali - hicbir sey olmasa 0 kalirdi");

            // IKINCI ve UCUNCU kosum (at-least-once mekanizmasinin yeniden denemesi)
            await WithScopeAsync(sp => sp.GetRequiredService<ICouponDal>().SyncUsedCountAsync(kuponId));
            await WithScopeAsync(sp => sp.GetRequiredService<ICouponDal>().SyncUsedCountAsync(kuponId));

            await using (var ctx = NewContext())
                (await ctx.Set<Coupon>().AsNoTracking().SingleAsync(c => c.id == kuponId)).used_count
                    .Should().Be(1,
                        "TURETME idempotenttir - eski '+= 1' kalibi burada 3 verirdi ve kupon limiti " +
                        "gercekte dolmadan 'dolmus' gorunurdu");
        }

        // ── MADDE 1 / 2) IKINCI SAVUNMA HATTI: UNIQUE INDEKS ──────────────────────────
        //
        // Sayac artik `coupon_usages` satirlarindan turetildigi icin dogrulugu "ayni siparis
        // icin iki kullanim satiri olusamaz" garantisine BAGLI. Uygulama katmani satiri
        // transaction icinde yaziyor, ama veritabani duzeyinde de engellenmeli - aksi halde
        // turetme YANLIS bir kaynaktan beslenir.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task AyniSiparise_IKINCI_KuponKullanimSatiri_VERITABANINDA_ENGELLENIR()
        {
            if (Skipped()) return;

            var kuponId = await KuponEkleAsync("UNQ" + Damga());
            var siparisId = await SiparisEkleAsync((byte)OrderStatusEnum.Confirmed);
            int musteriId;
            await using (var ctx = NewContext())
                musteriId = (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == siparisId)).customer_id;

            CouponUsage Satir() => new CouponUsage
            {
                coupon_id = kuponId,
                customer_id = musteriId,
                order_id = siparisId,
                discount_applied = 10m,
                created_at = DateTime.Now
            };

            await using (var ctx = NewContext())
            {
                ctx.Set<CouponUsage>().Add(Satir());
                await ctx.SaveChangesAsync();   // ILKI GECMELI - vakum kirici
            }

            Func<Task> ikinci = async () =>
            {
                await using var ctx = NewContext();
                ctx.Set<CouponUsage>().Add(Satir());
                await ctx.SaveChangesAsync();
            };

            await ikinci.Should().ThrowAsync<DbUpdateException>(
                "UX_coupon_usages_coupon_order ayni siparis icin ikinci kullanim satirini ENGELLEMELI");

            await using (var son = NewContext())
                (await son.Set<CouponUsage>().AsNoTracking()
                    .CountAsync(u => u.coupon_id == kuponId && u.order_id == siparisId))
                    .Should().Be(1, "reddedilen insert satir birakmamali");
        }

        // ── MADDE 2 / 1) IPTAL EDILMIS SIPARISE FATURA KESILMEZ ───────────────────────
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Fatura_IPTAL_EDILMIS_Siparise_KESILMEZ()
        {
            if (Skipped()) return;

            var siparisId = await SiparisEkleAsync((byte)OrderStatusEnum.Cancelled);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IInvoiceService>().GenerateForOrder(siparisId));

            r.Item1.Should().Be(HttpStatusCode.BadRequest);
            r.Item2.Message.Should().Contain("faturalamaya uygun değil",
                "cift-anlam kirici: 400 baska bir sebepten degil, DURUM guard'indan gelmeli");

            await using var ctx = NewContext();
            (await ctx.Set<Invoice>().AsNoTracking().CountAsync(i => i.order_id == siparisId))
                .Should().Be(0, "iptal edilmis siparise fatura satiri OLUSMAMALI - 400 kozmetik degil");
        }

        // ── MADDE 2 / 2) ODEMESI TAMAMLANMAMIS (PENDING) SIPARISE FATURA KESILMEZ ─────
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Fatura_PENDING_Siparise_KESILMEZ()
        {
            if (Skipped()) return;

            var siparisId = await SiparisEkleAsync((byte)OrderStatusEnum.Pending);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IInvoiceService>().GenerateForOrder(siparisId));

            r.Item1.Should().Be(HttpStatusCode.BadRequest);

            await using var ctx = NewContext();
            (await ctx.Set<Invoice>().AsNoTracking().CountAsync(i => i.order_id == siparisId))
                .Should().Be(0, "para henuz alinmamis siparise fatura kesilmemeli");
        }

        // ── MADDE 2 / 3) VAKUM + CIFT-ANLAM KIRICI: ONAYLI SIPARISE FATURA KESILIR ────
        //
        // Bu olmadan yukaridaki iki pin, "GenerateForOrder her zaman 400 doner" durumunda da
        // yesil kalirdi ve guard'in DAR oldugunu hicbir sey kanitlamazdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task Fatura_ONAYLI_Siparise_KESILIR_GuardDAR()
        {
            if (Skipped()) return;

            var siparisId = await SiparisEkleAsync((byte)OrderStatusEnum.Confirmed);

            var r = await WithScopeAsync(sp => sp.GetRequiredService<IInvoiceService>().GenerateForOrder(siparisId));

            r.Item1.Should().Be(HttpStatusCode.OK, $"onayli siparis faturalanabilmeli: {r.Item2.Message}");

            await using var ctx = NewContext();
            (await ctx.Set<Invoice>().AsNoTracking().CountAsync(i => i.order_id == siparisId))
                .Should().Be(1, "POZITIF OLAY: fatura satiri GERCEKTEN olusmali");
        }
    }
}
