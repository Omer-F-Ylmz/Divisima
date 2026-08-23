using Divisima.Bussiness.Concrete;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 3 - IYS / TICARI ELEKTRONIK ILETI KAPISI
    //
    // BULUNAN DURUM: kayitta pazarlama rizasi ConsentRecord olarak YAZILIYOR (kabul de ret de),
    // ama hicbir gonderim yolu bu kaydi OKUMUYORDU - riza kaydi yalniz yazilip duruyordu.
    // AbandonedCartManager ayrica notify_email tercihine de bakmiyordu; yalniz is_active.
    //
    // Olculen sozlesme:
    //   - Marketing:Enabled KAPALI  -> pazarlama maili HIC gitmez (varsayilan bu),
    //   - ACIK + riza YOK/REDDEDILMIS -> gitmez,
    //   - ACIK + riza VAR + notify_email -> gider,
    //   - ISLEMSEL mail bayraktan ETKILENMEZ (cift-anlam kirici).
    [Trait("Category", "Sql")]
    public class MarketingGateTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaMarketingGateTest";
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

        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            try
            {
                await using var ctx = NewContext();
                await ctx.Database.EnsureDeletedAsync();
                await ctx.Database.EnsureCreatedAsync();
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak IYS kapisi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        private sealed class FakeMailService : IMailService
        {
            public int SendCount { get; private set; }
            public Task SendAsync(MailMessageDto message) { SendCount++; return Task.CompletedTask; }
        }

        private static IConfiguration Config(bool marketingEnabled) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Marketing:Enabled"] = marketingEnabled ? "true" : "false"
                })
                .Build();

        // Terk edilmis sepet + musteri + (opsiyonel) pazarlama rizasi tohumla.
        private static async Task<int> SeedAbandonedCartAsync(bool notifyEmail, bool? marketingGranted,
            bool addSecondConsentRevoking = false)
        {
            await using var ctx = NewContext();
            var c = new Customer
            {
                name = "IYS Testi",
                email = $"iys-{Guid.NewGuid():N}@divisima.test",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                notify_email = notifyEmail,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(c);
            await ctx.SaveChangesAsync();

            if (marketingGranted.HasValue)
            {
                ctx.Set<ConsentRecord>().Add(new ConsentRecord
                {
                    customer_id = c.id,
                    consent_type = "marketing",
                    document_version = "1.0",
                    granted = marketingGranted.Value,
                    created_at = DateTime.Now.AddDays(-2)
                });
                await ctx.SaveChangesAsync();
            }
            if (addSecondConsentRevoking)
            {
                // SONRADAN RET: en guncel kayit belirleyici olmali.
                ctx.Set<ConsentRecord>().Add(new ConsentRecord
                {
                    customer_id = c.id,
                    consent_type = "marketing",
                    document_version = "1.0",
                    granted = false,
                    created_at = DateTime.Now.AddDays(-1)
                });
                await ctx.SaveChangesAsync();
            }

            var cat = new Category
            {
                name = "IYS Kategori",
                slug = $"iys-{Guid.NewGuid():N}",
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(cat);
            await ctx.SaveChangesAsync();

            var p = new Product
            {
                name = "IYS Urun",
                brand = "T",
                category_id = cat.id,
                price = 100m,
                description = "iys testi urunu",
                color_hex = "#0E0E0E",
                product_type = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            // Atil sepet: son hareket 48 saat once (esik 24 saat), hatirlatma gonderilmemis.
            var cart = new Cart
            {
                customer_id = c.id,
                is_active = true,
                created_at = DateTime.Now.AddDays(-3),
                updated_at = DateTime.Now.AddHours(-48),
                reminder_sent_at = null
            };
            ctx.Set<Cart>().Add(cart);
            await ctx.SaveChangesAsync();

            ctx.Set<CartItem>().Add(new CartItem
            {
                cart_id = cart.id,
                product_id = p.id,
                size = "M",
                quantity = 1,
                is_active = true,
                created_at = DateTime.Now.AddHours(-48)
            });
            await ctx.SaveChangesAsync();
            return c.id;
        }

        private static async Task<int> RunRemindersAsync(bool marketingEnabled, FakeMailService mail)
        {
            await using var ctx = NewContext();
            var cfg = Config(marketingEnabled);
            var gate = new MarketingGate(cfg, new EfCustomerDal(ctx), new EfConsentRecordDal(ctx));
            var mgr = new AbandonedCartManager(
                new EfCartDal(ctx), new EfCartItemDal(ctx), new EfCustomerDal(ctx), mail, gate);
            return await mgr.SendReminders();
        }

        [Fact]
        public async Task Bayrak_KAPALI_PazarlamaMaili_GITMEZ()
        {
            if (Skipped()) return;
            await SeedAbandonedCartAsync(notifyEmail: true, marketingGranted: true);

            var mail = new FakeMailService();
            var sent = await RunRemindersAsync(marketingEnabled: false, mail);

            sent.Should().Be(0, "bayrak kapaliyken hic hatirlatma gonderilmemeli");
            mail.SendCount.Should().Be(0, "mail servisi hic cagrilmamali");

            // Damga da atilmamali: kisi ileride izin verirse hatirlatma hala gonderilebilmeli.
            await using var ctx = NewContext();
            (await ctx.Set<Cart>().AsNoTracking().SingleAsync()).reminder_sent_at
                .Should().BeNull("gonderilmeyen hatirlatma icin damga ATILMAMALI");
        }

        // CIFT-ANLAM KIRICI: bayrak kapaliyken ISLEMSEL mail HALA gider. Yani kapi
        // "her seyi susturan" bir salter degil, yalniz ticari iletiyi kesiyor.
        [Fact]
        public async Task Bayrak_KAPALIYKEN_ISLEMSEL_Mail_YINE_GIDER()
        {
            if (Skipped()) return;

            int productId;
            await using (var ctx = NewContext())
            {
                var cat = new Category
                {
                    name = "Islemsel Kategori",
                    slug = $"islemsel-{Guid.NewGuid():N}",
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "Islemsel Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 90m,
                    description = "islemsel test",
                    color_hex = "#0F0F0F",
                    product_type = 0,
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Products.Add(p);
                await ctx.SaveChangesAsync();
                productId = p.id;

                ctx.Set<StockNotificationRequest>().Add(new StockNotificationRequest
                {
                    // SPRINT 8 MADDE 10: unsubscribe_token artik ZORUNLU (NOT NULL). Tokensiz bir satir
                    // hicbir zaman abonelikten cikarilamaz - o yuzden kolon opsiyonel BIRAKILMADI ve
                    // dogrudan insert yapan test kurgulari da uretimle ayni sozlesmeye uyuyor.
                    unsubscribe_token = Divisima.Core.Utilities.Security.UnsubscribeToken.Yeni(),
                    product_id = productId,
                    size = "M",
                    email = $"stok-{Guid.NewGuid():N}@divisima.test",
                    is_notified = false,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            // Marketing:Enabled = false olan bir yapilandirmayla bile stokta-var bildirimi gider:
            // bu bildirim kisinin KENDI talebine verilen islemsel cevaptir, ticari ileti degil.
            var mail = new FakeMailService();
            await using (var ctx = NewContext())
            {
                var mgr = new StockNotificationManager(
                    new EfStockNotificationRequestDal(ctx), new EfProductDal(ctx), mail,
                    // LAUNCH-FIX A1(c): taban okuma IMailLinkBuilder'a tasindi. GERCEK uygulama veriliyor
                    // (stub degil); yapilandirma BOS oldugu icin builder null doner ve manager
                    // baglanti yerine "Hesabim > Bildirimlerim" metnini yazar - olculen sey yine gonderim.
                    new MailLinkBuilder(new ConfigurationBuilder().Build(), NullLogger<MailLinkBuilder>.Instance));
                await mgr.NotifyBackInStock(productId, "M");
            }

            mail.SendCount.Should().Be(1, "islemsel bildirim pazarlama bayragindan ETKILENMEMELI");
        }

        [Fact]
        public async Task Bayrak_ACIK_RizaYOKSA_Gitmez()
        {
            if (Skipped()) return;
            await SeedAbandonedCartAsync(notifyEmail: true, marketingGranted: null);   // hic riza kaydi yok

            var mail = new FakeMailService();
            var sent = await RunRemindersAsync(marketingEnabled: true, mail);

            sent.Should().Be(0, "riza kaydi olmayan kisiye ticari ileti gonderilmemeli");
            mail.SendCount.Should().Be(0);
        }

        [Fact]
        public async Task Bayrak_ACIK_RizaREDDEDILMISSE_Gitmez()
        {
            if (Skipped()) return;
            await SeedAbandonedCartAsync(notifyEmail: true, marketingGranted: false);

            var mail = new FakeMailService();
            var sent = await RunRemindersAsync(marketingEnabled: true, mail);

            sent.Should().Be(0, "pazarlama rizasi reddedilmis kisiye gonderilmemeli");
            mail.SendCount.Should().Be(0);
        }

        // EN GUNCEL kayit belirleyici: once kabul, sonra ret -> GITMEZ.
        [Fact]
        public async Task Bayrak_ACIK_SonradanREDDEDILMISSE_Gitmez()
        {
            if (Skipped()) return;
            await SeedAbandonedCartAsync(notifyEmail: true, marketingGranted: true, addSecondConsentRevoking: true);

            var mail = new FakeMailService();
            var sent = await RunRemindersAsync(marketingEnabled: true, mail);

            sent.Should().Be(0, "en guncel riza kaydi RET ise gonderilmemeli");
            mail.SendCount.Should().Be(0);
        }

        [Fact]
        public async Task Bayrak_ACIK_NotifyEmail_KAPALIYSA_Gitmez()
        {
            if (Skipped()) return;
            await SeedAbandonedCartAsync(notifyEmail: false, marketingGranted: true);

            var mail = new FakeMailService();
            var sent = await RunRemindersAsync(marketingEnabled: true, mail);

            sent.Should().Be(0, "notify_email tercihi kapaliysa gonderilmemeli");
            mail.SendCount.Should().Be(0);
        }

        // VAKUM KIRICI: butun kosullar saglaninca mail GERCEKTEN gidiyor. Bu olmadan ustteki
        // bes test "hicbir sey gonderilmiyor" durumunda da yesil kalirdi.
        [Fact]
        public async Task Bayrak_ACIK_RizaVAR_VeTercihACIK_GONDERILIR()
        {
            if (Skipped()) return;
            await SeedAbandonedCartAsync(notifyEmail: true, marketingGranted: true);

            var mail = new FakeMailService();
            var sent = await RunRemindersAsync(marketingEnabled: true, mail);

            sent.Should().Be(1, "tum kosullar saglaninca hatirlatma gonderilmeli");
            mail.SendCount.Should().Be(1);

            await using var ctx = NewContext();
            (await ctx.Set<Cart>().AsNoTracking().SingleAsync()).reminder_sent_at
                .Should().NotBeNull("gonderilen hatirlatma damgalanmali (tekrar gonderilmesin)");
        }
    }
}
