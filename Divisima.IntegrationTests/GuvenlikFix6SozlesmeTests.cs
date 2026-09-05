using System.Net;
using System.Net.Http.Json;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Validation;
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
    // ══ GF-6 SOZLESME PINLERI (LAUNCH ONCESI: UYE YOLU + DURUM MAKINESI + HUB + ICE-AKTARIM) ══
    //
    // Kaynak: `53·GUVENLIK-AV-3` bulgulari T1-B1 · T1-B2 · T1-B3 · T4-F5 · X-2 · T2-1 · T2-2 · T2-6.
    // ORTAK KOK (merkez tespiti): "misafir yolunun KAZANDIGI kapilar UYE yoluna TASINMAMIS".
    // Her pin olculen ONCE-DURUMU yorumunda tasir; biri duzeltmeyi geri alirsa testin ADI
    // neyin geri geldigini soyler.
    //
    // PIN SINIRI - DURUST BEYAN: bu sinif DAVRANIS olcer. Kaynak-sozlesme pinleri ayrica
    // isaretlidir ve MK-6 geregi uretim mutasyonuyla sinanmistir (kanit raporda).
    [Trait("Category", "Sql")]
    public class GuvenlikFix6SozlesmeTests : IAsyncLifetime
    {
        private const string DbName = "DivisimaGuvenlikFix6Test";
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

        private sealed class Gf6Factory : WebApplicationFactory<Program>
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

        private Gf6Factory? _factory;
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
                _factory = new Gf6Factory();
                _ = _factory.Services;
                _sqlAvailable = true;
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak GF-6 sozlesme testleri ortami hazirlanamadi - ATLANMAMALI.", ex);
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

        // ── KURGU: musteri + adres + urun/stok. Her test KENDI satirlarini uretir (Guid). ──
        private static async Task<(int musteriId, int adresId, int urunId)> KurguAsync(int stok = 20)
        {
            await using var ctx = NewContext();

            var musteri = new Customer
            {
                name = "GF6 Musteri",
                email = $"gf6-{Guid.NewGuid():N}@example.com",
                phone = "5550000000",
                password_hash = new byte[] { 1 },
                password_salt = new byte[] { 2 },
                is_active = true,
                email_verified = true,
                store_credit = 0m,
                created_at = DateTime.Now
            };
            ctx.Set<Customer>().Add(musteri);
            await ctx.SaveChangesAsync();

            var adresId = await TestAdresHelper.AdresOlusturAsync(ctx, musteri.id);

            var kategori = new Category
            {
                name = "GF6 Kategori " + Guid.NewGuid().ToString("N").Substring(0, 6),
                slug = "gf6-" + Guid.NewGuid().ToString("N"),
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kategori);
            await ctx.SaveChangesAsync();

            var urun = new Product
            {
                name = "GF6 Urun " + Guid.NewGuid().ToString("N").Substring(0, 6),
                brand = "GF6",
                category_id = kategori.id,
                price = 100m,
                description = "GF6 test urunu",   // CLAUDE.md 5: ZORUNLU alan
                color_hex = "#123456",            // CLAUDE.md 5: ZORUNLU alan
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

            return (musteri.id, adresId, urun.id);
        }

        private static OrderCreateRequestDto Istek(int musteriId, int adresId, int urunId,
            string? requestId = null, int adet = 1, byte odeme = 1) => new()
            {
                customer_id = musteriId,
                address_id = adresId,
                request_id = requestId,
                coupon_code = "",           // CLAUDE.md 5: non-nullable, binding zorunlu kilar
                use_store_credit = 0m,
                payment_method = odeme,
                items = new() { new OrderItemRequestDto { product_id = urunId, size = "M", quantity = adet } }
            };

        // ═════════════════════════════════════════════════════════════════════════════════════
        // K1 (D1) - request_id REPLAY GUARD'I UYE YOLUNDA · REPRO R-6.1 / R-6.2
        // ═════════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM (T1-B1, LAUNCH BLOKER): `OrderManager.PlaceOrder`in dedup dali
        // YALNIZ `o.request_id == dto.request_id` soruyor, SAHIPLIK SORMUYORDU. `orders.request_id`
        // tekil indeksi GLOBAL oldugu icin, BASKASININ anahtarini gonderen bir uye o siparisin
        // `id` ve `order_number` alanlarini 200 ile GERI ALIYORDU.
        [Fact]
        public async Task R6_1_BASKASININ_REQUEST_IDSI_400_DONER_ve_ORDER_NUMBER_SIZMAZ()
        {
            if (Skipped()) return;
            var a = await KurguAsync();
            var b = await KurguAsync();
            var rid = "gf6-" + Guid.NewGuid().ToString("N");

            // A kendi siparisini verir - POZITIF OLAY (vakum engeli).
            var aSonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(a.musteriId, a.adresId, a.urunId, rid)));
            aSonuc.Item1.Should().Be(HttpStatusCode.Created, $"A siparis verebilmeli: {aSonuc.Item2.Message}");

            string aSiparisNo;
            await using (var ctx = NewContext())
                aSiparisNo = (await ctx.Set<Order>().AsNoTracking()
                    .SingleAsync(o => o.customer_id == a.musteriId)).order_number;

            // B, A'nin request_id'sini gonderir.
            //
            // ══ SEPET BILINCLI OLARAK AYNI - MK-6 ILE OLCULDU ═══════════════════════════════
            // ILK YAZIMDA B KENDI URUNUNU gonderiyordu ve pin, sahiplik yuklemi TAMAMEN
            // KALDIRILDIGINDA BILE YESIL kaldi (MUT-1: `SahipMiAsync` -> `return true`,
            // 0 kirmizi): reddi ureten sey sahiplik DEGIL, SEPET FARKIYDI - yani assert
            // BEDAVA DOGRUYDU. B artik A'NIN URUNUNU, AYNI adet ve AYNI kuponla gonderiyor;
            // geriye ayirt edici TEK olcut olarak MUSTERI KIMLIGI kaliyor.
            var bSonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(b.musteriId, b.adresId, a.urunId, rid)));

            bSonuc.Item1.Should().Be(HttpStatusCode.BadRequest,
                "baskasinin request_id'si SIZINTISIZ 400 almali - 200 + order_number DEGIL");

            // CIFT-ANLAM KIRICI: durum kodu tek basina yetmez, GOVDE de sizdirmamali.
            var govde = System.Text.Json.JsonSerializer.Serialize(bSonuc.Item2);
            govde.Should().NotContain(aSiparisNo, "A'nin siparis NUMARASI B'ye SIZMAMALI");
            govde.Should().NotContain("\"id\":", "yanit VERI tasimamali - sizintisiz 400");

            // B'nin siparisi OLUSMAMALI (reddedilen istek yan etki birakmaz).
            await using (var ctx = NewContext())
                (await ctx.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == b.musteriId))
                    .Should().Be(0, "reddedilen istek siparis YAZMAMALI");
        }

        // CIFT-ANLAM KIRICI + T1-B7: AYNI musteri AYNI sepetle tekrar gonderirse idempotent
        // 200 almali ve `replayed` bayragi GERCEK degeri tasimali.
        [Fact]
        public async Task R6_2_AYNI_MUSTERI_AYNI_SEPET_200_REPLAY_FARKLI_SEPET_400()
        {
            if (Skipped()) return;
            var m = await KurguAsync();
            var rid = "gf6-" + Guid.NewGuid().ToString("N");

            var ilk = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(m.musteriId, m.adresId, m.urunId, rid, adet: 2)));
            ilk.Item1.Should().Be(HttpStatusCode.Created, $"ilk siparis olusmali: {ilk.Item2.Message}");
            var ilkVeri = (ilk.Item2 as Divisima.Core.Utilities.Results.SuccessDataResult<OrderPlaceResponseDto>)!.Data;
            ilkVeri.replayed.Should().BeFalse("ILK cagri siparisi GERCEKTEN olusturdu");

            // (a) AYNI sepet -> idempotent 200 + AYNI siparis + replayed = true
            var tekrar = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(m.musteriId, m.adresId, m.urunId, rid, adet: 2)));
            tekrar.Item1.Should().Be(HttpStatusCode.OK, "ayni musteri + ayni sepet REPLAY olmali");
            var tekrarVeri = (tekrar.Item2 as Divisima.Core.Utilities.Results.SuccessDataResult<OrderPlaceResponseDto>)!.Data;
            tekrarVeri.id.Should().Be(ilkVeri.id, "replay MEVCUT siparisi dondurmeli");
            tekrarVeri.replayed.Should().BeTrue("bu cagri YENI siparis YAZMADI (T1-B7)");

            // (b) FARKLI sepet -> 400 (olcut sepeti de kapsar - GF-3/K12'nin uye yoluna tasinmis hali)
            var farkli = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(m.musteriId, m.adresId, m.urunId, rid, adet: 3)));
            farkli.Item1.Should().Be(HttpStatusCode.BadRequest,
                "ayni request_id FARKLI sepetle gelirse REDDEDILMELI");

            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == m.musteriId))
                .Should().Be(1, "uc cagriya ragmen TEK siparis olmali");
        }

        // ═════════════════════════════════════════════════════════════════════════════════════
        // K2 (D2) - ADRES ZORUNLU + SNAPSHOT · REPRO R-6.3
        // ═════════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM (T1-B2, LAUNCH BLOKER): `address_id` verilmezse sahiplik blogunun
        // TAMAMI atlaniyor ve siparis ADRESSIZ olusuyordu (canlida 15 satir - D-YAN).
        [Fact]
        public async Task R6_3a_ADRESSIZ_SIPARIS_400_DONER_ve_SIPARIS_OLUSMAZ()
        {
            if (Skipped()) return;
            var m = await KurguAsync();

            var dto = Istek(m.musteriId, m.adresId, m.urunId);
            dto.address_id = null;

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(dto));

            sonuc.Item1.Should().Be(HttpStatusCode.BadRequest, "adressiz siparis REDDEDILMELI");
            // CIFT-ANLAM KIRICI: 400 BASKA bir dogrulamadan degil, ADRESTEN gelmeli.
            sonuc.Item2.Message.Should().Be(Divisima.Core.Utilities.Constants.Messages.OrderAddressRequired);

            await using var ctx = NewContext();
            (await ctx.Set<Order>().AsNoTracking().CountAsync(o => o.customer_id == m.musteriId))
                .Should().Be(0, "reddedilen istek siparis YAZMAMALI");
        }

        // Sahiplik ihlali sozlesmesi (GF-1/K4) DEGISMEDI: 404 + `IdorAttempt` izi.
        [Fact]
        public async Task R6_3b_BASKASININ_ADRESI_404_DONER_ve_IdorAttempt_YAZILIR()
        {
            if (Skipped()) return;
            var a = await KurguAsync();
            var b = await KurguAsync();

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(b.musteriId, a.adresId, b.urunId)));   // B, A'nin adresini kullaniyor

            sonuc.Item1.Should().Be(HttpStatusCode.NotFound,
                "sahiplik ihlali 404 - 403 adresin VAR oldugunu ima ederdi (GF-1/K4)");

            await using var ctx = NewContext();
            var olaylar = await ctx.Set<SecurityEvent>().AsNoTracking()
                .Where(e => e.event_type == "IdorAttempt").ToListAsync();
            olaylar.Should().HaveCount(1, "sahiplik ihlali IZ birakmali (>= 1 pozitif olay)");
            olaylar[0].detail.Should().Contain("address", "olay ADRES kaynagini anmali");
        }

        // NEG KONTROL: VAR OLMAYAN adres 404 doner ama olay YAZILMAZ (GF-5/K2 karari -
        // yazim hatasi ile SALDIRI ayni sey degildir).
        [Fact]
        public async Task R6_3c_VAR_OLMAYAN_ADRES_404_ama_OLAY_YAZILMAZ()
        {
            if (Skipped()) return;
            var m = await KurguAsync();

            var dto = Istek(m.musteriId, m.adresId, m.urunId);
            dto.address_id = 999_000_000;

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>().PlaceOrder(dto));
            sonuc.Item1.Should().Be(HttpStatusCode.NotFound);

            await using var ctx = NewContext();
            (await ctx.Set<SecurityEvent>().AsNoTracking().CountAsync(e => e.event_type == "IdorAttempt"))
                .Should().Be(0, "var olmayan id'yi yoklamak SALDIRI DEGILDIR - gurultu uretilmemeli");
        }

        // OLCULEN ONCE-DURUM: `OrderSnapshot.shipping_address` SABIT `null` yaziliyordu -
        // siparisin GITTIGI adres hicbir yerde DONDURULMUYORDU.
        [Fact]
        public async Task K2_SNAPSHOT_TESLIMAT_ADRESINI_DONDURUR()
        {
            if (Skipped()) return;
            var m = await KurguAsync();

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(m.musteriId, m.adresId, m.urunId)));
            sonuc.Item1.Should().Be(HttpStatusCode.Created, $"siparis olusmali: {sonuc.Item2.Message}");

            await using var ctx = NewContext();
            var snap = await ctx.Set<OrderSnapshot>().AsNoTracking()
                .SingleAsync(s => s.customer_id == m.musteriId);

            snap.shipping_address.Should().NotBeNullOrWhiteSpace("adres snapshot'ta DONDURULMALI");
            snap.shipping_address!.Should().Contain("Test Mah. Test Sok. No 1",
                "cift-anlam kirici: alan DOLU olmakla kalmayip GERCEK adresi tasimali");
            snap.shipping_address!.Length.Should().BeLessThanOrEqualTo(500,
                "kolon nvarchar(500) - kirpma URETIM NOKTASINDA yapilmali (SD-7 ailesi)");
        }

        // ═════════════════════════════════════════════════════════════════════════════════════
        // K3 (D4) - ODEME YONTEMI TANIMLI KUMEDEN · REPRO R-6.5
        // ═════════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM (T1-B3): `payment_method` bir `byte` ve dogrulamasi YOKTU;
        // TANIMSIZ her deger sessizce ONLINE dalina dusuyordu.
        [Fact]
        public void R6_5_TANIMSIZ_ODEME_YONTEMI_VALIDATORDE_REDDEDILIR()
        {
            var dto = new OrderCreateRequestDto
            {
                customer_id = 1,
                address_id = 1,
                coupon_code = "",
                payment_method = 99,
                items = new List<OrderItemRequestDto> { new() { product_id = 1, quantity = 1, size = "M" } }
            };

            var hatalar = new Divisima.Bussiness.ValidationRules.FluentValidation
                .OrderCreateRequestValidator().Validate(dto).Errors;

            hatalar.Any(e => e.PropertyName == nameof(dto.payment_method))
                .Should().BeTrue("99 TANIMSIZ bir odeme yontemi - sessizce ONLINE'a DUSURULMEMELI");

            // CIFT-ANLAM KIRICI: tanimli UC deger GECMELI (yoksa "her sey reddediliyor" ile de yesil kalirdi).
            foreach (var gecerli in GirdiSinirlari.GecerliOdemeYontemleri)
            {
                dto.payment_method = gecerli;
                new Divisima.Bussiness.ValidationRules.FluentValidation.OrderCreateRequestValidator()
                    .Validate(dto).Errors
                    .Any(e => e.PropertyName == nameof(dto.payment_method))
                    .Should().BeFalse($"tanimli deger {gecerli} GECMELI");
            }
        }

        // K2'nin validator yarisi - adres kapisi UCTA da (binding oncesi) kapali.
        [Fact]
        public void K2_ADRES_KAPISI_VALIDATORDE_DE_VAR()
        {
            var dto = new OrderCreateRequestDto
            {
                customer_id = 1,
                address_id = null,
                coupon_code = "",
                payment_method = GirdiSinirlari.OdemeKapida,
                items = new List<OrderItemRequestDto> { new() { product_id = 1, quantity = 1, size = "M" } }
            };

            new Divisima.Bussiness.ValidationRules.FluentValidation.OrderCreateRequestValidator()
                .Validate(dto).Errors
                .Any(e => e.PropertyName == nameof(dto.address_id))
                .Should().BeTrue("adres UCTA da zorunlu olmali");

            dto.address_id = 5;
            new Divisima.Bussiness.ValidationRules.FluentValidation.OrderCreateRequestValidator()
                .Validate(dto).Errors
                .Any(e => e.PropertyName == nameof(dto.address_id))
                .Should().BeFalse("gecerli adres id'si GECMELI - kural ayirt edici olmali");
        }

        // ═════════════════════════════════════════════════════════════════════════════════════
        // K5 (D5) - DURUM YAZIMI TEK KAPIDAN
        // ═════════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM (T4-F5): `OrderStatusMachine` VARDI ama yalniz BIR yol ondan
        // geciyordu; kalan bes yazim yeri durumu DOGRUDAN atiyordu.
        //
        // ══ PIN SINIRI - MK-6 ILE OLCULDU, DURUST BEYAN ════════════════════════════════════
        // Bu pin `DurumYaz` kapisini AYIRT EDEMEZ: MUT-6'da kapi tumden devre disi birakildi
        // (`if (false)`) ve pin YESIL kaldi - cunku `ConfirmManualPayment`in kendi
        // IDEMPOTENSI kosulu ("status != Pending", BILINCLI olarak korundu) ayni istegi
        // zaten reddediyor. Yani burada olculen sey "terminal siparis manuel onaylanamaz"
        // DAVRANISIDIR ve o davranis GF-6 ONCESINDE de vardi - bu pin REGRESYON KORUMASIDIR.
        // `DurumYaz` kapisinin KENDI kaniti KAYNAK pinindedir
        // (`K5_DURUM_YAZIMI_TEK_KAPIDAN_GECER`, MUT-6b ile TAM 1 kirmizi).
        [Fact]
        public async Task K5_IPTAL_EDILMIS_HAVALE_SIPARISI_MANUEL_ONAYLANAMAZ()
        {
            if (Skipped()) return;
            var m = await KurguAsync();

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(m.musteriId, m.adresId, m.urunId, odeme: GirdiSinirlari.OdemeHavale)));
            sonuc.Item1.Should().Be(HttpStatusCode.Created, $"havale siparisi olusmali: {sonuc.Item2.Message}");

            int siparisId;
            await using (var ctx = NewContext())
            {
                var o = await ctx.Set<Order>().SingleAsync(x => x.customer_id == m.musteriId);
                siparisId = o.id;
                o.status.Should().Be((byte)OrderStatusEnum.Pending, "havale siparisi Pending DOGAR");
                o.status = (byte)OrderStatusEnum.Cancelled;   // operatör iptali
                await ctx.SaveChangesAsync();
            }

            var onay = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .ConfirmManualPayment(siparisId));
            onay.Item1.Should().Be(HttpStatusCode.BadRequest, "TERMINAL siparis manuel onaylanamaz");

            await using (var ctx = NewContext())
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == siparisId)).status
                    .Should().Be((byte)OrderStatusEnum.Cancelled, "durum DEGISMEMELI");
        }

        // CIFT-ANLAM KIRICI: Pending havale siparisi ONAYLANABILMELI.
        [Fact]
        public async Task K5_PENDING_HAVALE_SIPARISI_MANUEL_ONAYLANIR()
        {
            if (Skipped()) return;
            var m = await KurguAsync();

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .PlaceOrder(Istek(m.musteriId, m.adresId, m.urunId, odeme: GirdiSinirlari.OdemeHavale)));
            sonuc.Item1.Should().Be(HttpStatusCode.Created, $"havale siparisi olusmali: {sonuc.Item2.Message}");

            int siparisId;
            await using (var ctx = NewContext())
                siparisId = (await ctx.Set<Order>().AsNoTracking()
                    .SingleAsync(o => o.customer_id == m.musteriId)).id;

            var onay = await WithScopeAsync(sp => sp.GetRequiredService<IOrderService>()
                .ConfirmManualPayment(siparisId));
            onay.Item1.Should().Be(HttpStatusCode.OK, $"Pending havale ONAYLANMALI: {onay.Item2.Message}");

            await using (var ctx = NewContext())
                (await ctx.Set<Order>().AsNoTracking().SingleAsync(o => o.id == siparisId)).status
                    .Should().Be((byte)OrderStatusEnum.Confirmed);
        }

        // ═════════════════════════════════════════════════════════════════════════════════════
        // K6 (D6 · X-2) - HUB YETKISI · REPRO R-6.7
        // ═════════════════════════════════════════════════════════════════════════════════════

        // OLCULEN ONCE-DURUM: hub'in korumasi TEK KANALDI (sinif ozniteligi). Yol duzeyinde
        // isaret YOKTU; `MapControllers` kapisi hub'i KAPSAMAZ.
        [Fact]
        public async Task R6_7_HUB_ANONIM_BAGLANTIYI_REDDEDER()
        {
            if (Skipped()) return;
            using var client = _factory!.CreateClient();

            // SignalR negotiate anonim olarak DENENIR - kimliksiz istek gecmemelidir.
            var yanit = await client.PostAsync("/hubs/notification/negotiate?negotiateVersion=1", null);

            yanit.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden },
                "anonim istemci bildirim hub'ina baglanamamali");

            // ══ GF-6 / F4 - POZITIF KONTROL (rapor denetcisi bulgusu) ══════════════════════
            // Bu assert TEK BASINA "rota YOK" (404 -> 401'e donusmez ama yol kaldirilirsa
            // negotiate 404 verir ve BeOneOf FAIL eder) ya da "her sey reddediliyor" hallerini
            // AYIRT EDEMEZ. Kimlikli bir istemcinin GECEBILDIGI gosterilir: yol CALISIYOR ve
            // reddin sebebi KIMLIK YOKLUGUDUR.
            // TOKEN GERCEK UCLARDAN ALINIR (CLAUDE.md bolum 5): `TestAuthHelper` yeniden
            // KULLANILIR, yeniden yazilmaz - register/verify/login zincirinden gecer.
            var uye = await TestAuthHelper.CreateCustomerClientAsync(_factory!);
            var kimlikliYanit = await uye.Client.PostAsync("/hubs/notification/negotiate?negotiateVersion=1", null);

            kimlikliYanit.StatusCode.Should().Be(HttpStatusCode.OK,
                "kimlikli istemci baglanabilmeli - aksi halde yukaridaki ret 'rota kapali' demek olurdu");
            (await kimlikliYanit.Content.ReadAsStringAsync()).Should().Contain("connectionId",
                "negotiate yaniti gercek bir SignalR el sikismasi olmali");
        }

        // ═════════════════════════════════════════════════════════════════════════════════════
        // K7 (D7) - CSV ICE-AKTARIM · REPRO R-6.8
        // ═════════════════════════════════════════════════════════════════════════════════════

        private static string CsvBasligi =>
            "name,brand,category_id,price,sale_price,description,color_hex,product_type,size,qty\n";

        private static async Task<int> KategoriAsync()
        {
            await using var ctx = NewContext();
            var kat = new Category
            {
                name = "GF6 CSV " + Guid.NewGuid().ToString("N").Substring(0, 6),
                slug = "gf6csv-" + Guid.NewGuid().ToString("N"),
                is_active = true,
                created_at = DateTime.Now
            };
            ctx.Set<Category>().Add(kat);
            await ctx.SaveChangesAsync();
            return kat.id;
        }

        [Fact]
        public async Task R6_8a_SATIR_SINIRI_ASILIRSA_400_ve_HICBIR_URUN_EKLENMEZ()
        {
            if (Skipped()) return;
            var katId = await KategoriAsync();

            var onek = "GF6SATIR" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var sb = new System.Text.StringBuilder(CsvBasligi);
            for (var i = 0; i <= GirdiSinirlari.CsvSatirEnCok; i++)   // SINIR + 1 VERI satiri
                sb.Append($"{onek}-{i},Marka,{katId},100,,aciklama,#123456,0,M,5\n");

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(sb.ToString()));

            sonuc.Item1.Should().Be(HttpStatusCode.BadRequest, "satir siniri asilinca REDDEDILMELI");
            sonuc.Item2.Message.Should().Be(Divisima.Core.Utilities.Constants.Messages.ImportTooManyRows,
                "cift-anlam kirici: 400 SATIR SAYISINDAN gelmeli");

            await using var ctx = NewContext();
            (await ctx.Set<Product>().AsNoTracking().CountAsync(p => p.name.StartsWith(onek)))
                .Should().Be(0, "reddedilen dosyadan HICBIR urun yazilmamali");
        }

        // ══ GF-6 / F4 - SINIR ARTIK GERCEKTEN SINANIYOR ════════════════════════════════════
        //
        // ILK YAZIMDA bu test 3 satir kullaniyordu ve ADI ("TAM SINIR KADAR") YALAN SOYLUYORDU:
        // sinirin kendisi (5000) HIC denenmiyordu, yani `CsvSatirEnCok` 3'e dusurulse bile pin
        // YESIL kalirdi. Rapor denetcisi yakaladi.
        //
        // ARTIK TAM SINIR: 5000 VERI satiri GECER, 5001 REDDEDILIR - ayni testte, IKI KOSUM.
        // MALIYET DUSURULDU, OLCUT DEGISMEDI: 5000 satirin hepsi AYNI ad+marka ve BOS beden
        // tasir; uretim kodu satirlari `name|brand` ile grupladigi ve bos bedeni stok listesine
        // ALMADIGI icin DB'ye TEK urun yazilir. Yani olculen sey SATIR SAYISI KAPISIDIR,
        // yazma hacmi degil.
        [Fact]
        public async Task R6_8a_TAM_SINIR_GECER_BIR_FAZLASI_REDDEDILIR()
        {
            if (Skipped()) return;
            var katId = await KategoriAsync();
            var onek = "GF6SINIR" + Guid.NewGuid().ToString("N").Substring(0, 6);

            string Dosya(int veriSatiri)
            {
                var sb = new System.Text.StringBuilder(CsvBasligi);
                for (var i = 0; i < veriSatiri; i++)
                    sb.Append($"{onek},Marka,{katId},100,,aciklama,#123456,0,,5\n");   // beden BOS
                return sb.ToString();
            }

            // (a) TAM SINIR -> GECER
            var tam = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(Dosya(GirdiSinirlari.CsvSatirEnCok)));
            tam.Item1.Should().Be(HttpStatusCode.OK,
                $"TAM SINIR ({GirdiSinirlari.CsvSatirEnCok}) GECMELI: {tam.Item2.Message}");

            await using (var ctx = NewContext())
                (await ctx.Set<Product>().AsNoTracking().CountAsync(p => p.name == onek))
                    .Should().Be(1, "gruplama sonucu TEK urun yazilmali (>= 1 pozitif olay)");

            // (b) SINIR + 1 -> REDDEDILIR, ve ret sebebi SATIR SAYISI olmali (cift-anlam kirici)
            var fazla = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(Dosya(GirdiSinirlari.CsvSatirEnCok + 1)));
            fazla.Item1.Should().Be(HttpStatusCode.BadRequest,
                $"SINIR + 1 ({GirdiSinirlari.CsvSatirEnCok + 1}) REDDEDILMELI");
            fazla.Item2.Message.Should().Be(Divisima.Core.Utilities.Constants.Messages.ImportTooManyRows);
        }

        // ══ GF-6 / F4 - D7'NIN PINSIZ SEVK EDILEN UC KAPISI ════════════════════════════════
        // Rapor denetcisi tespit etti: marka uzunlugu, `.csv` uzanti kontrolu ve content-type
        // kontrolu SEVK EDILMIS ama PINLENMEMISTI.
        [Fact]
        public async Task K7_MARKA_UZUNLUGU_KOLON_SINIRINDA_KESILIR()
        {
            if (Skipped()) return;
            var katId = await KategoriAsync();

            var onek = "GF6MARKA" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var uzunMarka = new string('B', GirdiSinirlari.UrunMarkasi + 1);
            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(CsvBasligi + $"{onek},{uzunMarka},{katId},100,,aciklama,#123456,0,M,5\n"));

            sonuc.Item1.Should().Be(HttpStatusCode.BadRequest,
                "121 karakterlik marka EF insert-time 500 uretirdi (kolon 120) - GIRISTE reddedilmeli");
            sonuc.Item2.Message.Should().Contain("marka cok uzun");

            await using var ctx = NewContext();
            (await ctx.Set<Product>().AsNoTracking().CountAsync(p => p.name == onek)).Should().Be(0);

            // CIFT-ANLAM KIRICI: TAM SINIR kadar marka GECMELI.
            var tamMarka = new string('B', GirdiSinirlari.UrunMarkasi);
            var gecer = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(CsvBasligi + $"{onek}-ok,{tamMarka},{katId},100,,aciklama,#123456,0,M,5\n"));
            gecer.Item1.Should().Be(HttpStatusCode.OK, $"TAM SINIR marka GECMELI: {gecer.Item2.Message}");
        }

        [Fact]
        public void K7_UZANTI_ve_CONTENT_TYPE_KAPISI_UCTA_VAR()
        {
            var controller = KodSatirlari(Oku("Divisima.API/Controllers/ProductController.cs"));

            // UZANTI: karar `.csv` uzerinden ve ORDINAL-IGNORECASE (kimlik dizgesi - CLAUDE.md 6c).
            Sayim(controller, "string.Equals(uzanti, \".csv\", StringComparison.OrdinalIgnoreCase)")
                .Should().Be(1, "uzanti kapisi ORDINAL-IGNORECASE olmali - kulturlu casing YASAK");

            // CONTENT-TYPE: yalnizca ACIKCA CELISEN turleri reddeder (tarayicilar `.csv` icin
            // farkli turler gonderir - karar uzantidadir).
            Sayim(controller, "file.ContentType").Should().BeGreaterThan(0, "content-type SORULMALI");
            Sayim(controller, "Messages.ImportFileTypeInvalid").Should().Be(2,
                "iki kapi da AYNI mesaji dondurmeli - varlik/tur ayrimi sizmasin");

            // BOYUT: govde tavani + tek dosya boyutu AYRI iki kapidir, ikisi de olmali.
            Sayim(controller, "GirdiSinirlari.CsvDosyaEnBuyukBayt").Should().Be(1);
            Sayim(controller, "[RequestSizeLimit(5 * 1024 * 1024)]").Should().Be(1);
        }

        [Fact]
        public async Task R6_8b_FORMUL_HUCRESI_SATIRI_REDDEDER_ve_ICE_AKTARIMI_DURDURUR()
        {
            if (Skipped()) return;
            var katId = await KategoriAsync();

            var onek = "GF6FORM" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var csv = CsvBasligi
                + $"{onek}-1,Marka,{katId},100,,aciklama,#123456,0,M,5\n"
                + $"=HYPERLINK(\"http://kotu\"),Marka,{katId},100,,aciklama,#123456,0,M,5\n";

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(csv));

            sonuc.Item1.Should().Be(HttpStatusCode.BadRequest, "formul hucresi REDDEDILMELI");
            sonuc.Item2.Message.Should().Contain("formul karakteriyle",
                "cift-anlam kirici: ret FORMUL sebebinden gelmeli");

            // HEPSI-YA-DA-HICBIRI: ONCEKI gecerli satir da YAZILMAMALI.
            await using var ctx = NewContext();
            (await ctx.Set<Product>().AsNoTracking().CountAsync(p => p.name.StartsWith(onek)))
                .Should().Be(0, "tek bozuk satir TUM ice aktarimi durdurmali - KISMI katalog YOK");
        }

        [Fact]
        public async Task R6_8c_BOZUK_UCUNCU_SATIR_TUM_ICE_AKTARIMI_GERI_ALIR()
        {
            if (Skipped()) return;
            var katId = await KategoriAsync();

            var onek = "GF6KISMI" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var csv = CsvBasligi
                + $"{onek}-1,Marka,{katId},100,,aciklama,#123456,0,M,5\n"
                + $"{onek}-2,Marka,{katId},100,,aciklama,#123456,0,L,5\n"
                + $"{onek}-3,Marka,{katId},BOZUKFIYAT,,aciklama,#123456,0,M,5\n";

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(csv));

            sonuc.Item1.Should().Be(HttpStatusCode.BadRequest);
            sonuc.Item2.Message.Should().Contain("Satir 4", "hatanin SATIRI soylenmeli");

            await using var ctx = NewContext();
            (await ctx.Set<Product>().AsNoTracking().CountAsync(p => p.name.StartsWith(onek)))
                .Should().Be(0,
                    "ONCE-DURUM: ilk IKI satir yazilir, ucuncusu atlanirdi (KISMI katalog). ARTIK 0.");
        }

        // ═════════════════════════════════════════════════════════════════════════════════════
        // KAYNAK-SOZLESME PINLERI - PIN SINIRI ACIKCA BEYAN EDILIR
        // ═════════════════════════════════════════════════════════════════════════════════════

        private static string Oku(string goreliYol)
        {
            var dizin = new DirectoryInfo(AppContext.BaseDirectory);
            while (dizin != null && !File.Exists(Path.Combine(dizin.FullName, "Divisima-Backend.sln")))
                dizin = dizin.Parent;
            dizin.Should().NotBeNull("depo kokune ulasilmali");
            return File.ReadAllText(Path.Combine(dizin!.FullName, goreliYol.Replace('/', Path.DirectorySeparatorChar)));
        }

        // MK-8 / GF-5 dersi: kaynak-sozlesme pinleri YORUMSUZ metin uzerinde kosar - aranan
        // dizge onu ACIKLAYAN yorumda gecerse assert YANLIS atesler ya da BEDAVA dogru olur.
        private static string KodSatirlari(string metin) =>
            string.Join("\n", metin.Split('\n')
                .Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        private static int Sayim(string metin, string capa) =>
            metin.Split(capa).Length - 1;

        // ── K6 (D6) - PIN SINIRI: DAVRANIS PINI (R6_7) BU SATIRI AYIRT EDEMEZ ───────────────
        //
        // DURUST BEYAN: `NotificationHub` sinifi ZATEN `[Authorize]` tasiyor, dolayisiyla
        // anonim baglanti bu satir OLMADAN DA 401 alir - yani R6_7 bu degisiklige KOR'dur
        // (olculdu: MUT-K6'da R6_7 YESIL kaldi). Bu satirin kendi guvencesi "koruma IKI
        // KANALDA" olmasidir ve o ancak KAYNAKTAN pinlenebilir. MK-6 mutasyonu bu pin
        // uzerinde kosuldu ve TAM 1 isimli kirmizi verdi.
        [Fact]
        public void K6_HUB_YOLU_RequireAuthorization_ILE_ISARETLI()
        {
            var program = KodSatirlari(Oku("Divisima.API/Program.cs"));

            Sayim(program, "MapHub<NotificationHub>(\"/hubs/notification\").RequireAuthorization()")
                .Should().Be(1, "hub yolu YETKI ISARETI tasimali - `MapControllers` kapisi hub'i KAPSAMAZ");

            // NEG KONTROL: isaretsiz eski bicim KALMAMALI (yarim degisiklik iki satir birakirdi).
            program.Should().NotContain("MapHub<NotificationHub>(\"/hubs/notification\");",
                "isaretsiz eski cagri KALMAMALI");

            // Sinif ozniteligi de YERINDE - iki kanal BIRLIKTE guvence verir.
            KodSatirlari(Oku("Divisima.API/Hubs/NotificationHub.cs"))
                .Should().Contain("[Authorize]", "sinif ozniteligi KALDIRILMAMALI");
        }

        // ── K4 (D3 - ENVANTER KILITLEME) - PIN SINIRI: KAYNAK + POLITIKA DEGERI ─────────────
        //
        // DURUST BEYAN: canli 429 (R-6.9) bu sinifta KOSULMADI. Gerekce OLCULDU: test host'u
        // `RateLimit:AuthPermitLimit` gibi degerleri `TestHostConfig` uzerinden eziyor ve
        // yerlesik limiter test istemcisinde IP bazli bolunuyor - 11 istekle guvenilir bir
        // 429 uretmek, olculen seyi degil RIGI olcerdi. Pinlenen sey: ucun "payment" kovasina
        // BAGLANDIGI ve o kovanin limitinin TEK KAYNAKTAN geldigi.
        [Fact]
        public void K4_ORDER_PLACE_PAYMENT_KOVASINA_BAGLI()
        {
            var controller = KodSatirlari(Oku("Divisima.API/Controllers/OrderController.cs"));

            Sayim(controller, "[EnableRateLimiting(Divisima.Core.Security.RateLimiting.RateLimitPolitikasi.OdemeKapsami)]")
                .Should().Be(1, "order/place `payment` kovasina baglanmali - genel 100/dk kovasi DEGIL");

            // Kova adi SABITTEN gelir, dizge literalinden DEGIL (ayrisma engeli).
            Divisima.Core.Security.RateLimiting.RateLimitPolitikasi.OdemeKapsami
                .Should().Be("payment", "kova adi TEK KAYNAKTAN okunur");
        }

        // ── K7 (D7) - OZNITELIK TAVANI ILE SABIT AYRISMASIN ────────────────────────────────
        //
        // `[RequestSizeLimit]` DERLEME ZAMANI sabiti ister, yani `GirdiSinirlari`den
        // OKUYAMAZ - iki deger elle esitlenmis durumda. Bu pin, birinin degisip digerinin
        // KALMASINI yakalar. (Ilk yazimda kod yorumu "bu pin var" DIYORDU ama pin YOKTU -
        // YORUM != OLCUM ailesi; yorum ancak pin YAZILINCA dogru oldu.)
        [Fact]
        public void K7_ISTEK_TAVANI_ile_SABIT_AYRISMIYOR()
        {
            var controller = KodSatirlari(Oku("Divisima.API/Controllers/ProductController.cs"));

            Sayim(controller, "[RequestSizeLimit(5 * 1024 * 1024)]").Should().Be(1,
                "istek govdesi tavani oznitelikte SABIT olarak durur");
            GirdiSinirlari.CsvDosyaEnBuyukBayt.Should().Be(5L * 1024L * 1024L,
                "oznitelikteki deger ile merkezi sabit AYNI olmali - biri degisirse bu pin kirilir");
        }

        // ── K1 (D1) - KURAL TEK KAYNAKTA: IKINCI KOPYA ACILMADI ────────────────────────────
        //
        // "Ayni kuralin ikinci kopyasi" bu depoda YEDI kez bedeli odenmis bir ailedir. Bu pin
        // replay kuralinin TEK dosyada yasadigini ve iki cagiranin da ONU cagirdigini olcer.
        [Fact]
        public void K1_REPLAY_KURALI_TEK_KAYNAKTA_IKINCI_KOPYA_YOK()
        {
            var guard = KodSatirlari(Oku("Divisima.Bussiness/Concrete/SiparisReplayGuardi.cs"));
            var misafir = KodSatirlari(Oku("Divisima.Bussiness/Concrete/GuestCheckoutManager.cs"));
            var uye = KodSatirlari(Oku("Divisima.Bussiness/Concrete/OrderManager.cs"));

            // Sepet anahtari ve sahiplik yuklemi YALNIZ guard'da.
            Sayim(guard, "SepetAnahtari").Should().BeGreaterThan(0, "algoritma guard'da yasar");
            Sayim(misafir, "SepetAnahtari").Should().Be(0, "misafir sinifinda KOPYA KALMAMALI");
            Sayim(uye, "SepetAnahtari").Should().Be(0, "uye sinifinda KOPYA ACILMAMALI");

            // Iki cagiran da AYNI servisi, FARKLI eksenle cagirir.
            Sayim(misafir, "_replayGuardi.DegerlendirAsync").Should().Be(2,
                "misafir yolu guard'i IKI yerde cagirir (bas + tekil-indeks yarisi)");
            Sayim(uye, "_replayGuardi.DegerlendirAsync").Should().Be(2,
                "uye yolu guard'i IKI yerde cagirir (on kontrol + yaris dali)");
            Sayim(misafir, "ReplaySahiplik.EpostaIle").Should().BeGreaterThan(0, "misafir ekseni E-POSTA");
            Sayim(uye, "ReplaySahiplik.MusteriIdIle").Should().BeGreaterThan(0, "uye ekseni customer_id");

            // NEG KONTROL: eski sahipliksiz dedup dali GERI GELMEMELI.
            uye.Should().NotContain("var duplicate = await _orderDal.GetAsync(o => o.request_id == dto.request_id)",
                "sahipliksiz dedup GERI GELMEMELI (T1-B1'in ta kendisi)");
        }

        // ── K5 (D5) - DURUM YAZIMI TEK KAPIDAN (kaynak yuzu) ───────────────────────────────
        [Fact]
        public void K5_DURUM_YAZIMI_TEK_KAPIDAN_GECER()
        {
            var uye = KodSatirlari(Oku("Divisima.Bussiness/Concrete/OrderManager.cs"));

            // Dogrudan atama YALNIZ tek kapinin ICINDE kalir.
            Sayim(uye, "order.status = (byte)hedef;").Should().Be(1, "yazimin TEK yeri `DurumYaz` olmali");
            Sayim(uye, "order.status = (byte)OrderStatusEnum.").Should().Be(0,
                "manager govdesinde DOGRUDAN durum atamasi KALMAMALI");
            Sayim(uye, "order.status = (byte)dto.order_status;").Should().Be(0,
                "admin yolu da kapidan gecmeli");

            // Kapinin KENDISI makineye baglidir.
            Sayim(uye, "OrderStatusMachine.IsValidTransition").Should().BeGreaterThan(0);

            // Iyzico'nun IKI yazim yeri de makineden gecer.
            var iyzico = KodSatirlari(Oku("Divisima.Bussiness/Concrete/IyzicoPaymentManager.cs"));
            Sayim(iyzico, "OrderStatusMachine.IsValidTransition").Should().Be(2,
                "basarili VE basarisiz dallarin IKISI de terminal kapisini sormali");
            Sayim(iyzico, "\"PaymentAfterTerminal\"").Should().Be(2,
                "iki dal da KENDI olayini yazmali");

            // ══ GF-6 / F4 - KAPSAM GENISLETILDI: ShipmentManager DA TARANIR ═══════════════
            //
            // Rapor denetcisi bulgusu: bu pin YALNIZ `OrderManager`i tariyordu, oysa depo
            // genelinde `orders.status`a yazan IKI SITE DAHA var (ShipmentManager :65 Shipped,
            // :118 Delivered). Ikisi de GF-6 ONCESINDEN makine korumalidir - yani bu bir
            // GUVENLIK boslugu degil, PIN KAPSAMI boslugu idi. Kapatildi: guard KALDIRILIRSA
            // artik ISIMLI KIRMIZI gelir.
            var kargo = KodSatirlari(Oku("Divisima.Bussiness/Concrete/ShipmentManager.cs"));
            Sayim(kargo, "OrderStatusMachine.IsValidTransition").Should().Be(2,
                "kargo yolunun IKI durum yazimi da makineden gecmeli (Shipped ve Delivered)");

            // DEPO GENELI SAYIM: `order.status` dogrudan atamasi bu UC dosyanin DISINDA OLMAMALI.
            // (OrderManager 0 - hepsi `DurumYaz`dan gecer; Iyzico 2 ve Kargo 2 - dordu de
            // hemen ustunde makineye SORAR.)
            Sayim(iyzico, "order.status = (byte)OrderStatusEnum.").Should().Be(2);
            Sayim(kargo, "order.status = (byte)OrderStatusEnum.").Should().Be(2);
        }

        // ══ GF-6 / F1 (K4-DAR) - TAM MATRIS: IKI BICIM AYRISAMAZ ═══════════════════════════
        //
        // Kural IKI yerde yasiyor - ZORUNLU olarak: `PaidOrderSpec.IsPaid(byte,byte)` bellek ici
        // gercek, `OdenmisSiparisSpec.YalnizOdenmis()` ise EF'e cevrilebilir bicim (Core, Entity'yi
        // GOREMEZ - ters yon dongu olur). Bu pin ikisini GECERLI HER (durum x odeme turu) ciftinde
        // karsilastirir; biri degisip digeri kalirsa ISIMLI KIRMIZI gelir. "Ayni kuralin ikinci
        // kopyasi" ailesi burada bir SAYAC ile degil, bir TESTLE kapatilmistir.
        [Fact]
        public void F1_ODENMIS_OLCUTU_IKI_BICIMDE_de_AYNI()
        {
            var yuklem = Divisima.Entity.Specifications.OdenmisSiparisSpec.YalnizOdenmis().Compile();
            var durumlar = Enum.GetValues(typeof(OrderStatusEnum)).Cast<OrderStatusEnum>().ToList();
            var odemeTurleri = GirdiSinirlari.GecerliOdemeYontemleri;

            durumlar.Should().HaveCountGreaterThan(1, "matris BOS olmamali (vakum engeli)");
            odemeTurleri.Should().HaveCount(3);

            var karsilastirilan = 0;
            foreach (var d in durumlar)
                foreach (var t in odemeTurleri)
                {
                    var bellek = PaidOrderSpec.IsPaid((byte)d, t);
                    var ef = yuklem(new Order { status = (byte)d, payment_type = t });
                    ef.Should().Be(bellek, $"durum={d} odemeTuru={t}: iki bicim AYRISMAMALI");
                    karsilastirilan++;
                }
            karsilastirilan.Should().Be(durumlar.Count * 3, "matrisin TAMAMI karsilastirilmali");

            // KURALIN KENDISI (vakum engeli: yukaridaki dongu iki bicim de YANLIS olsa yesil kalirdi)
            PaidOrderSpec.IsPaid((byte)OrderStatusEnum.Confirmed, PaidOrderSpec.KapidaOdemeTuru)
                .Should().BeFalse("COD `Confirmed`da ODENMIS SAYILMAZ - T1-B4'un ta kendisi");
            PaidOrderSpec.IsPaid((byte)OrderStatusEnum.Delivered, PaidOrderSpec.KapidaOdemeTuru)
                .Should().BeTrue("COD `Delivered`da odenmis sayilir");
            PaidOrderSpec.IsPaid((byte)OrderStatusEnum.Confirmed, GirdiSinirlari.OdemeOnline)
                .Should().BeTrue("ONLINE yolun kurali DEGISMEDI");
            PaidOrderSpec.KapidaOdemeTuru.Should().Be(GirdiSinirlari.OdemeKapida,
                "`payment_type` ve `payment_method` kodlamalari AYNI olmali - ayrisirsa kural yanlis kapiya bakar");
        }

        // ══ GF-6 / F2 (S-1) - UCUNCU DURUMUN UCTAKI KARSILIGI ══════════════════════════════
        // PIN SINIRI: bu KAYNAK pinidir. Davranis yuzu `PaymentCallbackSecurityTests` icindeki
        // terminal pinlerinde (odeme Success + siparis Cancelled + Critical olay) olculur;
        // BURADA olculen sey, o dalin musteriye `success` DEGIL `review` olarak yansidigidir.
        [Fact]
        public void F2_TERMINAL_ODEME_UCUNCU_DURUM_review_OLARAK_YANSIR()
        {
            var iyzico = KodSatirlari(Oku("Divisima.Bussiness/Concrete/IyzicoPaymentManager.cs"));
            var controller = KodSatirlari(Oku("Divisima.API/Controllers/PaymentController.cs"));

            Sayim(iyzico, "Messages.PaymentReceivedOrderCancelled").Should().Be(1,
                "terminal + basarili dal AYRI mesajla donmeli");
            Sayim(controller, "Messages.PaymentReceivedOrderCancelled").Should().Be(1,
                "controller o mesaji TANIMALI - bag TEK SABIT uzerinden");
            Sayim(controller, "\"review\"").Should().Be(1, "ucuncu durum `review` olarak yonlendirilmeli");

            // SIRA SOZLESMESI: terminal kontrolu, basarili donusun USTUNDE olmali. Aksi halde
            // bayrak hicbir yaniti degistirmez - duzeltilen kusurun TA KENDISI.
            var terminalIdx = iyzico.IndexOf("if (terminalAtlandi && odemeGecerli)", StringComparison.Ordinal);
            var basariliIdx = iyzico.IndexOf("return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentSuccess));", StringComparison.Ordinal);
            terminalIdx.Should().BeGreaterThan(0, "terminal kontrolu VAR OLMALI");
            basariliIdx.Should().BeGreaterThan(0);
            terminalIdx.Should().BeLessThan(basariliIdx,
                "terminal kontrolu basarili donusten ONCE gelmeli - sira bu kusurun kokuydu");
        }

        // ══ GF-6 / F3 (T4-F1) - CIFT IADE KAPISI ═══════════════════════════════════════════
        // PIN SINIRI: yarisin KENDISI burada kosulmaz (K8 probuyla olculdu: kilitten once
        // 48 turda 41/35 cift iade, sonra 0). Bu pin korumanin YERINDE oldugunu sabitler:
        // kalem basina dagitik kilit + kilit ICINDE TAZE (no-tracking) okuma.
        [Fact]
        public void F3_IADE_KALEM_BASINA_KILITLI_ve_TAZE_OKUYOR()
        {
            var iade = KodSatirlari(Oku("Divisima.Bussiness/Concrete/ReturnManager.cs"));

            Sayim(iade, "_distributedLock.AcquireAsync(").Should().Be(1,
                "iade olusturma kalem basina SERILESTIRILMELI");
            Sayim(iade, "$\"return:{item.id}\"").Should().Be(1,
                "kilit anahtari ORDER_ITEM basina olmali - siparis ya da musteri basina DEGIL");
            // CLAUDE.md bolum 5: `GetListAsync` TRACKED'dir; kilitten sonra TAZE deger sart.
            Sayim(iade, "_returnDal.GetListNoTrackingAsync(").Should().Be(1,
                "kilit ICINDEKI okuma TRACKED olursa bayat deger okunur - koruma SESSIZCE olur");
            Sayim(iade, "_returnDal.GetListAsync(r => r.order_id == order.id").Should().Be(0,
                "eski TRACKED okuma KALMAMALI");

            var controller = KodSatirlari(Oku("Divisima.API/Controllers/ReturnController.cs"));
            Sayim(controller, "[Idempotency]").Should().Be(1,
                "PARA ucu: ag tekrari ikinci talep uretmemeli");
        }

        [Fact]
        public async Task K7_AD_UZUNLUGU_KOLON_SINIRINDA_KESILIR()
        {
            if (Skipped()) return;
            var katId = await KategoriAsync();

            var uzunAd = new string('A', GirdiSinirlari.UrunAdi + 1);
            var csv = CsvBasligi + $"{uzunAd},Marka,{katId},100,,aciklama,#123456,0,M,5\n";

            var sonuc = await WithScopeAsync(sp => sp.GetRequiredService<IProductService>()
                .ImportFromCsv(csv));

            sonuc.Item1.Should().Be(HttpStatusCode.BadRequest,
                "201 karakterlik ad EF insert-time 500 uretirdi (SD-7 ailesi) - GIRISTE reddedilmeli");
            sonuc.Item2.Message.Should().Contain("ad cok uzun");

            await using var ctx = NewContext();
            (await ctx.Set<Product>().AsNoTracking().CountAsync(p => p.name == uzunAd))
                .Should().Be(0);
        }
    }
}
