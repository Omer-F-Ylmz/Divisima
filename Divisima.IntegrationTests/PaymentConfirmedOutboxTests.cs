using System.Text.Json;
using Autofac;
using Divisima.Bussiness.Concrete;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 8 MADDE 3 - "PaymentConfirmed" OUTBOX'A TASINDI (SECENEK A: DORT ADIM DA)
    //
    // OLCULEN SORUN: fatura / sadakat / referans odulu / kupon sayaci commit SONRASI
    // "best-effort" kosuyordu. Patlarlarsa adiyla loglaniyor ve zaman cizelgesine not dusuluyordu -
    // ama HIC YENIDEN DENENMIYORDU. Gecici bir aksaklik (DB kesintisi, saglayici zaman asimi) o
    // yan etkiyi KALICI OLARAK kaybettiriyordu: fatura hic kesilmiyor, sadakat hic verilmiyor,
    // referans odulu hic odenmiyor - ustelik musteri "siparisin onaylandi" goruyor.
    //
    // KARAR (kullanici): SECENEK A - dort adim da TEK "PaymentConfirmed" mesajinda.
    // Gerekce: kaybedilen yan etki SESSIZ ve KALICI, gecikme ise GORUNUR ve GECICI. Kupon sayacini
    // inline birakmak "kimi yan etki senkron kimi asenkron" diye gerekcesiz bir ikilik yaratirdi.
    //
    // AT-LEAST-ONCE ZORUNLULUGU: mesaj birden fazla teslim edilebilir. Dort adimin idempotentlik
    // dayanaklari (hepsi VERITABANI duzeyinde ya da turetme):
    //   fatura         -> "bu siparis icin fatura zaten var" + durum guard'i (madde 2)
    //   sadakat        -> UX_loyalty_transactions_order_earn (Sprint 6)
    //   referans odulu -> UX_store_credit_referee_reward (madde 3 - bu dalgada eklendi)
    //   kupon sayaci   -> coupon_usages'tan TURETME (madde 1) + UX_coupon_usages_coupon_order
    [Trait("Category", "Sql")]
    public class PaymentConfirmedOutboxTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaPaymentConfirmedOutboxTest";
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

        // Sadakat adimini ISTEGE BAGLI patlatan sarmalayici. GERCEK LoyaltyManager'dan TUREME -
        // bayrak kapaliyken gercek kod kosar; "her sey sahte" bir kurulumda bu testler hicbir sey
        // olcmezdi. (PaymentCallbackSecurityTests'teki ayni kalip.)
        private sealed class KontrolluLoyalty : LoyaltyManager, Divisima.Bussiness.Abstract.ILoyaltyService
        {
            public KontrolluLoyalty(Divisima.DataAccess.Abstract.ICustomerDal customerDal,
                Divisima.DataAccess.Abstract.IOrderDal orderDal,
                Divisima.DataAccess.Abstract.ILoyaltyTransactionDal txDal,
                Divisima.DataAccess.Abstract.IStoreCreditTransactionDal creditTxDal,
                Divisima.Core.DataAccess.IUnitOfWork unitOfWork)
                : base(customerDal, orderDal, txDal, creditTxDal, unitOfWork) { }

            // STATIK BAYRAK TEST SINIRINI ASAR - InitializeAsync'te SIFIRLANIR (S7 tuzagi).
            public static bool EarnPatlasin;

            public new async Task<(System.Net.HttpStatusCode, Divisima.Core.Utilities.Results.Result)> EarnFromOrder(
                int customerId, decimal orderTotal, int orderId)
            {
                if (EarnPatlasin)
                    throw new InvalidOperationException("TEST: sadakat adimi patladi (enjekte edilmis hata)");
                return await base.EarnFromOrder(customerId, orderTotal, orderId);
            }
        }

        private sealed class OutboxFactory : WebApplicationFactory<Program>
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

            // Autofac modulundeki servisler services.AddScoped ile EZILEMEZ (CLAUDE.md tuzagi):
            // AutofacServiceProviderFactory once Populate yapar, modul SONRA kaydeder ve Autofac'te
            // son kayit kazanir. Bu yuzden hata enjeksiyonu MODULDEN SONRA calisacak bir
            // ConfigureContainer ile yapilir - CreateHost override edilerek.
            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.ConfigureContainer<Autofac.ContainerBuilder>(cb =>
                {
                    cb.RegisterType<KontrolluLoyalty>()
                      .As<Divisima.Bussiness.Abstract.ILoyaltyService>()
                      .InstancePerLifetimeScope();
                });
                return base.CreateHost(builder);
            }
        }

        private OutboxFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            KontrolluLoyalty.EarnPatlasin = false;   // statik bayrak SIFIRLANIR
            try
            {
                await using (var pre = NewContext())
                {
                    await pre.Database.EnsureDeletedAsync();
                    await pre.Database.EnsureCreatedAsync();
                }
                _factory = new OutboxFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak outbox testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            KontrolluLoyalty.EarnPatlasin = false;
            if (_factory != null) await _factory.DisposeAsync();
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private async Task IsleAsync()
        {
            using var scope = _factory!.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessPendingAsync();
        }

        // DORT ADIMIN DA ANLAMLI oldugu bir senaryo kurar: davet edilmis musteri (referans),
        // kuponlu ve kalemli ONAYLI siparis.
        private sealed record Senaryo(int OrderId, int RefereeId, int ReferrerId, int CouponId, string CouponCode);

        private static async Task<Senaryo> SenaryoKurAsync()
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            await using var ctx = NewContext();

            Customer Musteri(string ad) => new()
            {
                name = ad + " " + damga,
                email = $"{ad.ToLowerInvariant()}-{damga.ToLowerInvariant()}@example.com",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                user_type = (byte)UserTypeEnum.Customer,
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };

            var davetEden = Musteri("Referrer");
            ctx.Set<Customer>().Add(davetEden);
            await ctx.SaveChangesAsync();

            var davetEdilen = Musteri("Referee");
            davetEdilen.referred_by = davetEden.id;
            ctx.Set<Customer>().Add(davetEdilen);
            await ctx.SaveChangesAsync();

            var kategori = new Category
            {
                name = "Outbox Kategori " + damga,
                slug = "outbox-kat-" + damga.ToLowerInvariant(),
                display_order = 1,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Outbox Urunu " + damga,
                brand = "Divisima",
                category_id = kategori.id,
                price = 100.00m,
                description = "Outbox pini icin urun.",
                color_hex = "#445566",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Product>().Add(urun);
            await ctx.SaveChangesAsync();

            var kupon = new Coupon
            {
                code = "OUTBOX" + damga,
                discount_type = 0,
                value = 10m,
                min_amount = 0m,
                usage_limit = 100,
                used_count = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Coupon>().Add(kupon);
            await ctx.SaveChangesAsync();

            var siparis = new Order
            {
                customer_id = davetEdilen.id,
                order_number = "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + damga,
                status = (byte)OrderStatusEnum.Confirmed,   // fatura guard'i (madde 2) icin ZORUNLU
                subtotal = 100m,
                discount_amount = 0m,
                shipping_cost = 0m,
                total_price = 100m,
                currency = "TRY",
                coupon_code = kupon.code,
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
                quantity = 1,
                unit_price = 100m,
                is_cancelled = false,
                created_at = DateTime.Now
            });
            // Kupon kullanim satiri A bolgesinde yaziliyor - sayac BUNDAN turetiliyor.
            ctx.Set<CouponUsage>().Add(new CouponUsage
            {
                coupon_id = kupon.id,
                customer_id = davetEdilen.id,
                order_id = siparis.id,
                discount_applied = 10m,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            return new Senaryo(siparis.id, davetEdilen.id, davetEden.id, kupon.id, kupon.code);
        }

        private static async Task<int> MesajYazAsync(Senaryo s)
        {
            await using var ctx = NewContext();
            var msg = new OutboxMessage
            {
                event_type = "PaymentConfirmed",
                payload = JsonSerializer.Serialize(new PaymentConfirmedEvent
                {
                    order_id = s.OrderId,
                    customer_id = s.RefereeId,
                    total_price = 100m,
                    coupon_code = s.CouponCode
                }),
                status = 0,
                retry_count = 0,
                created_at = DateTime.Now
            };
            ctx.Set<OutboxMessage>().Add(msg);
            await ctx.SaveChangesAsync();
            return msg.id;
        }

        private static async Task MesajiYenidenTeslimEdilebilirYapAsync(int messageId)
        {
            // AT-LEAST-ONCE TAKLIDI: isleyici mesaji Processed yapti; gercek bir yeniden teslimat
            // (ag bolunmesi, iki instance, reclaim) onu tekrar Pending gorur. Durumu geri alarak
            // AYNI mesajin IKINCI kez islenmesini saglariz.
            await using var ctx = NewContext();
            var m = await ctx.Set<OutboxMessage>().SingleAsync(x => x.id == messageId);
            m.status = 0;
            m.processed_at = null;
            await ctx.SaveChangesAsync();
        }

        private sealed record Sayimlar(int Fatura, int Sadakat, int RefereeOdul, int ReferrerOdul, int KuponSayaci);

        private static async Task<Sayimlar> SayAsync(Senaryo s)
        {
            await using var ctx = NewContext();
            return new Sayimlar(
                await ctx.Set<Invoice>().CountAsync(i => i.order_id == s.OrderId),
                await ctx.Set<LoyaltyTransaction>().CountAsync(t => t.order_id == s.OrderId
                        && t.type == (byte)LedgerEntryTypeEnum.Earn),
                await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == s.RefereeId
                        && t.reason == ReferralManager.RefereeRewardReason),
                await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == s.ReferrerId
                        && t.reason == ReferralManager.ReferrerRewardReason),
                (await ctx.Set<Coupon>().AsNoTracking().SingleAsync(c => c.id == s.CouponId)).used_count);
        }

        // ── 1) IKINCI TESLIMAT DORT ADIMIN HICBIRINDE FAZLA ETKI URETMEZ ──────────────
        //
        // Kullanicinin (i) sarti. VAKUM KIRICI: once dort adimin da GERCEKTEN uygulandigi
        // dogrulanir - hicbiri kosmasaydi "ikinci teslimat fazla etki uretmedi" iddiasi
        // bombos kalirdi.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task IkinciTeslimat_DORT_ADIMIN_HICBIRINDE_FAZLA_ETKI_URETMEZ()
        {
            if (Skipped()) return;

            var s = await SenaryoKurAsync();
            var messageId = await MesajYazAsync(s);

            await IsleAsync();

            var ilk = await SayAsync(s);
            ilk.Fatura.Should().Be(1, "POZITIF OLAY: fatura kesilmeli");
            ilk.Sadakat.Should().Be(1, "POZITIF OLAY: sadakat kazanimi yazilmali");
            ilk.RefereeOdul.Should().Be(1, "POZITIF OLAY: davet edilene odul verilmeli");
            ilk.ReferrerOdul.Should().Be(1, "POZITIF OLAY: davet edene odul verilmeli");
            ilk.KuponSayaci.Should().Be(1, "POZITIF OLAY: kupon sayaci kullanim satirindan turetilmeli");

            // AYNI mesaj ikinci kez teslim edilir.
            await MesajiYenidenTeslimEdilebilirYapAsync(messageId);
            await IsleAsync();

            var ikinci = await SayAsync(s);
            ikinci.Should().BeEquivalentTo(ilk,
                "at-least-once teslimatta IKINCI islem hicbir adimda FAZLA etki uretmemeli: " +
                "fatura tek, puan tek, odul tek, sayac dogru");

            // Ucuncu teslimat da ayni - "iki kez dayandi, ucuncude kirildi" ihtimalini kapatir.
            await MesajiYenidenTeslimEdilebilirYapAsync(messageId);
            await IsleAsync();
            (await SayAsync(s)).Should().BeEquivalentTo(ilk, "ucuncu teslimat da fazla etki uretmemeli");
        }

        // ── 2) BES DENEMEDE TUKENEN MESAJ GURULTULU KALIR ─────────────────────────────
        //
        // Kullanicinin (ii) sarti: H53 kalibi - kalici basarisizlik SESSIZLESMEZ.
        // Onceden bu yalniz outbox_messages tablosunda bir satirdi; kimse bakmazsa GORUNMEZDI.
        // Artik siparis zaman cizelgesine "KRITIK" notu dusuyor - operator panelde gorur.
        //
        // CIFT-ANLAM KIRICI: not YALNIZ hak tukendiginde yazilmali. Ilk dort denemede mesaj
        // Pending kalir ve KRITIK notu YAZILMAZ - aksi halde her gecici hata operatore kalici
        // bir alarm gibi gorunurdu.
        [Fact]
        [Trait("Category", "Sql")]
        public async Task BesDenemeSonunda_Failed_ve_ZamanCizelgesinde_KRITIK_Notu_Kalir()
        {
            if (Skipped()) return;

            var s = await SenaryoKurAsync();
            await MesajYazAsync(s);

            KontrolluLoyalty.EarnPatlasin = true;

            // 1. deneme: Pending kalir, KRITIK notu YOK.
            await IsleAsync();
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<OutboxMessage>().AsNoTracking().SingleAsync();
                m.status.Should().Be((byte)0, "ilk hatada mesaj Pending kalmali");
                m.retry_count.Should().Be(1);
                (await ctx.Set<OrderStatusHistory>().CountAsync(h => h.order_id == s.OrderId
                        && h.note != null && h.note.Contains("KRITIK")))
                    .Should().Be(0, "hak tukenmeden KRITIK notu yazilmamali - gecici hata alarm degildir");
            }

            // 2..5. denemeler: 5'te hak tukenir.
            for (int i = 0; i < 4; i++) await IsleAsync();

            await using (var son = NewContext())
            {
                var m = await son.Set<OutboxMessage>().AsNoTracking().SingleAsync();
                m.status.Should().Be((byte)2, "bes denemeden sonra mesaj Failed olmali");
                m.retry_count.Should().Be(5);
                m.error.Should().NotBeNullOrWhiteSpace("hata metni kaydedilmeli");

                var notlar = await son.Set<OrderStatusHistory>().AsNoTracking()
                    .Where(h => h.order_id == s.OrderId).Select(h => h.note).ToListAsync();
                notlar.Should().Contain(n => n != null && n.Contains("KRITIK"),
                    $"kalici basarisizlik zaman cizelgesinde GORUNMELI. Notlar: {string.Join(" | ", notlar)}");
            }
        }
    }
}
