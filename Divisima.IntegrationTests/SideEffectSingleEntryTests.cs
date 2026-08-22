using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Concrete.Context;
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
    // ══ DALGA-2-FIX (B10) - ONAY YAN ETKILERININ TEK GIRIS NOKTASI ═════════════════════════════
    //
    // OLCULEN SORUN (Dalga 2, gercek dev veritabani): dort yan etkiden UCU yalnizca KART yolunda
    // calisiyordu. Kok sebep, `ApplyConfirmedSideEffectsAsync`in SADECE faturayi kesmesi; sadakat,
    // referans odulu ve kupon defteri ise yalnizca `IyzicoPaymentManager`in yazdigi
    // "PaymentConfirmed" outbox mesajina bagliydi. Kart disi UC onay yolu o mesaji hic yazmiyordu.
    //
    // CANLI TABLO (olcum, tahmin degil):
    //     ptype=0 (kart)  siparis 10, 31, 33, 34   FATURA=1  SADAKAT=1   -> 4/4
    //     ptype=1 (COD)   siparis 12, 13, 32       FATURA=1  SADAKAT=0   -> 0/3
    //     siparis #13 kuponluydu -> coupon_usages 0 satir, coupons.used_count 0
    //
    // URETIMDEKI ANLAMI: kapida odeyen musteri sadakat puani KAZANMIYOR, davet eden referans
    // kredisini ALAMIYOR, kupon defteri BOS kaliyor (admin panelinde kupon "hic kullanilmamis").
    //
    // DUZELTME: uc onay yolunun ucu de olayi KENDI TRANSACTION'I ICINDE yaziyor; dort adim TEK
    // uygulayicida (PaymentConfirmedSideEffects) kaliyor.
    //
    // NEDEN "Confirmed" DOGRU TETIK NOKTASI (kullanicinin (iii) sarti):
    //   - Kapida odemede para TESLIMATTA alinir; siparisin `Confirmed` olmasi MAGAZANIN kabulunu
    //     ifade eder - siparis gecerlidir, hazirlanacaktir, faturasi kesilecektir. Zaten fatura da
    //     BUGUNE KADAR tam bu noktada kesiliyordu; sadakat/referans/kupon defterini AYNI noktaya
    //     baglamak yeni bir kapi acmiyor, var olan kapiyi TUTARLI hale getiriyor.
    //   - Havale/EFT'de `ConfirmManualPayment` zaten "para hesaba GECTI" beyanidir
    //     (`is_online_payment_done = true` orada yazilir).
    //   - Admin durum degisikliginde onay ADMININ karari; ayrica `OrderStatusMachine` yalnizca
    //     Pending -> Confirmed gecisine izin verir.
    //   - Siparis sonradan iptal edilirse puan ZATEN geri alinir (`ReverseForOrder`, farming
    //     engeli) ve fatura iptal edilir - yani "onaydan sonra vazgecilirse" yolu kapali degil.
    [Trait("Category", "Sql")]
    public class SideEffectSingleEntryTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaSideEffectEntryTest";
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

        private sealed class EntryFactory : WebApplicationFactory<Program>
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

        private EntryFactory? _factory;
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
                _factory = new EntryFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak yan etki testleri icin ortam hazirlanamadi - ATLANMAMALI.", ex);
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

        // Outbox'i GERCEK isleyiciyle bosaltir. Uretimde bunu Cron.Minutely yapiyor; testte
        // beklemek yerine ayni isleyici dogrudan kosturulur.
        private async Task OutboxBosaltAsync()
        {
            using var scope = _factory!.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ProcessPendingAsync();
        }

        private sealed record Kurulum(int MusteriId, int DavetEdenId, int UrunId, int KuponId, string KuponKodu);

        // DORT ADIMIN DA ANLAMLI oldugu bir kurulum: davet edilmis musteri (referans odulu),
        // kuponlu siparis (kupon defteri), gercek urun + stok (rezervasyon/onay), fatura.
        private static async Task<Kurulum> KurulumYapAsync(decimal fiyat = 100m, int stok = 10)
        {
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
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

            var davetEden = Musteri("Daveteden");
            ctx.Set<Customer>().Add(davetEden);
            await ctx.SaveChangesAsync();

            var musteri = Musteri("Alici");
            musteri.referred_by = davetEden.id;
            ctx.Set<Customer>().Add(musteri);
            await ctx.SaveChangesAsync();

            var kategori = new Category
            {
                name = "Yan Etki Kategori " + damga,
                slug = "yanetki-" + damga.ToLowerInvariant(),
                display_order = 1,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "Yan Etki Urunu " + damga,
                brand = "Divisima",
                category_id = kategori.id,
                price = fiyat,
                description = "Yan etki pini icin urun.",   // ZORUNLU alan (CLAUDE.md tuzagi)
                color_hex = "#112233",                      // ZORUNLU alan
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
                stock_quantity = stok,
                reserved_quantity = 0,
                is_active = true,
                created_at = DateTime.Now
            });

            // KOD KANONIK YAZILIR: GetByCodeAsync artik KimlikDizgesi.KanonikKod ile ariyor
            // (dalga-1-fix B2). Salt ASCII buyuk harf secildi ki kurulum o donusumden bagimsiz olsun.
            var kupon = new Coupon
            {
                code = "YANETKI" + damga,
                discount_type = (byte)DiscountTypeEnum.Percentage,
                value = 10m,
                min_amount = 0m,
                usage_limit = 100,
                per_user_limit = 0,
                used_count = 0,
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Coupon>().Add(kupon);
            await ctx.SaveChangesAsync();

            return new Kurulum(musteri.id, davetEden.id, urun.id, kupon.id, kupon.code);
        }

        private async Task<int> SiparisVerAsync(Kurulum k, byte odemeYontemi, int adet = 2)
        {
            var place = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(new OrderCreateRequestDto
            {
                customer_id = k.MusteriId,
                coupon_code = k.KuponKodu,
                use_store_credit = 0m,
                payment_method = odemeYontemi,
                items = new() { new OrderItemRequestDto { product_id = k.UrunId, size = "M", quantity = adet } }
            }));
            place.Item2.Success.Should().BeTrue($"siparis olusmali: {place.Item2.Message}");

            await using var ctx = NewContext();
            return (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.customer_id == k.MusteriId)).id;
        }

        private sealed record Sayimlar(int Fatura, int SadakatKazanim, int KuponSatiri, int KuponSayaci,
            int RefereeOdul, int ReferrerOdul, int OnayMesaji);

        private static async Task<Sayimlar> SayAsync(Kurulum k, int orderId)
        {
            await using var ctx = NewContext();
            return new Sayimlar(
                await ctx.Set<Invoice>().CountAsync(i => i.order_id == orderId),
                await ctx.Set<LoyaltyTransaction>().CountAsync(t => t.order_id == orderId
                        && t.type == (byte)LedgerEntryTypeEnum.Earn),
                await ctx.Set<CouponUsage>().CountAsync(u => u.order_id == orderId),
                (await ctx.Set<Coupon>().AsNoTracking().SingleAsync(c => c.id == k.KuponId)).used_count,
                await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == k.MusteriId
                        && t.reason == ReferralManager.RefereeRewardReason),
                await ctx.Set<StoreCreditTransaction>().CountAsync(t => t.customer_id == k.DavetEdenId
                        && t.reason == ReferralManager.ReferrerRewardReason),
                await ctx.Set<OutboxMessage>().CountAsync(m => m.event_type == "PaymentConfirmed"
                        && m.payload.Contains($"\"order_id\":{orderId},")));
        }

        // Dort yan etkinin de BIRER kez uygulandigini tek yerde dogrular.
        private static void DortYanEtkiUygulandi(Sayimlar s, string kanal)
        {
            s.Fatura.Should().Be(1, $"{kanal}: fatura kesilmeli");
            s.SadakatKazanim.Should().Be(1, $"{kanal}: sadakat puani YAZILMALI - kart disi yollarda HIC yazilmiyordu (B10)");
            s.KuponSatiri.Should().Be(1, $"{kanal}: kupon kullanim satiri YAZILMALI - kart disi yollarda HIC yazilmiyordu (B10)");
            s.KuponSayaci.Should().Be(1, $"{kanal}: used_count kullanim satirindan TURETILIR, dolayisiyla 1 olmali");
            s.RefereeOdul.Should().Be(1, $"{kanal}: davet edilene referans odulu YAZILMALI");
            s.ReferrerOdul.Should().Be(1, $"{kanal}: davet edene referans odulu YAZILMALI");
        }

        // ── 1) KAPIDA ODEME ──────────────────────────────────────────────────────────────────
        //
        // Dalga 2'de olculen tablonun DOGRUDAN karsiligi: COD siparisinde sadakat 0'di.
        [Fact]
        public async Task KapidaOdemeOnayi_DORT_YAN_ETKIYI_de_Uygular_PUAN_KUPON_REFERANS_FATURA()
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 1);   // 1 = kapida odeme

            await using (var ctx = NewContext())
            {
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == orderId)).status
                    .Should().Be((byte)OrderStatusEnum.Confirmed, "kapida odeme siparisi ANINDA onaylanir");
            }

            await OutboxBosaltAsync();

            DortYanEtkiUygulandi(await SayAsync(k, orderId), "kapida odeme");
        }

        // ── 2) HAVALE/EFT ADMIN ONAYI ────────────────────────────────────────────────────────
        [Fact]
        public async Task HavaleAdminOnayi_DORT_YAN_ETKIYI_de_Uygular()
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 2);   // 2 = havale/EFT -> Pending kalir

            // VAKUM KIRICI: onaydan ONCE hicbir yan etki olmamali. Bu olmadan, yan etkiler
            // siparis olusurken uygulanmis olsa bile test yesil kalirdi.
            var oncesi = await SayAsync(k, orderId);
            oncesi.SadakatKazanim.Should().Be(0, "havale siparisi onaylanana kadar puan YAZILMAMALI");
            oncesi.Fatura.Should().Be(0, "havale siparisi onaylanana kadar fatura KESILMEMELI");

            var onay = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ConfirmManualPayment(orderId));
            onay.Item2.Success.Should().BeTrue($"havale onayi basarili olmali: {onay.Item2.Message}");

            await OutboxBosaltAsync();

            DortYanEtkiUygulandi(await SayAsync(k, orderId), "havale admin onayi");
        }

        // ── 3) ADMIN DURUM DEGISIKLIGI (Pending -> Confirmed) ────────────────────────────────
        [Fact]
        public async Task AdminDurumDegisikligi_Confirmeda_DORT_YAN_ETKIYI_de_Uygular()
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 2);   // Pending kalsin

            var degistir = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                new OrderStatusChangeRequestDto { id = orderId, order_status = OrderStatusEnum.Confirmed }));
            degistir.Item2.Success.Should().BeTrue($"durum degisikligi basarili olmali: {degistir.Item2.Message}");

            await OutboxBosaltAsync();

            DortYanEtkiUygulandi(await SayAsync(k, orderId), "admin durum degisikligi");
        }

        // ── 4) AYNI SIPARIS IKI KEZ ONAYLANIRSA YAN ETKI BIRER KALIR ─────────────────────────
        //
        // Kullanicinin acikca istedigi pin. At-least-once teslimatin gercek karsiligi: mesaj
        // yeniden teslim edilebilir (reclaim, iki instance, ag bolunmesi).
        [Fact]
        public async Task AyniSiparis_IKI_KEZ_Islenirse_YanEtkiler_BIRER_Kalir()
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 1);

            await OutboxBosaltAsync();
            var ilk = await SayAsync(k, orderId);
            DortYanEtkiUygulandi(ilk, "ilk islem");   // VAKUM KIRICI: once GERCEKTEN uygulandigi gorulur

            // AT-LEAST-ONCE TAKLIDI: isleyici mesaji Processed yapti; gercek bir yeniden teslimat
            // onu tekrar Pending gorur.
            await using (var ctx = NewContext())
            {
                foreach (var m in await ctx.Set<OutboxMessage>()
                             .Where(x => x.payload.Contains($"\"order_id\":{orderId},")).ToListAsync())
                {
                    m.status = 0;
                    m.processed_at = null;
                }
                await ctx.SaveChangesAsync();
            }

            await OutboxBosaltAsync();

            var ikinci = await SayAsync(k, orderId);
            ikinci.Should().BeEquivalentTo(ilk,
                "ikinci teslimat DORT ADIMIN HICBIRINDE fazla etki uretmemeli - " +
                "fatura, sadakat, kupon satiri/sayaci ve iki referans odulu BIRER kalmali");
        }

        // ── 4b) FATURA TEK KEZ KESILIR - IKI YAZICI CAKISMAZ (kullanicinin (ii) sarti) ───────
        //
        // Kart DISI onay yollarinda fatura SENKRON kesilir (bugune kadar oyleydi ve oyle kalmali -
        // asenkrona tasima denemesi iki mevcut IDOR pinini kirdi, olculdu). Ayni faturayi outbox
        // isleyicisinin 1. adimi da kesmeye calisir. Bu pin ikisinin CAKISMADIGINI olcer:
        // senkron cagri keser, isleyici NO-OP doner.
        //
        // YAN KAZANC (bilerek boyle): senkron cagri patlarsa isleyici faturayi TAMAMLAR - kart
        // disi yollarda fatura onceden best-effort'tu ve HIC yeniden denenmiyordu.
        [Fact]
        public async Task Fatura_ONAY_ANINDA_Kesilir_ve_OutboxTeslimati_IKINCI_FATURA_URETMEZ()
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 1);

            // OUTBOX HENUZ ISLENMEDI: fatura yine de HAZIR olmali (senkron yol).
            (await SayAsync(k, orderId)).Fatura.Should().Be(1,
                "kart disi onayda fatura ANINDA kesilmeli - musteri/admin faturayi beklemeden gorur");

            // VAKUM KIRICI: diger uc yan etki bu noktada HENUZ uygulanmamis olmali; aksi halde
            // "outbox hic gerekmiyor" anlamina gelirdi ve bu dalganin duzeltmesi olculmemis olurdu.
            var oncesi = await SayAsync(k, orderId);
            oncesi.SadakatKazanim.Should().Be(0, "sadakat outbox teslimatiyla gelir");
            oncesi.KuponSatiri.Should().Be(0, "kupon kullanim satiri outbox teslimatiyla gelir");

            await OutboxBosaltAsync();

            var sonrasi = await SayAsync(k, orderId);
            sonrasi.Fatura.Should().Be(1,
                "outbox isleyicisinin 1. adimi AYNI faturayi kesmeye calisir; InvoiceManager'in " +
                "'bu siparis icin fatura zaten var' kontrolu NO-OP dondurmeli - IKINCI fatura OLMAZ");
            DortYanEtkiUygulandi(sonrasi, "senkron fatura + outbox");
        }

        // ── 5) MUKERRER MESAJ OLUSMAZ (kullanicinin (i) sarti) ───────────────────────────────
        //
        // Iki ayri riski birlikte olcer:
        //   (a) kart yolu: siparis ZATEN Confirmed iken admin bir durum degisikligi yaparsa
        //       ikinci bir onay mesaji URETILMEMELI (guard: previousStatus != Confirmed).
        //   (b) kart disi yol: onay mesaji yazildiktan sonra ilerleyen durumlar (Preparing,
        //       Shipped) yeni mesaj URETMEMELI.
        [Fact]
        public async Task Onay_TEK_MESAJ_Yazar_SonrakiDurumGecisleri_MUKERRER_MESAJ_URETMEZ()
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 1);   // COD -> onay + TEK mesaj

            (await SayAsync(k, orderId)).OnayMesaji.Should().Be(1, "onay TAM BIR mesaj yazmali");

            // Confirmed -> Preparing -> Shipped: hicbiri yeni mesaj uretmemeli.
            foreach (var hedef in new[] { OrderStatusEnum.Preparing, OrderStatusEnum.Shipped })
            {
                var r = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                    new OrderStatusChangeRequestDto { id = orderId, order_status = hedef }));
                r.Item2.Success.Should().BeTrue($"{hedef} gecisi basarili olmali: {r.Item2.Message}");
            }

            (await SayAsync(k, orderId)).OnayMesaji.Should().Be(1,
                "onay SONRASI durum gecisleri IKINCI bir onay mesaji URETMEMELI - " +
                "kart yolunda da ayni guard calisir (callback siparisi Confirmed yapar, " +
                "admin sonradan Preparing'e tasisa bile yeni mesaj olusmaz)");
        }

        // ── 6) IPTAL EDILMIS SIPARISIN FATURASI "Sent" KALAMAZ (veri artigi invarianti) ──────
        //
        // Dalga 2'de dev veritabaninda iptal edilmis YEDI siparisin faturasi hala Sent bulundu
        // (22-23 Temmuz artiklari). Bugunku kod uc iptal yolunun ucunde de faturayi iptal ediyor;
        // BU PIN o iddiayi SABITLER - iki OrderManager yolu icin. (Ucuncu yol, Iyzico'nun
        // basarisiz dali, S7 pini `FraudReddi_FaturaBIRAKMAZ_VeCiroyaGIRMEZ` ile zaten kapali.)
        [Theory]
        [InlineData(true)]    // son kalemi iptal ederek tam iptal
        [InlineData(false)]   // admin durum degisikligiyle iptal
        public async Task IptalEdilenSiparisin_Faturasi_SENT_KALAMAZ(bool kalemIptaliyle)
        {
            if (Skipped()) return;

            var k = await KurulumYapAsync();
            var orderId = await SiparisVerAsync(k, odemeYontemi: 1);
            await OutboxBosaltAsync();

            // VAKUM KIRICI: once faturanin GERCEKTEN kesilmis ve Sent oldugu dogrulanir.
            await using (var ctx = NewContext())
            {
                var f = await ctx.Set<Invoice>().AsNoTracking().SingleAsync(i => i.order_id == orderId);
                f.status.Should().Be((byte)InvoiceStatusEnum.Sent, "iptalden ONCE fatura Sent olmali");
            }

            if (kalemIptaliyle)
            {
                int itemId;
                await using (var ctx = NewContext())
                    itemId = (await ctx.Set<OrderItem>().AsNoTracking().FirstAsync(i => i.order_id == orderId)).id;

                var r = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                    .CancelItem(orderId, itemId, k.MusteriId));
                r.Item2.Success.Should().BeTrue($"kalem iptali basarili olmali: {r.Item2.Message}");
            }
            else
            {
                var r = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().ChangeOrderStatus(
                    new OrderStatusChangeRequestDto { id = orderId, order_status = OrderStatusEnum.Cancelled }));
                r.Item2.Success.Should().BeTrue($"iptal basarili olmali: {r.Item2.Message}");
            }

            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().AsNoTracking().SingleAsync(x => x.id == orderId);
                o.status.Should().Be((byte)OrderStatusEnum.Cancelled, "siparis iptal edilmis olmali");

                var f = await ctx.Set<Invoice>().AsNoTracking().SingleAsync(i => i.order_id == orderId);
                f.status.Should().Be((byte)InvoiceStatusEnum.Cancelled,
                    "IPTAL EDILMIS siparisin faturasi Sent KALAMAZ - fatura mali bir beyandir; " +
                    "acikta kalirsa ciro sisirilir ve musteriye olmayan bir borc gonderilir");
            }
        }
    }
}
