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
    // D5-4/5 - CLAIM-BEFORE-SEND
    //
    // Iki ayri yerde ayni desen var: is once ATOMIK olarak sahiplenilir, sonra yan etki
    // (mail/olay) uretilir. Sahiplenme atomik olmazsa ayni mesaj/abonelik iki kez islenir.
    //   - EfOutboxMessageDal.TryClaimAsync            : status 0 -> 3, ExecuteUpdate, etkilenen satir sayisi
    //   - EfStockNotificationRequestDal.TryClaimForNotificationAsync : is_notified false -> true
    // Ikisi de tek SQL UPDATE ile kosullu yaziyor; testler bunu paralel cagrilarla zorluyor.
    [Trait("Category", "Sql")]
    public class ClaimBeforeSendTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaClaimRaceTest";
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

        private sealed class ClaimFactory : WebApplicationFactory<Program>
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

        private ClaimFactory? _factory;
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
                _factory = new ClaimFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak claim yarisi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        [Fact]
        public async Task Outbox_SekizParalelIsleyici_AyniMesaji_TAM_BIR_KEZ_Sahiplenir()
        {
            if (Skipped()) return;
            const int workers = 8;

            int messageId;
            await using (var ctx = NewContext())
            {
                var msg = new OutboxMessage
                {
                    event_type = "OrderPlaced",
                    payload = "{\"orderId\":1}",
                    status = 0,                 // Beklemede
                    retry_count = 0,
                    created_at = DateTime.Now
                };
                ctx.Set<OutboxMessage>().Add(msg);
                await ctx.SaveChangesAsync();
                messageId = msg.id;
            }

            // Her isleyici KENDI scope unda (kendi DbContext i) ayni mesaji sahiplenmeye calisir.
            var affected = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ =>
                WithScopeAsync(sp => sp.GetRequiredService<IOutboxMessageDal>().TryClaimAsync(messageId))));

            affected.Count(a => a > 0).Should().Be(1,
                $"kosullu UPDATE yalniz BIR isleyicide satir etkilemeli. Sonuclar: {string.Join(",", affected)}");
            affected.Sum().Should().Be(1, "toplam etkilenen satir sayisi 1 olmali - cift islem yok");

            await using (var ctx = NewContext())
            {
                var son = await ctx.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.id == messageId);
                son.status.Should().Be(3, "mesaj Processing (3) durumuna gecmeli");
                son.processed_at.Should().NotBeNull("sahiplenme zamani yazilmali");
            }
        }

        [Fact]
        public async Task StokBildirimi_SekizParalelCagri_AyniAboneligi_TAM_BIR_KEZ_Sahiplenir()
        {
            if (Skipped()) return;
            const int workers = 8;

            int requestId;
            await using (var ctx = NewContext())
            {
                var urunId = await TestVeriKurgusu.GercekUrunAsync(ctx);
                var req = new StockNotificationRequest
                {
                    // SPRINT 8 MADDE 10: unsubscribe_token artik ZORUNLU (NOT NULL). Tokensiz bir satir
                    // hicbir zaman abonelikten cikarilamaz - o yuzden kolon opsiyonel BIRAKILMADI ve
                    // dogrudan insert yapan test kurgulari da uretimle ayni sozlesmeye uyuyor.
                    unsubscribe_token = Divisima.Core.Utilities.Security.UnsubscribeToken.Yeni(),
                    // D-SEMA-FIX: product_id artik GERCEK bir urune isaret etmeli
                    // (FK_stock_notification_requests_product_id). Uydurma id uretimde olusamaz.
                    product_id = urunId,
                    size = "M",
                    email = $"stok-{Guid.NewGuid():N}@divisima.test",
                    is_notified = false,
                    created_at = DateTime.Now
                };
                ctx.Set<StockNotificationRequest>().Add(req);
                await ctx.SaveChangesAsync();
                requestId = req.id;
            }

            var claims = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ =>
                WithScopeAsync(sp => sp.GetRequiredService<IStockNotificationRequestDal>()
                    .TryClaimForNotificationAsync(requestId))));

            claims.Count(c => c).Should().Be(1,
                $"ayni aboneye YALNIZ bir mail hakki verilmeli. Sonuclar: {string.Join(",", claims)}");
            claims.Count(c => !c).Should().Be(workers - 1, "digerleri sahiplenemeden donmeli");

            await using (var ctx = NewContext())
            {
                (await ctx.Set<StockNotificationRequest>().AsNoTracking().SingleAsync(x => x.id == requestId))
                    .is_notified.Should().BeTrue("kayit bildirildi olarak isaretlenmeli");
            }
        }

        // D5b - FIYAT DUSUSU ABONELIGI: stok bildirimiyle AYNI claim deseni. Ayni abonelige
        // eszamanli iki fiyat guncellemesi gelirse yalniz biri mail hakkini almali.
        [Fact]
        public async Task FiyatDususu_SekizParalelCagri_AyniAboneligi_TAM_BIR_KEZ_Sahiplenir()
        {
            if (Skipped()) return;
            const int workers = 8;

            int subId;
            await using (var ctx = NewContext())
            {
                var urunId = await TestVeriKurgusu.GercekUrunAsync(ctx);
                var sub = new PriceDropSubscription
                {
                    // SPRINT 8 MADDE 10: unsubscribe_token artik ZORUNLU (NOT NULL). Tokensiz bir satir
                    // hicbir zaman abonelikten cikarilamaz - o yuzden kolon opsiyonel BIRAKILMADI ve
                    // dogrudan insert yapan test kurgulari da uretimle ayni sozlesmeye uyuyor.
                    unsubscribe_token = Divisima.Core.Utilities.Security.UnsubscribeToken.Yeni(),
                    // D-SEMA-FIX: product_id artik GERCEK bir urune isaret etmeli
                    // (FK_price_drop_subscriptions_product_id). Uydurma id uretimde olusamaz.
                    product_id = urunId,
                    email = $"fiyat-{Guid.NewGuid():N}@divisima.test",
                    subscribed_price = 250m,
                    is_notified = false,
                    created_at = DateTime.Now
                };
                ctx.Set<PriceDropSubscription>().Add(sub);
                await ctx.SaveChangesAsync();
                subId = sub.id;
            }

            var claims = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ =>
                WithScopeAsync(sp => sp.GetRequiredService<IPriceDropSubscriptionDal>()
                    .TryClaimForNotificationAsync(subId))));

            claims.Count(c => c).Should().Be(1,
                $"ayni aboneye YALNIZ bir mail hakki verilmeli. Sonuclar: {string.Join(",", claims)}");
            claims.Count(c => !c).Should().Be(workers - 1, "digerleri sahiplenemeden donmeli");

            await using (var ctx = NewContext())
            {
                (await ctx.Set<PriceDropSubscription>().AsNoTracking().SingleAsync(x => x.id == subId))
                    .is_notified.Should().BeTrue("kayit bildirildi olarak isaretlenmeli");
            }
        }

        // Filtreli tekil indeks: (product_id, size, email) UNIQUE WHERE is_notified = 0.
        // Ayni abone AYNI urune iki kez bekleyen kayit acamaz; bildirim gittikten SONRA
        // (is_notified = 1) yeniden abone olabilir - filtrenin varlik sebebi bu.
        [Fact]
        public async Task StokBildirimi_FiltreliUnique_IkinciBEKLEYEN_Kaydi_Engeller()
        {
            if (Skipped()) return;
            var email = $"abone-{Guid.NewGuid():N}@divisima.test";

            // D-SEMA-FIX: urun id ARTIK PARAMETRE - kayitlarin GERCEK bir urune baglanmasi
            // gerekiyor (FK_stock_notification_requests_product_id).
            static StockNotificationRequest Kayit(int urunId, string email, bool bildirildi) => new()
            {
                product_id = urunId,
                size = "L",
                email = email,
                is_notified = bildirildi,
                created_at = DateTime.Now,
                notified_at = bildirildi ? DateTime.Now : null,
                // SPRINT 8 MADDE 10: unsubscribe_token ZORUNLU (NOT NULL) ve UNIQUE. Her cagri
                // KENDI jetonunu uretmeli - sabit bir deger verilseydi ikinci satir bu testin
                // OLCTUGU filtreli-unique yerine JETON unique'ine takilir ve test yanlis sebepten
                // yesil/kirmizi olurdu.
                unsubscribe_token = Divisima.Core.Utilities.Security.UnsubscribeToken.Yeni()
            };

            int urunId;
            await using (var kur = NewContext())
                urunId = await TestVeriKurgusu.GercekUrunAsync(kur);

            int ilkId;
            await using (var ctx = NewContext())
            {
                var ilk = Kayit(urunId, email, false);
                ctx.Set<StockNotificationRequest>().Add(ilk);
                await ctx.SaveChangesAsync();
                ilkId = ilk.id;
            }

            var ikinciDeneme = async () =>
            {
                await using var ctx = NewContext();
                ctx.Set<StockNotificationRequest>().Add(Kayit(urunId, email, false));
                await ctx.SaveChangesAsync();
            };
            await ikinciDeneme.Should().ThrowAsync<DbUpdateException>(
                "ayni abone icin ikinci BEKLEYEN kayit acilamaz");

            // Bildirim gittikten sonra yeniden abonelik SERBEST (filtre bunun icin var).
            await using (var ctx = NewContext())
            {
                var ilk = await ctx.Set<StockNotificationRequest>().SingleAsync(x => x.id == ilkId);
                ilk.is_notified = true;
                ilk.notified_at = DateTime.Now;
                await ctx.SaveChangesAsync();
            }
            await using (var ctx = NewContext())
            {
                ctx.Set<StockNotificationRequest>().Add(Kayit(urunId, email, false));
                await ctx.SaveChangesAsync();
            }

            await using (var son = NewContext())
            {
                (await son.Set<StockNotificationRequest>().CountAsync(x => x.email == email && !x.is_notified))
                    .Should().Be(1, "her an yalniz TEK bekleyen kayit olabilir");
                (await son.Set<StockNotificationRequest>().CountAsync(x => x.email == email))
                    .Should().Be(2, "bildirilmis kayit korunur - toplam iki satir");
            }
        }
    }
}
