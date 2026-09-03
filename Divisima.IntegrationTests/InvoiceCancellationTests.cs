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
    // Açıklayıcı yorum: FATURA İPTALİ doğrulaması. GERÇEK SQL Server'a karşı koşar - CI'da workflow'un
    // sqlserver service container'ı (DIVISIMA_TEST_SQL), yerelde LocalDB. Gerçek EF DAL'ları + gerçek
    // InvoiceManager çalışır, sonuç DB'den TAZE context ile okunur (tracked nesne değil).
    // Doğrulanan: iptal edilmiş siparişin faturası status=3 (InvoiceStatusEnum.Cancelled) olur.
    // Aciklayici yorum: GERCEK SQL gerektirir - ci.yml adanmis adimi bu trait ile suzuyor.
    [Trait("Category", "Sql")]
    public class InvoiceCancellationTests : IAsyncLifetime
    {
        // SQL Server bağlantısı iki modda çalışır:
        //  - DIVISIMA_TEST_SQL VERİLMİŞSE (CI): SQL Server BEKLENİYOR demektir. Bağlanılamazsa testler
        //    sessizce atlanmaz, PATLAR - yoksa yanlış yapılandırılmış bir CI "yeşil" görünür ama aslında
        //    hiçbir şey doğrulamamış olurdu (atlanan test ile geçen test çıktıda ayırt edilemez).
        //  - VERİLMEMİŞSE (yerel): LocalDB denenir; yoksa testler atlanır (Windows dışı geliştirici makinesi).
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        // Veritabani adini SINIF kendisi belirler (diger tum SQL sinifiyla ayni desen).
        // ONCEDEN: ExplicitConn ham kullaniliyordu; dizgede "Database=" yoksa EnsureDeleted
        // "The database name could not be determined" ile duserdi. Yani sinif, CI'daki dizgenin
        // SEKLINE bagliydi. InitialCatalog burada set edilince bagimlilik ortadan kalkar.
        private const string DbName = "DivisimaInvoiceCancelTest";

        private static readonly string ConnStr =
            new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(ExplicitConn)
                    ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                    : ExplicitConn)
            { InitialCatalog = TestDbAdi.Cozumle(DbName) }.ConnectionString;

        private bool _sqlAvailable;

        private DbContextOptions<DivisimaDbContext> Options =>
            new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options;

        private DivisimaDbContext NewContext() => new DivisimaDbContext(Options);

        // e-Fatura sağlayıcısı sahtesi. ARTIK CancelForOrder'da KULLANILIYOR: sağlayıcıya
        // gönderilmiş (provider_invoice_id dolu) bir fatura iptal edilirken önce burası çağrılır.
        // CancelSucceeds ile iki yol da pinlenebilir: sağlayıcı kabul ederse yerel iptal yazılır,
        // reddederse fatura Cancelled İŞARETLENMEZ.
        private sealed class FakeEInvoiceProvider : IEInvoiceProvider
        {
            public bool CancelSucceeds { get; init; } = true;
            public int CancelCallCount { get; private set; }
            public string? LastCancelledProviderId { get; private set; }

            public Task<EInvoiceResult> SendInvoiceAsync(EInvoiceRequest request) =>
                Task.FromResult(new EInvoiceResult { Success = false, ErrorMessage = "test" });

            public Task<EInvoiceResult> CancelInvoiceAsync(string providerInvoiceId, string reason)
            {
                CancelCallCount++;
                LastCancelledProviderId = providerInvoiceId;
                return Task.FromResult(CancelSucceeds
                    ? new EInvoiceResult { Success = true, ProviderInvoiceId = providerInvoiceId }
                    : new EInvoiceResult { Success = false, ErrorMessage = "saglayici iptali reddetti (test)" });
            }
        }

        private InvoiceManager NewManager(DivisimaDbContext ctx, FakeEInvoiceProvider? provider = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            return new InvoiceManager(
                new EfInvoiceDal(ctx), new EfOrderDal(ctx), new EfOrderItemDal(ctx),
                new EfProductDal(ctx), provider ?? new FakeEInvoiceProvider(), config,
                new EfInvoiceItemDal(ctx), new EfCategoryDal(ctx));
        }

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
                // CI: bağlantı AÇIKÇA verildi ama SQL Server'a ulaşılamadı -> sessizce atlama, PATLA.
                // (Aksi halde yanlış yapılandırılmış CI hiçbir şey doğrulamadan yeşil görünürdü.)
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak SQL Server'a baglanilamadi - fatura iptali testleri " +
                    "ATLANMAMALI. Service container saglikli mi kontrol edin. Hedef: " + ConnStr, ex);
            }
            catch
            {
                // Yerel makine (bağlantı verilmemiş): SQL yok -> testler atlanır (Skipped() koruması).
                _sqlAvailable = false;
            }
        }

        public async Task DisposeAsync()
        {
            if (!_sqlAvailable) return;
            try
            {
                await using var ctx = NewContext();
                await TestDbKurulum.SilAsync(ctx.Database);
            }
            catch { /* temizlik best-effort */ }
        }

        // SQL yoksa test gövdesi çalıştırılmaz (yalnız yerel; CIda yukarıdaki guard patlar).
        private bool Skipped() => !_sqlAvailable;

        // Sipariş + faturayı kur; faturanın başlangıç durumu parametrik.
        private async Task<(int orderId, int invoiceId)> SeedAsync(byte orderStatus, byte? invoiceStatus, string? providerInvoiceId = null)
        {
            await using var ctx = NewContext();
            // orders -> customers FK'si var: önce müşteri lazım.
            var customer = new Customer
            {
                name = "Test Musteri",
                email = $"test-{Guid.NewGuid():N}@divisima.test",
                phone = "05000000000",
                password_salt = new byte[] { 1, 2, 3 },
                password_hash = new byte[] { 4, 5, 6 },
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(customer);
            await ctx.SaveChangesAsync();

            var order = new Order
            {
                customer_id = customer.id,
                order_number = $"ORD-{Guid.NewGuid():N}".Substring(0, 18),
                status = orderStatus,
                subtotal = 100m,
                total_price = 120m,
                currency = "TRY",
                created_at = DateTime.Now
            };
            ctx.Set<Order>().Add(order);
            await ctx.SaveChangesAsync();

            int invoiceId = 0;
            if (invoiceStatus.HasValue)
            {
                var invoice = new Invoice
                {
                    order_id = order.id,
                    customer_id = customer.id,
                    invoice_number = $"DIV-TEST-{order.id:D6}",
                    invoice_type = (byte)InvoiceTypeEnum.Individual,
                    subtotal = 100m,
                    tax_rate = 0.20m,
                    tax_amount = 20m,
                    total = 120m,
                    provider_invoice_id = providerInvoiceId,   // dolu ise: fatura GERCEKTEN saglayiciya gitmis
                    status = invoiceStatus.Value,
                    created_at = DateTime.Now
                };
                ctx.Set<Invoice>().Add(invoice);
                await ctx.SaveChangesAsync();
                invoiceId = invoice.id;
            }
            return (order.id, invoiceId);
        }

        private async Task<byte> ReadInvoiceStatusAsync(int invoiceId)
        {
            // TAZE context - önceki context'in izlediği nesne değil, DB'deki GERÇEK satır okunur.
            await using var ctx = NewContext();
            var row = await ctx.Set<Invoice>().AsNoTracking().SingleAsync(i => i.id == invoiceId);
            return row.status;
        }

        [Fact]
        public async Task CancelForOrder_IptalEdilenSiparis_FaturaStatus3Olur()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Sent);

            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(1, "başlangıçta fatura Sent olmalı");

            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            (await ReadInvoiceStatusAsync(invoiceId))
                .Should().Be((byte)InvoiceStatusEnum.Cancelled)
                .And.Be(3, "InvoiceStatusEnum.Cancelled = 3 DB'ye yazılmalı");
        }

        [Fact]
        public async Task CancelForOrder_IkinciCagri_Idempotent()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Approved);

            await using (var ctx1 = NewContext())
                await NewManager(ctx1).CancelForOrder(orderId);

            await using var ctx2 = NewContext();
            var (code, result) = await NewManager(ctx2).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Fatura zaten iptal edilmiş.");
            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be(3);
        }

        [Fact]
        public async Task CancelForOrder_SiparisIptalDegilse_BadRequest_FaturaDokunulmaz()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Confirmed, (byte)InvoiceStatusEnum.Sent);

            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.BadRequest);
            result.Success.Should().BeFalse();
            (await ReadInvoiceStatusAsync(invoiceId))
                .Should().Be(1, "aktif siparişin faturası iptal EDİLMEMELİ");
        }

        [Fact]
        public async Task CancelForOrder_FaturaYoksa_BasariliNoOp()
        {
            if (Skipped()) return;   // SQL Server yok
            var (orderId, _) = await SeedAsync((byte)OrderStatusEnum.Cancelled, null);

            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(orderId);

            code.Should().Be(HttpStatusCode.OK);
            result.Success.Should().BeTrue();
            result.Message.Should().Be("İptal edilecek fatura bulunmuyor.");
        }

        [Fact]
        public async Task CancelForOrder_SiparisYoksa_NotFound()
        {
            if (Skipped()) return;   // SQL Server yok
            await using var ctx = NewContext();
            var (code, result) = await NewManager(ctx).CancelForOrder(999999);

            code.Should().Be(HttpStatusCode.NotFound);
            result.Success.Should().BeFalse();
        }

        // SPRINT 3 - SAGLAYICI IPTALI.
        // Fatura GERCEKTEN gonderilmisse (provider_invoice_id dolu) yerel iptalden ONCE
        // saglayici cagrilir. Oncesinde bu cagri HIC yoktu: siparis iptal ediliyor, fatura
        // yerelde Cancelled oluyor, GIB tarafinda GECERLI kaliyordu.
        [Fact]
        public async Task CancelForOrder_GonderilmisFatura_SaglayiciIptaliCAGRILIR_YerelDeIptalOlur()
        {
            if (Skipped()) return;
            const string providerId = "GIB-REF-12345";
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Sent, providerId);

            var provider = new FakeEInvoiceProvider { CancelSucceeds = true };
            await using (var ctx = NewContext())
            {
                var (code, result) = await NewManager(ctx, provider).CancelForOrder(orderId);
                code.Should().Be(HttpStatusCode.OK, $"saglayici kabul etti: {result.Message}");
                result.Success.Should().BeTrue();
            }

            provider.CancelCallCount.Should().Be(1, "saglayici iptali TAM BIR kez cagrilmali");
            provider.LastCancelledProviderId.Should().Be(providerId, "saglayiciya dogru referans gonderilmeli");
            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be((byte)InvoiceStatusEnum.Cancelled,
                "saglayici kabul edince yerel iptal de yazilmali");
        }

        // SAGLAYICI REDDEDERSE fatura Cancelled ISARETLENMEZ. Aksi halde magazanin kaydinda
        // "iptal", vergi idaresinde GECERLI fatura kalir ve bu uyumsuzluk sessizce buyur.
        [Fact]
        public async Task CancelForOrder_SaglayiciREDDEDERSE_Fatura_CancelledISARETLENMEZ()
        {
            if (Skipped()) return;
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Sent, "GIB-REF-99999");

            var provider = new FakeEInvoiceProvider { CancelSucceeds = false };
            await using (var ctx = NewContext())
            {
                var (code, result) = await NewManager(ctx, provider).CancelForOrder(orderId);
                code.Should().Be(HttpStatusCode.BadGateway, "saglayici reddi yukari bildirilmeli");
                result.Success.Should().BeFalse();
                result.Message.Should().Contain("iptal edilemedi", "hata mesaji sebebi soylemeli");
            }

            provider.CancelCallCount.Should().Be(1);
            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be((byte)InvoiceStatusEnum.Sent,
                "saglayici reddedince fatura durumu DEGISMEMELI - yarim iptal olmaz");
        }

        // CIFT-ANLAM KIRICI: saglayiciya HIC gitmemis fatura (provider_invoice_id bos) icin
        // saglayici cagrilmaz ve yerel iptal dogrudan yazilir. Yani ustteki iki test
        // "her fatura icin saglayici cagriliyor" yanilgisini disarida birakir.
        [Fact]
        public async Task CancelForOrder_GonderilmemisFatura_SaglayiciCAGRILMAZ_YerelIptalYazilir()
        {
            if (Skipped()) return;
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Draft, providerInvoiceId: null);

            var provider = new FakeEInvoiceProvider { CancelSucceeds = false };   // cagrilirsa test kirmizi olur
            await using (var ctx = NewContext())
            {
                var (code, result) = await NewManager(ctx, provider).CancelForOrder(orderId);
                code.Should().Be(HttpStatusCode.OK, $"gonderilmemis fatura icin iptal dogrudan yazilmali: {result.Message}");
                result.Success.Should().BeTrue();
            }

            provider.CancelCallCount.Should().Be(0, "saglayiciya hic gitmemis fatura icin iptal cagrisi YAPILMAMALI");
            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be((byte)InvoiceStatusEnum.Cancelled);
        }

        // ══ DALGA-2-FIX - IPTAL YAN ETKISI SESSIZ DUSEMEZ ════════════════════════════════════
        //
        // OLCULEN ARTIK (Dalga 2): dev veritabaninda iptal edilmis YEDI siparisin faturasi hala
        // `Sent`. Bugunku kod uc iptal yolunun ucunde de `CancelForOrder` cagiriyor, yani o
        // satirlar tarihsel artik. AMA yol BEST-EFFORT: yukaridaki
        // `CancelForOrder_SaglayiciREDDEDERSE_...` pininin olctugu durumda (saglayici GIB iptalini
        // reddediyor) siparis Cancelled olur, fatura Sent KALIR ve bunu goren TEK sey bir LOG
        // SATIRIDIR. Yani ayni tablo URETIMDE yeniden olusabilir ve kimse fark etmez.
        //
        // GUARD: hata artik siparis ZAMAN CIZELGESINE de "KRITIK" notu olarak duser (H53'teki
        // "para iadesi BASARISIZ" kalibinin aynisi) - operasyonun gorebilecegi bir kanal.
        private OrderConfirmationManager NewConfirmationManager(DivisimaDbContext ctx, FakeEInvoiceProvider provider) =>
            new OrderConfirmationManager(
                NewManager(ctx, provider),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<OrderConfirmationManager>.Instance,
                new OrderStatusHistoryManager(new EfOrderStatusHistoryDal(ctx), new EfOrderDal(ctx)));

        [Fact]
        public async Task IptalYanEtkisi_FATURA_IPTALI_BASARISIZSA_ZamanCizelgesine_KRITIK_Notu_Duser()
        {
            if (Skipped()) return;
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Sent, "GIB-REF-77777");

            await using (var ctx = NewContext())
                await NewConfirmationManager(ctx, new FakeEInvoiceProvider { CancelSucceeds = false })
                    .ApplyCancelledSideEffectsAsync(orderId);

            // Fatura yine Sent KALIR (yarim iptal olmaz) - ama artik SESSIZ degil.
            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be((byte)InvoiceStatusEnum.Sent);

            await using (var ctx = NewContext())
            {
                var notlar = await ctx.Set<OrderStatusHistory>().AsNoTracking()
                    .Where(h => h.order_id == orderId).Select(h => h.note).ToListAsync();
                notlar.Should().Contain(n => n != null && n.Contains("KRİTİK") && n.Contains("FATURASI İPTAL EDİLEMEDİ"),
                    "iptal edilmis ama faturasi acikta kalmis siparis OPERATORE GORUNUR olmali - " +
                    "tek kanal log satiri olsaydi bu durum aylarca fark edilmezdi (Dalga 2'de bulunan " +
                    "yedi artik satirin uretimdeki karsiligi tam olarak budur)");
            }
        }

        // VAKUM KIRICI: basarili iptalde KRITIK notu DUSMEZ. Bu olmadan "her cagride not yazan"
        // bir uygulama da ustteki testi gecerdi ve zaman cizelgesi gurultuye bogulurdu.
        [Fact]
        public async Task IptalYanEtkisi_BASARILIYSA_KRITIK_Notu_DUSMEZ_FaturaIptalEdilir()
        {
            if (Skipped()) return;
            var (orderId, invoiceId) = await SeedAsync(
                (byte)OrderStatusEnum.Cancelled, (byte)InvoiceStatusEnum.Sent, "GIB-REF-88888");

            await using (var ctx = NewContext())
                await NewConfirmationManager(ctx, new FakeEInvoiceProvider { CancelSucceeds = true })
                    .ApplyCancelledSideEffectsAsync(orderId);

            (await ReadInvoiceStatusAsync(invoiceId)).Should().Be((byte)InvoiceStatusEnum.Cancelled,
                "saglayici kabul edince fatura GERCEKTEN iptal edilmeli");

            await using (var ctx = NewContext())
            {
                var notlar = await ctx.Set<OrderStatusHistory>().AsNoTracking()
                    .Where(h => h.order_id == orderId).Select(h => h.note).ToListAsync();
                notlar.Should().NotContain(n => n != null && n.Contains("KRİTİK"),
                    "basarili iptalde KRITIK notu YAZILMAMALI");
            }
        }
    }
}
