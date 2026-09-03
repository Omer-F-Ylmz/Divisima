using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Concrete.Context;
using Divisima.Entity.Dtos.Product;
using Divisima.Entity.Dtos.Return;
using Divisima.Entity.Dtos.Shipping;
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
    // ══ DALGA B - OPERASYON YUZEYI (DAVRANIS PINLERI) ═════════════════════════════════════
    //
    // Bu dalganin amaci "site acildiginda gelen siparisi YONETEBILMEK"ti. Admin panelinin sekiz
    // ekranindan besi (dashboard, orders, returns, shipments, coupons) HIC ACILMAMISTI; acilinca
    // uc ayri sinifta kusur cikti ve hepsi CANLI olculdu:
    //
    //   B1 KUPON     : panel "discount_value" gonderiyordu, DTO alani "value" -> %30 girilen
    //                  kupon DB'ye 0 olarak yaziliyor, her katman "basarili" diyordu.
    //   B2 SIPARIS   : uc {items,totalCount}, panel {Items,TotalCount} -> 52 siparis varken
    //                  ekran "Siparis yok" gosteriyordu.
    //   B2 URUN      : form "stocks"/"color_hex" gondermiyordu -> ekleme/duzenleme IMKANSIZDI.
    //                  Form calisir hale gelince ALTTAKI URETIM HATASI ortaya cikti (asagi).
    //   B3 IADE      : iki yol da dogru calisiyordu ama musteriye HICBIR bildirim gitmiyordu.
    //   B4 KARGO     : takip numarasi musteriye HICBIR kanaldan ulasmiyordu; ustelik takip
    //                  entegrasyonu KAPALIYKEN sahte durum VERITABANINA yaziliyordu.
    //
    // Kaynak sozlesmesi pinleri AdminPanelSozlesmeTests'te; burasi DAVRANISI tutar.
    [Trait("Category", "Sql")]
    public class DalgaBOperasyonTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaDalgaBTest";
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");
        private const string VitrinTabani = "https://vitrin.divisima.test";

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

        private static readonly List<MailMessageDto> Yakalanan = new();

        private sealed class SahteMail : IMailService
        {
            public Task SendAsync(MailMessageDto message)
            {
                lock (Yakalanan) Yakalanan.Add(message);
                return Task.CompletedTask;
            }
        }

        private sealed class DalgaBFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseSetting("Storefront:BaseUrl", VitrinTabani);
                // KARGO TAKIP ENTEGRASYONU KAPALI - LAUNCH YAPILANDIRMASININ AYNISI.
                // Kargo firmasi entegrasyonu yok ve olmayacak (is karari), yani uretimde
                // surekli kosacak dal budur. B4 pini tam olarak bu dali olcer.
                builder.UseSetting("Shipping:Enabled", "false");
                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    services.AddDbContext<DivisimaDbContext>(o => o.UseSqlServer(ConnStr));
                    services.AddScoped<IMailService, SahteMail>();
                });
            }
        }

        private DalgaBFactory? _factory;
        private bool _sqlAvailable;

        private static DivisimaDbContext NewContext() =>
            new DivisimaDbContext(new DbContextOptionsBuilder<DivisimaDbContext>().UseSqlServer(ConnStr).Options);

        public async Task InitializeAsync()
        {
            lock (Yakalanan) Yakalanan.Clear();
            try
            {
                await using (var pre = NewContext())
                {
                    await TestDbKurulum.SilAsync(pre.Database);
                    await TestDbKurulum.OlusturAsync(pre.Database);
                }
                _factory = new DalgaBFactory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak Dalga B testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // ── Kurgu yardimcilari ────────────────────────────────────────────────────────────
        private static async Task<(int UrunId, string Beden, int KategoriId)> UrunHazirlaAsync(int stok = 20)
        {
            await using var ctx = NewContext();
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            var kat = new Category { name = "DalgaB " + damga, slug = "dalgab-" + damga, is_active = true, created_at = DateTime.Now };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "DalgaB Urun " + damga,
                description = "Dalga B pini icin urun.",
                color_hex = "#334455",
                brand = "Divisima",
                price = 499.90m,
                category_id = kat.id,
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
            await ctx.SaveChangesAsync();
            return (urun.id, "M", kat.id);
        }

        // ADMIN ISTEMCI: TestAuthHelper YENIDEN KULLANILIR (gercek register/verify/login),
        // sonra user_type Admin'e cekilip TEKRAR giris yapilir. Uydurma JWT yok.
        private async Task<HttpClient> AdminIstemciAsync()
        {
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            await using (var ctx = NewContext())
            {
                var c = await ctx.Set<Customer>().SingleAsync(x => x.id == user.CustomerId);
                c.user_type = (byte)UserTypeEnum.Admin;
                await ctx.SaveChangesAsync();
            }
            var anon = _factory!.CreateClient();
            var login = await anon.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = TestAuthHelper.TestPassword });
            login.IsSuccessStatusCode.Should().BeTrue(
                $"admin girisi calismali: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await login.Content.ReadAsStringAsync())}");
            using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var token = doc.RootElement.GetProperty("data").GetProperty("token").GetString();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // Teslim edilmis, iadeye uygun bir siparis kurar ve iade talebi acar.
        private async Task<(int OrderId, int ReturnId, int CustomerId, int UrunId, string Beden)> TeslimEdilmisIadeliSiparisAsync()
        {
            var (urunId, beden, _) = await UrunHazirlaAsync();
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            int orderId;
            await using (var ctx = NewContext())
            {
                var siparis = new Order
                {
                    customer_id = user.CustomerId,
                    order_number = "DVSB" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(),
                    status = (byte)OrderStatusEnum.Delivered,
                    payment_type = 1,                      // kapida odeme -> iade MAGAZA KREDISINE gider
                    subtotal = 499.90m,
                    shipping_cost = 0m,
                    discount_amount = 0m,
                    total_price = 499.90m,
                    coupon_code = "",
                    delivered_at = DateTime.Now.AddDays(-1),
                    created_at = DateTime.Now.AddDays(-3)
                };
                ctx.Set<Order>().Add(siparis);
                await ctx.SaveChangesAsync();
                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = siparis.id,
                    product_id = urunId,
                    size = beden,
                    quantity = 1,
                    unit_price = 499.90m,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
                orderId = siparis.id;
            }

            var r = await user.Client.PostAsJsonAsync("/api/return/create", new
            {
                order_id = orderId,
                product_id = urunId,
                size = beden,
                quantity = 1,
                reason = (byte)0,
                description = "Dalga B pini",
                return_type = (byte)0
            });
            r.IsSuccessStatusCode.Should().BeTrue($"iade talebi acilmali: {await r.Content.ReadAsStringAsync()}");

            await using var ctx2 = NewContext();
            var iade = await ctx2.Set<ReturnRequest>().AsNoTracking()
                .Where(x => x.order_id == orderId).OrderByDescending(x => x.id).FirstAsync();
            return (orderId, iade.id, user.CustomerId, urunId, beden);
        }

        private static List<MailMessageDto> Mailler()
        {
            lock (Yakalanan) return Yakalanan.ToList();
        }

        // ══ B2 - SIPARIS LISTESI ZARFI ════════════════════════════════════════════════════
        // OLCULEN ZARAR: uc, repository tipini (PagedResult<T>) dogrudan donduruyordu; PascalCase
        // ozellikler camelCase'e serilesip { items, totalCount, ... } cikiyordu. Deponun DIGER
        // sayfali uclari { items, total_count, ... } donuyor. Panel snake_case bekliyordu, dolayisiyla
        // veritabaninda 52 siparis varken ekran "Siparis yok" gosteriyordu.
        [Fact]
        public async Task AdminSiparisListesi_SNAKE_CASE_ZARF_Doner_camelCase_ARTIK_YOK()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            await TeslimEdilmisIadeliSiparisAsync();          // en az bir siparis olsun

            var r = await client.PostAsJsonAsync("/api/order/admin/list", new { page = 1, page_size = 20 });
            r.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");

            // VAKUM KIRICI: liste GERCEKTEN dolu olmali. Bos bir liste ile "alan adlari dogru"
            // demek, tam da duzeltilen belirtiyi (bos ekran) yesil gostermek olurdu.
            data.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0,
                "kurulan siparis listede GORUNMELI - ekranin bos kalmasi olculen kusurdu");

            data.TryGetProperty("total_count", out var toplam).Should().BeTrue("zarf snake_case olmali");
            toplam.GetInt32().Should().BeGreaterThan(0);
            data.TryGetProperty("total_pages", out _).Should().BeTrue();

            // CIFT-ANLAM KIRICI: yalnizca "snake_case var" yetmez. Eski camelCase adlarin
            // KALMADIGI da dogrulanmali - ikisi birden donseydi panel calisirdi ama API'de
            // iki konvansiyon yasamaya devam ederdi ve bir sonraki ekran yine yanlisini secerdi.
            data.TryGetProperty("totalCount", out _).Should().BeFalse("repo tipi (PagedResult) artik yanita KONMUYOR");
            data.TryGetProperty("totalPages", out _).Should().BeFalse();
        }

        // ══ B2 - URUN GUNCELLEME: UPSERT ══════════════════════════════════════════════════
        // OLCULEN ZARAR (canli, panelden): guncelleme HTTP 500 verdi ->
        //   "Cannot insert duplicate key row in object 'dbo.product_stocks' with unique index
        //    'IX_product_stocks_product_id_size'. The duplicate key value is (123, S)."
        // Sebep: eski kod TUM beden satirlarini pasifleyip gelenleri YENI SATIR olarak ekliyordu;
        // unique indeks FILTRESIZ, yani pasifleme (product_id,size) ciftini SERBEST BIRAKMIYOR.
        // Ustelik Update TRANSACTION'SIZ: pasifleme kaydedilmis, insert patlamis -> urun TUM
        // AKTIF BEDENLERINI KAYBETMIS ve satin ALINAMAZ hale gelmisti.
        [Fact]
        public async Task UrunGuncelleme_AYNI_BEDEN_TEKRAR_GONDERILINCE_500_VERMEZ_SATIR_KIMLIGI_KORUNUR()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            var (urunId, beden, katId) = await UrunHazirlaAsync(stok: 20);

            int oncekiSatirId;
            await using (var ctx = NewContext())
                oncekiSatirId = (await ctx.Set<ProductStock>().AsNoTracking()
                    .SingleAsync(s => s.product_id == urunId && s.size == beden)).id;

            // Rezervasyon muhasebesinin korundugunu olcebilmek icin satira rezerve adet yaziyoruz.
            await using (var ctx = NewContext())
            {
                var s = await ctx.Set<ProductStock>().SingleAsync(x => x.id == oncekiSatirId);
                s.reserved_quantity = 3;
                await ctx.SaveChangesAsync();
            }

            var r = await client.PutAsJsonAsync("/api/product/update", new ProductUpdateRequestDto
            {
                id = urunId,
                name = "DalgaB Urun (guncellendi)",
                brand = "Divisima",
                category_id = katId,
                price = 449.90m,
                description = "guncellendi",
                color_hex = "#334455",
                product_type = ProductTypeEnum.Clothing,
                stocks = new List<ProductStockDto> { new() { size = beden, stock_quantity = 12 } }
            });

            r.StatusCode.Should().Be(HttpStatusCode.OK,
                $"ayni bedeni tekrar gondermek 500 VERMEMELI. Govde: {await r.Content.ReadAsStringAsync()}");

            await using var son = NewContext();
            var satirlar = await son.Set<ProductStock>().AsNoTracking()
                .Where(s => s.product_id == urunId).ToListAsync();

            satirlar.Should().HaveCount(1, "yeni satir EKLENMEMELI - upsert var olani gunceller");
            satirlar[0].id.Should().Be(oncekiSatirId, "satir KIMLIGI korunmali");
            satirlar[0].is_active.Should().BeTrue("beden hala satista olmali");
            // VAKUM KIRICI: guncelleme GERCEKTEN bir sey yapmis olmali - yoksa "hicbir sey yapma"
            // uygulamasi da bu pini gecerdi.
            satirlar[0].stock_quantity.Should().Be(12, "gonderilen adet yazilmali");
            // ASIL KAZANC: yeni satir eklenseydi reserved_quantity SIFIRLANIRDI ve
            // "available = stock - reserved" kimligi bozulup ayni mal iki kez satilabilirdi.
            satirlar[0].reserved_quantity.Should().Be(3, "rezervasyon muhasebesi KAYBOLMAMALI");
        }

        [Fact]
        public async Task UrunGuncelleme_GONDERILMEYEN_BEDEN_PASIFLENIR_SATIR_SILINMEZ()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            var (urunId, beden, katId) = await UrunHazirlaAsync(stok: 20);

            await using (var ctx = NewContext())
            {
                ctx.Set<ProductStock>().Add(new ProductStock
                {
                    product_id = urunId,
                    size = "L",
                    stock_quantity = 5,
                    reserved_quantity = 0,
                    is_active = true,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
            }

            // Yalnizca "M" gonderiliyor - "L" listede YOK.
            var r = await client.PutAsJsonAsync("/api/product/update", new ProductUpdateRequestDto
            {
                id = urunId,
                name = "DalgaB Urun",
                brand = "Divisima",
                category_id = katId,
                price = 499.90m,
                description = "d",
                color_hex = "#334455",
                product_type = ProductTypeEnum.Clothing,
                stocks = new List<ProductStockDto> { new() { size = beden, stock_quantity = 9 } }
            });
            r.StatusCode.Should().Be(HttpStatusCode.OK);

            await using var son = NewContext();
            var hepsi = await son.Set<ProductStock>().AsNoTracking().Where(s => s.product_id == urunId).ToListAsync();

            hepsi.Should().HaveCount(2, "listede olmayan beden SILINMEZ - siparis/rezervasyon gecmisi korunur");
            hepsi.Single(s => s.size == "L").is_active.Should().BeFalse("gonderilmeyen beden PASIFLENIR");
            // CIFT-ANLAM KIRICI: "hepsini pasifle" uygulamasi da yukaridaki asserti gecerdi.
            hepsi.Single(s => s.size == beden).is_active.Should().BeTrue("gonderilen beden AKTIF kalmali");
            hepsi.Single(s => s.size == beden).stock_quantity.Should().Be(9);
        }

        // ══ B3 - IADE SONUCU MUSTERIYE BILDIRILIR ═════════════════════════════════════════
        // OLCULEN ONCE-DURUM: ReturnManager'da mail/outbox/bildirim SIFIR referanstı - admin
        // iadeyi onaylayip magaza kredisi yazsa da musteriye HICBIR SEY gitmiyordu.
        [Fact]
        public async Task IadeOnayi_MUSTERIYE_MAIL_YAZAR_TUTARI_ve_NEREYE_GITTIGINI_Soyler()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            var (_, iadeId, musteriId, _, _) = await TeslimEdilmisIadeliSiparisAsync();
            lock (Yakalanan) Yakalanan.Clear();

            var r = await client.PostAsJsonAsync("/api/return/process",
                new ReturnProcessRequestDto { return_id = iadeId, approve = true, admin_note = "" });
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());

            await OutboxBosaltAsync();

            await using var ctx = NewContext();
            var musteri = await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == musteriId);

            // VAKUM KIRICI: iade GERCEKTEN islenmis olmali (yoksa "mail yok" da dogru olurdu).
            musteri.store_credit.Should().Be(499.90m, "kapida odenmis siparisin iadesi magaza kredisine gider");

            var mail = Mailler().SingleOrDefault(m => m.To == musteri.email && m.Subject.Contains("onaylandı"));
            mail.Should().NotBeNull("iade onayi musteriye BILDIRILMELI - once hicbir kanal yoktu");
            mail!.Body.Should().Contain("499,90", "tutar yazilmali (kultur tr-TR'ye pinli)");
            // CIFT-ANLAM KIRICI: "iade edildi" demek yetmez - PARANIN NEREYE gittigi yazilmali,
            // yoksa parasini kartinda arayan musteri uretir.
            mail.Body.Should().Contain("mağaza kredine");
            mail.Body.Should().Contain(VitrinTabani, "ayrinti baglantisi TEK KAYNAKTAN uretilmeli");
        }

        [Fact]
        public async Task IadeReddi_MUSTERIYE_MAIL_YAZAR_ADMIN_NOTUYLA_ve_HICBIR_PARA_HAREKETI_OLMAZ()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            var (_, iadeId, musteriId, urunId, beden) = await TeslimEdilmisIadeliSiparisAsync();
            lock (Yakalanan) Yakalanan.Clear();

            const string not = "Urun kullanilmis olarak geldi.";
            var r = await client.PostAsJsonAsync("/api/return/process",
                new ReturnProcessRequestDto { return_id = iadeId, approve = false, admin_note = not });
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());

            await OutboxBosaltAsync();

            await using var ctx = NewContext();
            var musteri = await ctx.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == musteriId);
            var mail = Mailler().SingleOrDefault(m => m.To == musteri.email && m.Subject.Contains("hakkında"));
            mail.Should().NotBeNull("ret de BILDIRILMELI - musterinin bekledigi cevap budur");
            mail!.Body.Should().Contain(not, "admin notu musteriye ULASMALI, yoksa 'neden reddedildi' sorusu cevapsiz kalir");

            // CIFT-ANLAM KIRICI: ret yolunda HICBIR para/stok hareketi olmamali. Yalnizca maile
            // bakan bir pin, yanlislikla iade de yapan bir uygulamayi yesil gosterirdi.
            musteri.store_credit.Should().Be(0m, "ret PARA HAREKETI URETMEZ");
            var stok = await ctx.Set<ProductStock>().AsNoTracking().SingleAsync(s => s.product_id == urunId && s.size == beden);
            stok.stock_quantity.Should().Be(20, "ret STOK IADESI URETMEZ");
        }

        // ══ B4 - KARGO ════════════════════════════════════════════════════════════════════
        // OLCULEN ONCE-DURUM: takip numarasi hicbir bildirim mesajinda YOKTU ve musteri onu
        // hicbir ekranda goremiyordu.
        [Fact]
        public async Task KargoOlusturulunca_MAIL_TAKIP_NUMARASINI_ve_FIRMAYI_Tasir()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            var (urunId, beden, _) = await UrunHazirlaAsync();
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            int orderId;
            await using (var ctx = NewContext())
            {
                var s = new Order
                {
                    customer_id = user.CustomerId,
                    order_number = "DVSB" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(),
                    status = (byte)OrderStatusEnum.Preparing,
                    payment_type = 1,
                    subtotal = 499.90m,
                    shipping_cost = 0m,
                    discount_amount = 0m,
                    total_price = 499.90m,
                    coupon_code = "",
                    created_at = DateTime.Now
                };
                ctx.Set<Order>().Add(s);
                await ctx.SaveChangesAsync();
                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = s.id,
                    product_id = urunId,
                    size = beden,
                    quantity = 1,
                    unit_price = 499.90m,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
                orderId = s.id;
            }

            lock (Yakalanan) Yakalanan.Clear();
            const string takipNo = "MNG555444333";
            var r = await client.PostAsJsonAsync("/api/shipment/create", new ShipmentCreateDto
            {
                order_id = orderId,
                carrier = (byte)CarrierEnum.Mng,
                tracking_number = takipNo
            });
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());

            await OutboxBosaltAsync();

            await using var ctx2 = NewContext();
            var musteri = await ctx2.Set<Customer>().AsNoTracking().SingleAsync(c => c.id == user.CustomerId);
            var mail = Mailler().SingleOrDefault(m => m.To == musteri.email && m.Subject.Contains("kargoya verildi"));
            mail.Should().NotBeNull("kargoya verilince musteriye e-posta gitmeli");
            mail!.Body.Should().Contain(takipNo, "TAKIP NUMARASI mesajda OLMALI - entegrasyon yok, musterinin tek dayanagi bu");
            // GORUNTU ADI: ham enum "Mng" degil, "MNG Kargo".
            mail.Body.Should().Contain("MNG Kargo");
        }

        // Bu pin, LAUNCH YAPILANDIRMASINI (Shipping:Enabled=false) olcer.
        // OLCULEN ZARAR: kapali dal Success=true + NormalizedStatus=1 (InTransit) +
        // RawStatusText="Takip devre disi (dev)" donuyordu ve cagiran bunu VERITABANINA YAZIYORDU.
        // Canli olculdu: admin kargoyu Preparing olarak olusturdu, musteri BIR KEZ track cagirdi,
        // satir InTransit + gelistirici dizgesi haline geldi. Paketi kimse tasimamisti.
        [Fact]
        public async Task KargoTakibi_ENTEGRASYON_KAPALIYKEN_SAHTE_DURUM_YAZMAZ_GERCEK_KAYIT_KORUNUR()
        {
            if (Skipped()) return;
            var client = await AdminIstemciAsync();
            var (urunId, beden, _) = await UrunHazirlaAsync();
            var user = await TestAuthHelper.CreateCustomerClientAsync(_factory!);

            int orderId;
            await using (var ctx = NewContext())
            {
                var s = new Order
                {
                    customer_id = user.CustomerId,
                    order_number = "DVSB" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(),
                    status = (byte)OrderStatusEnum.Preparing,
                    payment_type = 1,
                    subtotal = 499.90m,
                    shipping_cost = 0m,
                    discount_amount = 0m,
                    total_price = 499.90m,
                    coupon_code = "",
                    created_at = DateTime.Now
                };
                ctx.Set<Order>().Add(s);
                await ctx.SaveChangesAsync();
                ctx.Set<OrderItem>().Add(new OrderItem
                {
                    order_id = s.id,
                    product_id = urunId,
                    size = beden,
                    quantity = 1,
                    unit_price = 499.90m,
                    created_at = DateTime.Now
                });
                await ctx.SaveChangesAsync();
                orderId = s.id;
            }

            const string takipNo = "ARS987654321";
            (await client.PostAsJsonAsync("/api/shipment/create", new ShipmentCreateDto
            {
                order_id = orderId,
                carrier = (byte)CarrierEnum.Aras,
                tracking_number = takipNo
            })).StatusCode.Should().Be(HttpStatusCode.OK);

            // MUSTERI TAKIP CAGIRIYOR - zararin tetiklendigi an tam olarak burasiydi.
            var takip = await user.Client.GetAsync($"/api/shipment/track/{orderId}");
            takip.StatusCode.Should().Be(HttpStatusCode.OK, await takip.Content.ReadAsStringAsync());
            using var doc = JsonDocument.Parse(await takip.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");

            // VAKUM KIRICI: uc GERCEKTEN kargo kaydini donmus olmali; bos bir yanit da
            // "sahte durum yok" iddiasini bedavaya gecerdi.
            data.GetProperty("tracking_number").GetString().Should().Be(takipNo);
            data.GetProperty("carrier_name").GetString().Should().Be("Aras Kargo", "ham enum adi degil GORUNTU adi");

            data.GetProperty("status").GetByte().Should().Be((byte)ShipmentStatusEnum.Preparing,
                "entegrasyon kapaliyken durum UYDURULMAZ - adminin birakti gi hal korunur");
            data.GetProperty("last_status_text").ValueKind.Should().Be(JsonValueKind.Null,
                "'Takip devre disi (dev)' gibi bir GELISTIRICI dizgesi musteriye SERVIS EDILEMEZ");

            // VERITABANINDA DA yazilmamis olmali - zarar yanitta degil KALICI kayitta olusuyordu.
            await using var ctx2 = NewContext();
            var kargo = await ctx2.Set<Shipment>().AsNoTracking().SingleAsync(s => s.order_id == orderId);
            kargo.status.Should().Be((byte)ShipmentStatusEnum.Preparing);
            kargo.last_status_text.Should().BeNull();
            kargo.last_checked_at.Should().BeNull("basarisiz sorgu 'kontrol edildi' olarak damgalanmamali");
        }

        // ═══ p-k6a - FAZ 0 / K6: DENETIM UCUNUN YETKI KAPISI ══════════════════════════════
        //
        // Uc, is katmanina tasindi (IAuditLogService). Tasima sirasinda yetki ozniteligi
        // dusseydi denetim kayitlari (audit_logs, uretimde 1500+ satir; kim ne degistirdi
        // bilgisi) ANONIME acilirdi. Bu pin kapiyi davranisla sabitler.
        [Fact]
        public async Task DenetimUcu_ANONIME_401_MUSTERIYE_403_ADMINE_200()
        {
            if (Skipped()) return;

            var anon = _factory!.CreateClient();
            var anonYanit = await anon.GetAsync("/api/auditlog/list");
            anonYanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "denetim kaydi ANONIM okunamaz");

            // Deponun admin-kapisi sozlesmesi: kimlikli ama yetkisiz -> 403 (RequireUserType).
            var musteri = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var musteriYanit = await musteri.Client.GetAsync("/api/auditlog/list");
            musteriYanit.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "kimlikli ama admin OLMAYAN kullanici 403 almali - deponun RequireUserType sozlesmesi");

            // VAKUM KIRICI: kapi HERKESI reddetmiyor - admin GERCEKTEN girebiliyor.
            var admin = await AdminIstemciAsync();
            var adminYanit = await admin.GetAsync("/api/auditlog/list");
            adminYanit.StatusCode.Should().Be(HttpStatusCode.OK,
                $"admin denetim kaydini okuyabilmeli: {Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(await adminYanit.Content.ReadAsStringAsync())}");
        }

        // ═══ p-k6b - FAZ 0 / K6: ZARF SEKLI + tableName FILTRESI ══════════════════════════
        //
        // OLCULEN ONCE-DURUM: uc SuccessDataResult<PagedResult<AuditLog>> donuyordu; repository
        // tipi HTTP'ye sizdigi icin zarf camelCase {items,totalCount,page,size,totalPages}
        // cikiyordu - oysa deponun DIGER sayfali uclari snake_case {items,total_count,...}
        // donuyor. Bu, DALGA B / B2'de olculup duzeltilen defektin IKINCI ornegiydi ve orada
        // bedeli CANLIDA odenmisti (admin siparis listesi hep bos gorunuyordu).
        //
        // CIFT-ANLAM KIRICI: yalnizca "total_count var" demek yetmez - eski camelCase alanlarin
        // ARTIK OLMADIGI da assert edilir; ikisi birden donseydi panel calisirdi ama iki
        // konvansiyon yasamaya devam ederdi (B2'de birebir bu ayrim yapilmisti).
        [Fact]
        public async Task DenetimUcu_SNAKE_CASE_ZARF_Doner_ve_tableName_FILTRESI_CALISIR()
        {
            if (Skipped()) return;
            var admin = await AdminIstemciAsync();

            // KURGU NEDEN ELLE: bu host'ta AuditInterceptor ATESLEMEZ - OLCULDU. DalgaBFactory
            // DbContextOptions kaydini kaldirip `AddDbContext(o => o.UseSqlServer(ConnStr))` ile
            // YENIDEN kuruyor ve o kayit `.AddInterceptors(...)` TASIMIYOR (Program.cs'teki
            // uretim kaydi tasiyor). Yani audit_logs bu suitte BOS kalir. Pinin OLCTUGU sey
            // interceptor DEGIL, UCUN SOZLESMESI (zarf sekli + DTO alanlari + tableName filtresi);
            // bu yuzden satirlar dogrudan kuruluyor. Interceptor'in kendi davranisi AYRI bir is.
            var damga = Guid.NewGuid().ToString("N").Substring(0, 8);
            var hedefTablo = $"faz0_hedef_{damga}";
            var digerTablo = $"faz0_diger_{damga}";
            await using (var seed = NewContext())
            {
                for (int i = 0; i < 3; i++)
                    seed.Set<AuditLog>().Add(new AuditLog
                    {
                        table_name = hedefTablo,
                        entity_id = $"{i}",
                        action = "Modified",
                        changes = "{\"alan\":[\"eski\",\"yeni\"]}",
                        user_id = "faz0",
                        created_at = DateTime.Now.AddMinutes(-i)
                    });
                seed.Set<AuditLog>().Add(new AuditLog
                {
                    table_name = digerTablo,
                    entity_id = "9",
                    action = "Added",
                    user_id = "faz0",
                    created_at = DateTime.Now.AddMinutes(-10)
                });
                await seed.SaveChangesAsync();
            }

            var hepsi = await admin.GetAsync("/api/auditlog/list?page=1&size=5");
            hepsi.StatusCode.Should().Be(HttpStatusCode.OK);

            using var doc = JsonDocument.Parse(await hepsi.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");

            var alanlar = data.EnumerateObject().Select(p => p.Name).ToList();
            alanlar.Should().Contain(new[] { "items", "total_count", "page", "size", "total_pages" },
                "zarf ProductPagingListResponseDto / AdminOrderPagingListResponseDto ile AYNI konvansiyonda olmali");
            alanlar.Should().NotContain("totalCount",
                "B2 defekti: repository tipi PagedResult<T> HTTP'ye SIZMAMALI");
            alanlar.Should().NotContain("totalPages",
                "camelCase zarf geri gelemez - iki konvansiyon ayni API'de yasayamaz");

            // VAKUM KIRICI: liste GERCEKTEN dolu (yoksa filtre assert'i bedava dogru olurdu).
            data.GetProperty("total_count").GetInt32().Should().BeGreaterOrEqualTo(4,
                "kurgudaki dort denetim satiri listelenmeli");
            data.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

            // Ham entity DISARI CIKMAMALI ama denetim alanlari DTO'da TAM olmali.
            var ilk = data.GetProperty("items")[0];
            foreach (var beklenen in new[] { "id", "table_name", "entity_id", "action", "changes", "user_id", "created_at" })
                ilk.TryGetProperty(beklenen, out _).Should().BeTrue($"'{beklenen}' alani DTO'da olmali");

            // SIRALAMA controller'dan tasindi: created_at DESC (en yeni once).
            ilk.GetProperty("table_name").GetString().Should().Be(hedefTablo,
                "en yeni kayit basta olmali - created_at DESC davranisi TASINDI, degismedi");

            // ── tableName FILTRESI: controller'dan tasinan davranis KORUNDU mu ──
            var filtreli = await admin.GetAsync($"/api/auditlog/list?page=1&size=50&tableName={hedefTablo}");
            filtreli.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc2 = JsonDocument.Parse(await filtreli.Content.ReadAsStringAsync());
            var data2 = doc2.RootElement.GetProperty("data");
            data2.GetProperty("total_count").GetInt32().Should().Be(3,
                "yalniz hedef tablonun UC satiri donmeli");
            foreach (var satir in data2.GetProperty("items").EnumerateArray())
                satir.GetProperty("table_name").GetString().Should().Be(hedefTablo,
                    "tableName ESITLIK filtresi - baska tablo (faz0_diger_*) SIZMAMALI");

            // CIFT-ANLAM KIRICI: filtre GERCEKTEN daraltiyor (her seyi donduren bir uygulama gecmesin).
            var olmayan = await admin.GetAsync("/api/auditlog/list?page=1&size=50&tableName=olmayan_tablo_xyz");
            using var doc3 = JsonDocument.Parse(await olmayan.Content.ReadAsStringAsync());
            doc3.RootElement.GetProperty("data").GetProperty("total_count").GetInt32().Should().Be(0,
                "eslesmeyen tablo adinda sonuc BOS donmeli - filtre yok sayilmiyor");
        }

        // Outbox at-least-once calisir ve arka plan isi (Cron.Minutely) testte kosmaz;
        // bekleyen mesajlar BURADA, GERCEK isleyiciyle bosaltilir (stub degil).
        private async Task OutboxBosaltAsync()
        {
            using var scope = _factory!.Services.CreateScope();
            var islemci = scope.ServiceProvider.GetRequiredService<Divisima.Bussiness.Outbox.OutboxProcessor>();
            await islemci.ProcessPendingAsync();
        }
    }
}
