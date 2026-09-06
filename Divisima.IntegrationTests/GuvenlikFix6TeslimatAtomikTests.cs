using System.Net;
using Autofac;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Integrations.Shipping;
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
    // ══ GF-6 / F5 (push oncesi L3 BULGU-1) - TESLIMAT YAZIMLARI ATOMIK ═════════════════════
    //
    // OLCULEN ONCE-DURUM: `ShipmentManager`da transaction sayisi **0** idi (POZ kontrol:
    // `OrderManager` 5). Teslimat dali DORT yazmayi ayri ayri kosuyordu; olay yazimi duserse
    // siparis `Delivered` OLUYOR ama `PaymentConfirmed` olayi YAZILMIYORDU -> kapida odemede
    // sadakat/referans HIC verilmiyor ve TELAFI YOLU YOK (admin ayni durumu tekrar yazinca
    // `order.status != Delivered` guard'i yuzunden yeni olay uretilmez). `[PARA]` LATENT.
    //
    // BU SINIF NEDEN AYRI: hata enjeksiyonu DI'da yapiliyor (`IOutboxService` -> atan surum) ve
    // bu, sinif BASINA bir fabrika ister. `IOutboxService` `AutofacBusinessModule`de kayitli;
    // CLAUDE.md bolum 5: modul servisleri `services.AddScoped` ile EZILEMEZ - `CreateHost`
    // override edilip `ConfigureContainer<ContainerBuilder>` MODULDEN SONRA calistirilir.
    // Emsal: `PaymentCallbackSecurityTests` ve `PaymentConfirmedOutboxTests`.
    [Trait("Category", "Sql")]
    public class GuvenlikFix6TeslimatAtomikTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaGuvenlikFix6TeslimatTest";
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

        // HATA ENJEKSIYONU: yalniz "PaymentConfirmed" yaziminda atar; diger olaylar (or.
        // "EmailNotification") NORMAL yazilir - boylece testin olctugu sey DAR kalir.
        //
        // STATIK BAYRAK SIFIRLANIR (CLAUDE.md bolum 5: statik enjeksiyon bayraklari test
        // sinirini ASAR): `InitializeAsync` her kosumda `false`a ceker.
        public static bool OlayYazimiPatlasin;

        private sealed class PatlayanOutbox : IOutboxService
        {
            private readonly OutboxService _gercek;
            public PatlayanOutbox(OutboxService gercek) { _gercek = gercek; }

            public Task WriteAsync(string eventType, object payload)
            {
                if (OlayYazimiPatlasin && eventType == "PaymentConfirmed")
                    throw new InvalidOperationException("F5 olcumu: olay yazimi bilerek dusuruldu.");
                return _gercek.WriteAsync(eventType, payload);
            }
        }

        // Kargo firmasi HER ZAMAN "teslim edildi" doner - dis bagimlilik YOK.
        private sealed class TeslimEdildiKargo : ICarrierProvider
        {
            public Task<CarrierTrackingResult> TrackAsync(byte carrier, string trackingNumber) =>
                Task.FromResult(new CarrierTrackingResult
                {
                    Success = true,
                    NormalizedStatus = (byte)ShipmentStatusEnum.Delivered,
                    RawStatusText = "Teslim edildi",
                    DeliveredAt = DateTime.Now
                });
        }

        private sealed class TeslimatFactory : WebApplicationFactory<Program>
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

            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.ConfigureContainer<ContainerBuilder>(cb =>
                {
                    cb.RegisterType<OutboxService>().AsSelf().InstancePerLifetimeScope();
                    cb.RegisterType<PatlayanOutbox>().As<IOutboxService>().InstancePerLifetimeScope();
                    cb.RegisterType<TeslimEdildiKargo>().As<ICarrierProvider>().InstancePerLifetimeScope();
                });
                return base.CreateHost(builder);
            }
        }

        private TeslimatFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            OlayYazimiPatlasin = false;   // CLAUDE.md bolum 5: statik bayrak HER kosumda sifirlanir
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new TeslimatFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak F5 teslimat testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            OlayYazimiPatlasin = false;
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

        // Kargoya verilmis (Shipped) bir siparis + kargo kaydi kurar.
        private static async Task<(int musteriId, int siparisId)> KargodaSiparisAsync()
        {
            await using var ctx = NewContext();

            var musteri = new Customer
            {
                name = "F5 Musteri",
                email = $"f5-{Guid.NewGuid():N}@example.com",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(musteri);
            await ctx.SaveChangesAsync();

            var siparis = new Order
            {
                customer_id = musteri.id,
                order_number = "F5-" + Guid.NewGuid().ToString("N").Substring(0, 10),
                status = (byte)OrderStatusEnum.Shipped,
                subtotal = 100m,
                discount_amount = 0m,
                shipping_cost = 0m,
                total_price = 100m,
                store_credit_used = 0m,
                coupon_code = null,
                payment_type = 1,                 // kapida odeme - F1'in para anlami TESLIMATTA
                is_online_payment_done = false,
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(siparis);
            await ctx.SaveChangesAsync();

            ctx.Set<Shipment>().Add(new Shipment
            {
                order_id = siparis.id,
                carrier = 0,
                tracking_number = "TRK" + Guid.NewGuid().ToString("N").Substring(0, 8),
                status = (byte)ShipmentStatusEnum.InTransit,
                shipped_at = DateTime.Now,
                created_at = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            return (musteri.id, siparis.id);
        }

        // ── DAVRANIS: OLAY YAZIMI DUSERSE SIPARIS Delivered OLMAZ ───────────────────────────
        //
        // KIRMIZI-ONCE: F5 oncesinde bu test BASARISIZ olurdu - siparis `Delivered` OLUYOR,
        // olay YAZILMIYORDU. (Kanit: MUT-F5, transaction kaldirilinca TAM 1 isimli kirmizi.)
        [Fact]
        public async Task F5_OLAY_YAZIMI_DUSERSE_SIPARIS_TESLIM_EDILDI_OLMAZ()
        {
            if (Skipped()) return;
            var (musteriId, siparisId) = await KargodaSiparisAsync();

            OlayYazimiPatlasin = true;
            try
            {
                // Takip cagrisi: kargo "teslim edildi" der -> teslimat dali kosar -> olay yazimi PATLAR.
                var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IShipmentService>()
                    .TrackByOrder(siparisId, musteriId));
                // Istisna yukari cikarsa cagri 500 doner; ONEMLI OLAN yanit degil, VERITABANI DURUMU.
                sonuc.Item1.Should().NotBe(HttpStatusCode.OK,
                    "olay yazimi dusen bir teslimat BASARILI raporlanmamali");
            }
            catch (Exception)
            {
                // Istisnanin uca kadar cikmasi da kabul - olculen sey ASAGIDAKI DB durumudur.
            }
            finally { OlayYazimiPatlasin = false; }

            await using var ctx = NewContext();
            var siparis = await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == siparisId);

            siparis.status.Should().Be((byte)OrderStatusEnum.Shipped,
                "olay yazilamadiysa siparis Delivered OLMAMALI - aksi halde kapida odemede "
                + "sadakat/referans HIC verilmez ve TELAFI YOLU YOKTUR (L3 BULGU-1)");
            siparis.delivered_at.Should().BeNull("teslim zamani da yazilmamali");

            (await ctx.Set<OutboxMessage>().AsNoTracking()
                .CountAsync(m => m.event_type == "PaymentConfirmed"))
                .Should().Be(0, "olay zaten yazilamadi");

            // CIFT-ANLAM KIRICI: zaman cizelgesi kaydi da GERI ALINMIS olmali - yani gercekten
            // TRANSACTION geri aldi, "hicbiri hic kosmadi" degil.
            (await ctx.Set<OrderStatusHistory>().AsNoTracking()
                .CountAsync(h => h.order_id == siparisId && h.status == (byte)OrderStatusEnum.Delivered))
                .Should().Be(0, "dort yazmanin TAMAMI geri alinmali");
        }

        // POZITIF KONTROL (vakum engeli): hata YOKKEN teslimat GERCEKTEN tamamlanir ve
        // olay YAZILIR. Bu olmadan yukaridaki test "teslimat hic calismiyor" ile de yesil kalirdi.
        [Fact]
        public async Task F5_HATA_YOKKEN_TESLIMAT_TAMAMLANIR_ve_OLAY_YAZILIR()
        {
            if (Skipped()) return;
            var (musteriId, siparisId) = await KargodaSiparisAsync();

            OlayYazimiPatlasin = false;
            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IShipmentService>()
                .TrackByOrder(siparisId, musteriId));
            sonuc.Item1.Should().Be(HttpStatusCode.OK, $"takip basarili olmali: {sonuc.Item2.Message}");

            await using var ctx = NewContext();
            var siparis = await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == siparisId);
            siparis.status.Should().Be((byte)OrderStatusEnum.Delivered, "teslimat YAZILMALI");
            siparis.delivered_at.Should().NotBeNull();

            (await ctx.Set<OutboxMessage>().AsNoTracking()
                .CountAsync(m => m.event_type == "PaymentConfirmed"))
                .Should().Be(1, "teslimat olayi TAM BIR KEZ yazilmali (>= 1 pozitif olay)");
            (await ctx.Set<OrderStatusHistory>().AsNoTracking()
                .CountAsync(h => h.order_id == siparisId && h.status == (byte)OrderStatusEnum.Delivered))
                .Should().Be(1);
        }
    }
}
