using System.Text.Json;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;
using Divisima.DataAccess.Concrete.Context;
using Divisima.DataAccess.Concrete.EntityFramework;
using Divisima.Entity.Entities;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 3 - MAIL TESLIM SOZLESMESI + OUTBOX UCTAN UCA
    //
    // SmtpMailService artik gercek gonderim yapiyor ve BASARISIZLIKTA ISTISNA FIRLATIYOR.
    // Firlatmak bir tercih degil, SOZLESME: cagiranlarin telafi mantigi ancak istisna gorurse
    // calisir. Onceki halinde servis yalniz log basip basari donuyordu; OutboxProcessor mesaji
    // "islendi" isaretliyor, StockNotificationManager claim'i geri almiyordu - yani hic
    // gonderilmemis bildirimler kalici olarak "gonderildi" sayiliyordu.
    //
    // Burada SMTP'nin kendisi degil, o SOZLESMENIN cagiranlar tarafindan dogru okundugu olculur:
    // sahte bir IMailService ile basari ve hata yollari ayri ayri surulur.
    [Trait("Category", "Sql")]
    public class MailDeliveryContractTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaMailContractTest";
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
                    "DIVISIMA_TEST_SQL verildi ancak mail sozlesmesi testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
            }
            catch { _sqlAvailable = false; }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try { await using var ctx = NewContext(); await ctx.Database.EnsureDeletedAsync(); } catch { }
        }

        private bool Skipped() => !_sqlAvailable;

        // SAHTE SMTP: gercek sunucuya baglanmadan sozlesmenin iki dalini de surer.
        private sealed class FakeMailService : IMailService
        {
            public bool ThrowOnSend { get; init; }
            public int SendCount { get; private set; }
            public string? LastTo { get; private set; }

            public Task SendAsync(MailMessageDto message)
            {
                SendCount++;
                LastTo = message.To;
                if (ThrowOnSend)
                    throw new InvalidOperationException("SMTP sunucusuna ulasilamadi (sahte)");
                return Task.CompletedTask;
            }
        }

        // OrderPlaced yolunu bu testte kullanmiyoruz; ctor'u doyurmak icin sessiz sahte.
        private sealed class NoopOrderPlacedPublisher : IOrderPlacedEventPublisher
        {
            public Task PublishAsync(OrderPlacedEvent evt) => Task.CompletedTask;
        }

        // SPRINT 8 MADDE 3: isleyiciye odeme-onayi yan etkileri ve zaman cizelgesi bagimliligi eklendi.
        // Bu sinif E-POSTA mesajlarini olcuyor; odeme dali CAGRILMAZ, cagrilirsa GURULTULU duser
        // (sessiz bir sahte donmek, testin yanlis yolu olctugunu gizlerdi).
        // SPRINT 8 MADDE 3: isleyici odeme dalinda MESAJ BASINA AYRI SCOPE aciyor. Bu sinif
        // E-POSTA mesajlarini olcuyor; odeme dali CAGRILMAZ, dolayisiyla scope da hic istenmez.
        // Istenirse GURULTULU duser - sessiz bir sahte, testin yanlis yolu olctugunu gizlerdi.
        private sealed class CagrilmayanScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
                => throw new NotSupportedException("Mail sozlesme testlerinde odeme dali kullanilmaz.");
        }

        private sealed class CagrilmayanZamanCizelgesi : Divisima.Bussiness.Abstract.IOrderStatusHistoryService
        {
            public Task RecordAsync(int orderId, byte status, string note)
                => throw new NotSupportedException("Mail sozlesme testlerinde kullanilmaz.");
            public Task<(System.Net.HttpStatusCode, Divisima.Core.Utilities.Results.Result)> GetTimeline(int orderId, int customerId)
                => throw new NotSupportedException("Mail sozlesme testlerinde kullanilmaz.");
        }

        private static OutboxProcessor NewProcessor(DivisimaDbContext ctx, IMailService mail) =>
            new OutboxProcessor(new EfOutboxMessageDal(ctx), new NoopOrderPlacedPublisher(), mail,
                new CagrilmayanZamanCizelgesi(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxProcessor>.Instance,
                new CagrilmayanScopeFactory());

        private static async Task<int> SeedEmailMessageAsync()
        {
            await using var ctx = NewContext();
            var msg = new OutboxMessage
            {
                event_type = "EmailNotification",
                payload = JsonSerializer.Serialize(new MailMessageDto
                {
                    To = $"alici-{Guid.NewGuid():N}@divisima.test",
                    Subject = "Test bildirimi",
                    Body = "govde"
                }),
                status = 0,               // Beklemede
                retry_count = 0,
                created_at = DateTime.Now
            };
            ctx.Set<OutboxMessage>().Add(msg);
            await ctx.SaveChangesAsync();
            return msg.id;
        }

        private static async Task<OutboxMessage> ReadMessageAsync(int id)
        {
            await using var ctx = NewContext();
            return await ctx.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.id == id);
        }

        [Fact]
        public async Task Outbox_MailBASARILI_Mesaj_Islendi_Isaretlenir()
        {
            if (Skipped()) return;
            var messageId = await SeedEmailMessageAsync();
            var mail = new FakeMailService { ThrowOnSend = false };

            await using (var ctx = NewContext())
                await NewProcessor(ctx, mail).ProcessPendingAsync();

            // VAKUM KIRICI: mail GERCEKTEN gonderildi (islem hic calismadi da olabilirdi).
            mail.SendCount.Should().Be(1, "bekleyen mesaj icin tam bir gonderim yapilmali");

            var son = await ReadMessageAsync(messageId);
            son.status.Should().Be(1, "basarili gonderim sonrasi mesaj Processed (1) olmali");
            son.processed_at.Should().NotBeNull();
            son.error.Should().BeNull();
            son.retry_count.Should().Be(0);
        }

        // ASIL SINAV: mail servisi ISTISNA firlatinca mesaj "islendi" SAYILMAMALI.
        // Eski (log-only) serviste bu dal HIC calismiyordu - her mesaj basarili sayiliyordu.
        [Fact]
        public async Task Outbox_MailHATA_VERIRSE_Mesaj_IslendiSAYILMAZ_YenidenDenenir()
        {
            if (Skipped()) return;
            var messageId = await SeedEmailMessageAsync();
            var mail = new FakeMailService { ThrowOnSend = true };

            await using (var ctx = NewContext())
                await NewProcessor(ctx, mail).ProcessPendingAsync();

            mail.SendCount.Should().Be(1, "gonderim denenmis olmali");

            var son = await ReadMessageAsync(messageId);
            son.status.Should().NotBe(1, "hata alan mesaj Processed (1) ISARETLENMEMELI");
            son.status.Should().Be(0, "5 denemeye kadar Pending'e (0) geri donmeli - tekrar denenecek");
            son.retry_count.Should().Be(1, "deneme sayaci artmali");
            son.error.Should().NotBeNullOrWhiteSpace("hata sebebi kaydedilmeli");
            son.processed_at.Should().BeNull("islenmemis mesajda islem zamani bulunmamali");
        }

        // Crash kurtarma: Processing'de (3) takili kalmis eski mesaj yeniden Pending yapilir.
        // Bu olmadan processor cokunce mesaj sonsuza dek Processing kalirdi.
        [Fact]
        public async Task Outbox_YaridaKalmisMesaj_ReclaimIle_YenidenIslenir()
        {
            if (Skipped()) return;
            var messageId = await SeedEmailMessageAsync();

            // Mesaji "6 dakika once sahiplenilmis ama bitmemis" durumuna getir.
            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<OutboxMessage>().SingleAsync(x => x.id == messageId);
                m.status = 3;                                   // Processing
                m.processed_at = DateTime.Now.AddMinutes(-6);   // 5 dk esiginden eski
                await ctx.SaveChangesAsync();
            }

            var mail = new FakeMailService { ThrowOnSend = false };
            await using (var ctx = NewContext())
                await NewProcessor(ctx, mail).ProcessPendingAsync();

            mail.SendCount.Should().Be(1, "reclaim sonrasi mesaj yeniden islenmeli");
            (await ReadMessageAsync(messageId)).status.Should().Be(1, "ikinci turda Processed olmali");
        }

        // CIFT-ANLAM KIRICI: reclaim her Processing mesaji geri almaz - YENI sahiplenilmis
        // (esikten yeni) mesaj baska bir isleyicinin elindedir ve DOKUNULMAMALI.
        [Fact]
        public async Task Outbox_YENI_Sahiplenilmis_Mesaj_ReclaimEDILMEZ()
        {
            if (Skipped()) return;
            var messageId = await SeedEmailMessageAsync();

            await using (var ctx = NewContext())
            {
                var m = await ctx.Set<OutboxMessage>().SingleAsync(x => x.id == messageId);
                m.status = 3;
                m.processed_at = DateTime.Now.AddMinutes(-1);   // esik icinde - hala calisiyor
                await ctx.SaveChangesAsync();
            }

            var mail = new FakeMailService { ThrowOnSend = false };
            await using (var ctx = NewContext())
                await NewProcessor(ctx, mail).ProcessPendingAsync();

            mail.SendCount.Should().Be(0, "baska isleyicinin elindeki mesaj islenmemeli");
            (await ReadMessageAsync(messageId)).status.Should().Be(3, "durum Processing kalmali");
        }

        // STOK BILDIRIMI: gonderim hata verirse claim GERI ALINMALI, yoksa abone bir daha
        // hic haber alamaz (is_notified=true kalir ve filtreli indeks yeni kayda da izin verir
        // ama eski abonelik sessizce olur).
        [Fact]
        public async Task StokBildirimi_MailHATA_VERIRSE_Claim_GeriAlinir()
        {
            if (Skipped()) return;

            int productId;
            await using (var ctx = NewContext())
            {
                var cat = new Category
                {
                    name = "Mail Kategori",
                    slug = $"mail-{Guid.NewGuid():N}",
                    is_active = true,
                    created_at = DateTime.Now
                };
                ctx.Set<Category>().Add(cat);
                await ctx.SaveChangesAsync();

                var p = new Product
                {
                    name = "Mail Urun",
                    brand = "T",
                    category_id = cat.id,
                    price = 80m,
                    description = "mail testi urunu",
                    color_hex = "#0D0D0D",
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
                    email = $"abone-{Guid.NewGuid():N}@divisima.test",
                    is_notified = false,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            var mail = new FakeMailService { ThrowOnSend = true };
            await using (var ctx = NewContext())
            {
                var mgr = new Divisima.Bussiness.Concrete.StockNotificationManager(
                    new EfStockNotificationRequestDal(ctx), new EfProductDal(ctx), mail,
                    // LAUNCH-FIX A1(c): taban okuma IMailLinkBuilder'a tasindi. GERCEK uygulama veriliyor
                    // (stub degil); yapilandirma BOS oldugu icin builder null doner ve manager
                    // baglanti yerine "Hesabim > Bildirimlerim" metnini yazar - olculen sey yine gonderim.
                    new MailLinkBuilder(new ConfigurationBuilder().Build(), NullLogger<MailLinkBuilder>.Instance));
                await mgr.NotifyBackInStock(productId, "M");
            }

            mail.SendCount.Should().Be(1, "gonderim denenmis olmali");
            await using (var ctx = NewContext())
            {
                (await ctx.Set<StockNotificationRequest>().AsNoTracking()
                    .SingleAsync(x => x.product_id == productId))
                    .is_notified.Should().BeFalse(
                        "gonderim basarisizsa claim GERI ALINMALI - abone tekrar denenebilir olmali");
            }
        }
    }
}
