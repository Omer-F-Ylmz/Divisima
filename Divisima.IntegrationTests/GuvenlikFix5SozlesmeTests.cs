using System.Text.RegularExpressions;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Concrete;
using Divisima.Bussiness.ValidationRules.FluentValidation;
using Divisima.Core.Utilities.Http;
using Divisima.Core.Utilities.Text;
using Divisima.Core.Utilities.Validation;
using Divisima.Entity.Dtos.Auth;
using Divisima.Entity.Dtos.Guest;
using Divisima.Entity.Dtos.Order;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GF-5 / F2 - DALGANIN KALEMLERI ICIN PIN KAPSAMI ════════════════════════════════════
    //
    // NEDEN VAR (uc denetci de BAGIMSIZ olarak ayni seyi buldu): GF-5'in ilk halinde sekiz
    // kalemin YALNIZ IKISI (K2'nin 429 yarisi ve K9) davranis pini tasiyordu. `IpEnUzun`
    // yarin 64'e donse, `guest_name` siniri kalksa, `LogMetniMaskesi` sokulse HICBIR TEST
    // KIRILMAZDI - yani dalganin kapattigi LAUNCH BLOKER dahil cogu kalem REGRESYON KAPISIZDI.
    //
    // PIN TURU HER TESTTE ACIKCA YAZILIR (pin durustlugu): DAVRANIS pini uretim kodunu
    // CALISTIRIR; KAYNAK pini yalnizca metni tarar ve davranis kanitinin NEREDE oldugunu soyler.
    public class GuvenlikFix5SozlesmeTests
    {
        private static string KokDizin()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "Divisima-Backend.sln")))
                d = d.Parent;
            d.Should().NotBeNull("cozum kokü bulunmali");
            return d!.FullName;
        }

        private static string Oku(string goreliYol) => File.ReadAllText(Path.Combine(KokDizin(), goreliYol));

        // ══ CAPA KIRLENMESI KORUMASI (GF-4'un 2. CC hatasinin BIREBIR TEKRARI, GF-5'te) ═════
        // `NotContain("action == \"Added\"")` asserti ILK YAZIMDA KIRMIZI verdi: aranan dizge
        // uretim kodunda DEGIL, o kodu ACIKLAYAN YORUMDA geciyordu. Bu depoda ayni tuzaga
        // GF-4/K4'te de dusulmustu (`<clear />` asserti dosyanin kendi yorumuyla tatmin
        // oluyordu). Kaynak-sozlesmesi pinleri artik YORUMSUZ metin uzerinde kosar.
        private static string Yorumsuz(string kaynak)
        {
            var satirsiz = Regex.Replace(kaynak, @"^[ \t]*//.*$", "", RegexOptions.Multiline);
            return Regex.Replace(satirsiz, @"/\*.*?\*/", "", RegexOptions.Singleline);
        }

        private static string YorumsuzOku(string goreliYol) => Yorumsuz(Oku(goreliYol));

        // ─────────────────────────────────────────────────────────────────────────────────
        // K4 / SD-7 - LAUNCH BLOKER'IN DAVRANIS PINI
        // ─────────────────────────────────────────────────────────────────────────────────
        // DAVRANIS PINI: gercek validator calistirilir. AV-2'nin LAUNCH BLOKER'i buydu -
        // 151 karakterlik `guest_name` EF insert-time 500 uretiyor ve YETIM MUSTERI birakiyordu.
        [Theory]
        [InlineData(100, true)]    // TAM SINIR - gecmeli
        [InlineData(101, false)]   // SINIRIN BIR USTU - reddedilmeli
        [InlineData(151, false)]   // SD-7'nin BIREBIR canli degeri
        public void F2_K4_guest_name_UZUNLUK_KAPISI_SANITIZE_SONRASI(int uzunluk, bool gecmeli)
        {
            var dto = GecerliMisafir();
            dto.guest_name = new string('A', uzunluk);

            var sonuc = new GuestCheckoutValidator().Validate(dto);
            var adHatasi = sonuc.Errors.Any(e => e.PropertyName == nameof(dto.guest_name));

            adHatasi.Should().Be(!gecmeli,
                $"guest_name {uzunluk} karakter -> {(gecmeli ? "KABUL" : "RED")} beklenir "
                + $"(sinir {GirdiSinirlari.MusteriAdi}, kolon `addresses.full_name` 150)");
        }

        [Fact]
        public void F2_K4_guest_name_OLCUM_SANITIZE_SONRASI_YAPILIR()
        {
            // AYIRT EDICI: ham uzunluk 100'u ASIYOR ama tehlikeli etiket SOKULUNCE altina
            // duser. Olcum HAM uzerinden yapilsaydi bu girdi REDDEDILIRDI; DB'ye giden deger
            // ise rahatca sigiyor. Pin, olcumun DB'ye giden degere yapildigini kanitlar.
            var ad = new string('A', 90) + "<script>alert(1)</script>";
            ad.Length.Should().BeGreaterThan(GirdiSinirlari.MusteriAdi, "ham girdi siniri ASMALI");
            Divisima.Core.Utilities.Sanitization.InputSanitizer.Sanitize(ad).Length
                .Should().BeLessThanOrEqualTo(GirdiSinirlari.MusteriAdi, "sanitize SONRASI sigmali");

            var dto = GecerliMisafir();
            dto.guest_name = ad;
            new GuestCheckoutValidator().Validate(dto).Errors
                .Any(e => e.PropertyName == nameof(dto.guest_name))
                .Should().BeFalse("olcum SANITIZE SONRASI yapilir - ham uzunluk REDDETMEZ");
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K4 / D2 - request_id TASIYICI KAPISI (BICIM KAPISI DEGIL)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData("co-1757030000-ab12cd34", true)]   // frontend YEDEK dali - GUID DEGIL, GECMELI
        [InlineData("9f8e7d6c-1234-4abc-89de-000000000001", true)]  // GUID de gecmeli
        [InlineData("kisa.id_1", true)]
        [InlineData("co 123", false)]                  // BOSLUK - red
        [InlineData("co/123", false)]                  // EGIK CIZGI - red
        public void F2_K4_request_id_BICIM_DEGIL_TASIYICI_SINIRLAR(string rid, bool gecmeli)
        {
            var dto = GecerliMisafir();
            dto.request_id = rid;

            new GuestCheckoutValidator().Validate(dto).Errors
                .Any(e => e.PropertyName == nameof(dto.request_id))
                .Should().Be(!gecmeli,
                    "GUID SARTI YOKTUR - kapi yalnizca uzunluk ve karakter sinifi bakar "
                    + "(frontend'in `crypto.randomUUID` yedek dali GUID URETMEZ ve o dal PINLI)");
        }

        [Fact]
        public void F2_K4_request_id_UZUNLUK_SINIRI_KOLONLA_AYNI()
        {
            var dto = GecerliMisafir();

            dto.request_id = new string('a', GirdiSinirlari.RequestIdEnUzun);
            new GuestCheckoutValidator().Validate(dto).Errors
                .Any(e => e.PropertyName == nameof(dto.request_id))
                .Should().BeFalse("TAM SINIR (80) gecmeli - `orders.request_id` NVARCHAR(80)");

            dto.request_id = new string('a', GirdiSinirlari.RequestIdEnUzun + 1);
            new GuestCheckoutValidator().Validate(dto).Errors
                .Any(e => e.PropertyName == nameof(dto.request_id))
                .Should().BeTrue("81 karakter REDDEDILMELI - aksi halde EF insert-time 500");
        }

        [Fact]
        public void F2_K4_request_id_KAPISI_UYE_YOLUNDA_DA_VAR()
        {
            // AYNI KOLON, AYNI 500: kapiyi yalniz misafire koymak uye yolunu ACIK birakirdi.
            var uye = new OrderCreateRequestDto
            {
                customer_id = 1,
                coupon_code = "",
                request_id = new string('a', GirdiSinirlari.RequestIdEnUzun + 1),
                items = new List<OrderItemRequestDto> { new() { product_id = 1, quantity = 1, size = "M" } }
            };

            new OrderCreateRequestValidator().Validate(uye).Errors
                .Any(e => e.PropertyName == nameof(uye.request_id))
                .Should().BeTrue("uye yolunda da 81 karakter REDDEDILMELI");
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // F4 / C-2 - E-POSTA UZUNLUGU (iki yolda da)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Fact]
        public void F2_F4_eposta_UZUNLUK_KAPISI_MISAFIR_ve_UYE_YOLUNDA()
        {
            var uzun = new string('e', 190) + "@example.com";   // 202 - canli 500 ureten deger
            uzun.Length.Should().BeGreaterThan(GirdiSinirlari.EPosta);

            var misafir = GecerliMisafir();
            misafir.guest_email = uzun;
            new GuestCheckoutValidator().Validate(misafir).Errors
                .Any(e => e.PropertyName == nameof(misafir.guest_email))
                .Should().BeTrue("misafir yolunda 202 karakterlik e-posta REDDEDILMELI (once 500 uretiyordu)");

            var uye = new CustomerRegisterRequestDto
            { name = "Ad", email = uzun, phone = "5551112233", password = "Gecerli123" };
            new CustomerRegisterRequestValidator().Validate(uye).Errors
                .Any(e => e.PropertyName == nameof(uye.email))
                .Should().BeTrue("uye yolunda da REDDEDILMELI - kolon ayni, 500 ayni");

            // VAKUM KIRICI: TAM SINIR gecmeli, yoksa "hep reddediyor" ile de yesil kalirdi.
            var tamSinir = new string('e', GirdiSinirlari.EPosta - "@example.com".Length) + "@example.com";
            tamSinir.Length.Should().Be(GirdiSinirlari.EPosta);
            misafir.guest_email = tamSinir;
            new GuestCheckoutValidator().Validate(misafir).Errors
                .Any(e => e.PropertyName == nameof(misafir.guest_email))
                .Should().BeFalse("TAM SINIR (200) KABUL edilmeli");
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K1 - IP SINIRI KOLONDAN DAR OLMALI (semaya karsi KAYNAK pini)
        // ─────────────────────────────────────────────────────────────────────────────────
        // KAYNAK PINI (davranis DEGIL): `IpEnUzun` ile modeldeki kolon genisligi KARSILASTIRILIR.
        // Davranis kaniti canli rigde alindi (muhur); `WebApplicationFactory` `RemoteIpAddress`
        // uretmedigi icin bu rigde uctan uca olculemez - o sinir GF-1b/K6'da da kayitli.
        [Fact]
        public void F2_K1_IP_SINIRI_security_events_KOLONUNDAN_GENIS_OLAMAZ()
        {
            var ctx = Oku("Divisima.Dal/Concrete/Context/DivisimaDbContext.cs");

            var kolon = SecurityEventKolonGenisligi(ctx, "ip_address");
            kolon.Should().Be(60, "olcum capasi kaynaktan okundu - kolon degistiyse pin YENIDEN capalanmali");

            IstemciBilgisi.IpEnUzun.Should().BeLessThanOrEqualTo(kolon,
                "IP kirpma siniri kolondan GENIS olursa A09 izi yazilirken EF insert-time 500 "
                + "uretir - yani iz tutmaya calisirken ISTEK DUSER (`guest_name` ailesi)");

            // AYIRT EDICI: `user_sessions` kolonu 64'tur; secilen deger IKISINE DE sigmali.
            IstemciBilgisi.CihazEnUzun.Should().BeLessThanOrEqualTo(
                SecurityEventKolonGenisligi(ctx, "user_agent"),
                "user-agent kirpma siniri da kolondan genis olamaz");
        }

        [Fact]
        public void F2_F1_detay_SINIRI_KOLONLA_AYNI_ve_KIRPMA_URETIM_NOKTASINDA()
        {
            var ctx = Oku("Divisima.Dal/Concrete/Context/DivisimaDbContext.cs");
            SecurityEventManager.DetayEnUzun.Should().Be(SecurityEventKolonGenisligi(ctx, "detail"),
                "detay siniri kolonla AYNI olmali - BULGU-L3-1: 429 izinin detayi kullanici "
                + "kontrollu YOL tasiyor ve `SatirGuvenli` KIRPMAZ");

            // KAYNAK PINI: kirpma `LogAsync` ICINDE (tek nokta), cagri yerlerinde DEGIL.
            // YORUMSUZ metin uzerinde - capa kirlenmesi korumasi (bkz. `Yorumsuz`).
            var mgr = YorumsuzOku("Divisima.Bussiness/Concrete/SecurityEventManager.cs");
            mgr.Should().Contain("Substring(0, DetayEnUzun)",
                "kirpma URETIM NOKTASINDA olmali - K1'in IP icin kurdugu kalibin aynisi");
            mgr.Should().Contain("detail = izDetay",
                "DB'ye giden alan KIRPILMIS degeri almali, ham `detail`i DEGIL");
        }

        private static int SecurityEventKolonGenisligi(string dbContextKaynagi, string kolon)
        {
            // Capa HAM KAYNAKTAN: `b.Property(e => e.<kolon>)...HasMaxLength(N)`
            var m = Regex.Match(dbContextKaynagi,
                @"e\." + Regex.Escape(kolon) + @"\)\.HasColumnName\(""" + Regex.Escape(kolon) + @"""\)\.HasMaxLength\((\d+)\)");
            m.Success.Should().BeTrue($"`{kolon}` eslemesi DbContext'te bulunmali - bulunamiyorsa "
                + "esleme YENIDEN YAZILMIS demektir ve bu pin ARTIK OLCMUYOR");
            return int.Parse(m.Groups[1].Value);
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K6 - LOG METNI MASKESI (DAVRANIS)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Fact]
        public void F2_K6_SQL_TRUNCATE_DEGERI_MASKELENIR_TABLO_ve_KOLON_ADI_KORUNUR()
        {
            // Canli sizintinin BIREBIR bicimi (SQL Server 2628).
            const string ham = "String or binary data would be truncated in table 'DivisimaDb.dbo.customers', "
                + "column 'email'. Truncated value: 'eeeeeeeeeeeeeeeeeeeeeeeeeeeeee@example.com'.";

            var mask = LogMetniMaskesi.Maskele(ham)!;

            mask.Should().NotContain("eeeeeeeeeeeeeeeeeeeeeeeeeeeeee@example.com",
                "kirpilan DEGER (kullanici PII'si) log'da DURMAMALI");
            mask.Should().Contain("…", "kirpma isareti bulunmali");
            // TESHIS DEGERI KORUNUR - maskenin BEDELI olmamali:
            mask.Should().Contain("customers", "tablo adi GORUNUR kalmali");
            mask.Should().Contain("column 'email'", "kolon adi GORUNUR kalmali");
        }

        [Fact]
        public void F2_K6_EF_PARAMETRE_DOKUMU_MASKELENIR()
        {
            var mask = LogMetniMaskesi.Maskele("Executed DbCommand @p0='gizli-deger-1234567890', @p1='x'")!;

            mask.Should().NotContain("gizli-deger-1234567890", "parametre DEGERI maskelenmeli");
            mask.Should().Contain("@p0=", "parametre ADI gorunur kalmali");
        }

        [Fact]
        public void F2_K6_MASKE_ZARARSIZ_METNE_DOKUNMAZ()
        {
            // VAKUM/ASIRI-MASKELEME KIRICI: maske her seyi yutuyor olsaydi ustteki iki pin de
            // yesil kalirdi. Teshis metni AYNEN gecmeli.
            const string zararsiz = "Order 287 durumu Pending -> Confirmed";
            LogMetniMaskesi.Maskele(zararsiz).Should().Be(zararsiz,
                "maske teshis metnini DEGISTIRMEMELI");
        }

        [Fact]
        public void F2_K6_MASKELI_FORMATTER_HER_IKI_SINKE_BAGLI()
        {
            // KAYNAK PINI: davranis kaniti canli logda alindi (muhur); burada bagli OLDUGU pinlenir.
            var program = YorumsuzOku("Divisima.API/Program.cs");
            Regex.Matches(program, @"MaskeliFormatter").Count.Should().BeGreaterThanOrEqualTo(4,
                "Console ve File sink'lerinin IKISI de MaskeliFormatter almali (tip + sablon)");
            program.Should().Contain("WriteTo.Console(new Divisima.API.Logging.MaskeliFormatter");
            program.Should().Contain("new Divisima.API.Logging.MaskeliFormatter(Divisima.API.Logging.MaskeliFormatter.DosyaSablonu)");
        }

        [Fact]
        public void F2_K6_KanitMaskesi_OLCUTU_GENISLETILMEDI()
        {
            // BOZDUKLARIM kontrolu: K6 AYRI bir sinif acti; `KanitMaskesi`nin kendi olcutu
            // DEGISMEDI ve `KanitMaskesiTests` sozlesmesi (siparis numarasi dokunulmadan gecer)
            // AYAKTA kalmali.
            var km = YorumsuzOku("Divisima.Core/Utilities/Text/KanitMaskesi.cs");
            km.Should().Contain("char.IsLower", "olcutun kucuk-harf sarti KALDIRILMAMALI");
            LogMetniMaskesi.Maskele("DVS20260823-54740CC62D").Should().Be("DVS20260823-54740CC62D",
                "siparis numarasi DOKUNULMADAN gecmeli - `KanitMaskesiTests` ile ayni sozlesme");
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K3 - GECICI ANAHTAR OLCUTU (KAYNAK)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Fact]
        public void F2_K3_OLCUT_IsTemporary_OLMALI_action_Added_DEGIL()
        {
            // YORUMSUZ: `action == "Added"` dizgesi bu dosyanin KENDI YORUMUNDA geciyor ve
            // ham metin uzerinde kosulan bir `NotContain` ILK YAZIMDA yanlis kirmizi verdi.
            var i = YorumsuzOku("Divisima.Dal/Interceptors/AuditInterceptor.cs");

            i.Should().Contain("IsTemporary: true",
                "olcut EF'in GECICI ANAHTAR bayragi olmali - veritabaninin uretmedigi bir "
                + "anahtarla eklenen satir BOSUNA guncellenmesin");
            i.Should().Contain("override async ValueTask<int> SavedChangesAsync",
                "post-save kancasi bulunmali - K3'un dayandigi kanca budur");
            i.Should().Contain("override Task SaveChangesFailedAsync",
                "yazma duserse bekleyen liste TEMIZLENMELI");
            i.Should().Contain("_geciciAnahtarlilar.Clear()");
            // AYIRT EDICI: eski (yanlis) olcut GERI GELMEMELI.
            i.Should().NotContain("action == \"Added\"",
                "olcut EYLEM ADINA baglanirsa elle id verilen satirlar da guncellenirdi");
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K5 - MASKE ve MUSTERIYE GIDEN METIN (KAYNAK)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Fact]
        public void F2_K5_ODEME_JETONU_LOGA_MASKESIZ_GIRMEZ()
        {
            var p = YorumsuzOku("Divisima.Bussiness/Concrete/IyzicoPaymentManager.cs");

            p.Should().Contain("KanitMaskesi.Maskele(dto.token)",
                "odeme jetonu URETIM NOKTASINDA maskelenmeli (CLAUDE.md kalici kurali)");
            p.Should().NotContain("token={Token}\", \n", "bicim degisimi kacisi");
            Regex.Matches(p, @"token=\{Token\}").Count.Should().Be(1,
                "jeton yalniz TEK log satirinda anilir - ikinci bir maskesiz kopya acilmamali");
        }

        [Fact]
        public void F2_K5_SE3_MUSTERIYE_GIDEN_NOT_SABIT_METIN()
        {
            var o = YorumsuzOku("Divisima.Bussiness/Concrete/OrderConfirmationManager.cs");

            // `order_status_history.note` MUSTERIYE GORUNUR - saglayicinin ham metni GIRMEZ.
            o.Should().NotContain("({result.Message})",
                "saglayici hata metni musteriye gorunen nota GOMULMEMELI (47·GF-3·F1 sinifi)");
            o.Should().Contain("KanitMaskesi.SatirGuvenli(",
                "teknik ayrinti YALNIZ maskeli logda kalmali");
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K4 - TELAFI ATOMIKLIGI (KAYNAK)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Fact]
        public void F2_K4_TELAFI_TEK_TRANSACTION_ve_SARMALAMA_CATCH_ICINDE()
        {
            var g = YorumsuzOku("Divisima.Bussiness/Concrete/GuestCheckoutManager.cs");

            var i = g.IndexOf("private async Task MisafirKayitlariniTelafiSilAsync", StringComparison.Ordinal);
            i.Should().BeGreaterThanOrEqualTo(0, "telafi metodu bulunmali");
            var govde = g.Substring(i);

            var tx = govde.IndexOf("_unitOfWork.ExecuteInTransactionAsync", StringComparison.Ordinal);
            var cat = govde.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
            tx.Should().BeGreaterThanOrEqualTo(0, "iki silme TEK transaction'da olmali");
            cat.Should().BeGreaterThan(tx,
                "sarmalama `catch`in ICINDE olmali - DISARIDA olsaydi `ExecuteInTransactionAsync`in "
                + "yeniden firlattigi istisna cagirana cikar ve telafi hatasi MUSTERIYE 500 donerdi");

            // Silme sirasi FK'ya saygili kalmali: ONCE adres, SONRA musteri.
            govde.IndexOf("_addressDal.DeleteWhereAsync", StringComparison.Ordinal)
                .Should().BeLessThan(govde.IndexOf("_customerDal.DeleteWhereAsync", StringComparison.Ordinal));
        }

        // ─────────────────────────────────────────────────────────────────────────────────
        // K2 - SAHIPLIK IHLALI IZI (DAVRANIS) + CAGRI YERLERI (KAYNAK)
        // ─────────────────────────────────────────────────────────────────────────────────
        [Fact]
        public async Task F2_K2_SahiplikIhlali_IdorAttempt_YAZAR_ISTEK_SAHIBINI_ATFEDER()
        {
            var dal = new YakalayanOlayDal();
            var mgr = new SecurityEventManager(dal, new SessizBildirim(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityEventManager>.Instance);

            await mgr.SahiplikIhlaliAsync("order", 286, istekSahibi: 182);

            dal.Satirlar.Should().HaveCount(1, "olay GERCEKTEN yazilmali");
            var s = dal.Satirlar[0];
            s.event_type.Should().Be("IdorAttempt",
                "ad UYDURULMADI - `ops/serilog-siem.md` bu tipi ZATEN sayiyordu ama kod URETMIYORDU");
            s.severity.Should().Be("Warning", "ON KOSUL kimlikli oturum - Critical OLAMAZ");
            s.customer_id.Should().Be(182, "olay ISTEK SAHIBINI atfetmeli, kaynagin sahibini DEGIL");
            s.detail.Should().Be("order:286");
        }

        [Fact]
        public async Task F2_F1_detay_KOLON_GENISLIGINE_KIRPILIR()
        {
            var dal = new YakalayanOlayDal();
            var mgr = new SecurityEventManager(dal, new SessizBildirim(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityEventManager>.Instance);

            await mgr.LogAsync("RateLimitExceeded", "Warning", null, "::1", null,
                new string('y', SecurityEventManager.DetayEnUzun + 500));

            dal.Satirlar[0].detail!.Length.Should().Be(SecurityEventManager.DetayEnUzun,
                "detay kolon genisligine KIRPILMALI - aksi halde saldirgan yolunu uzatarak "
                + "KENDI 429 izini sessizce dusurebilirdi (BULGU-L3-1)");

            // VAKUM KIRICI: sinirin ALTINDAKI deger DOKUNULMADAN gecmeli.
            await mgr.LogAsync("Logout", "Info", 1, null, null, "kisa detay");
            dal.Satirlar[1].detail.Should().Be("kisa detay");
        }

        [Fact]
        public void F2_K2_OLAY_YAZAN_CAGRI_YERLERI_KAYNAKTA_DURUYOR()
        {
            // KAYNAK PINI: davranis kaniti canli rigde alindi (muhur, R-5.1..R-5.4, R-5.9).
            var auth = YorumsuzOku("Divisima.Bussiness/Concrete/AuthManager.cs");
            var order = YorumsuzOku("Divisima.Bussiness/Concrete/OrderManager.cs");
            var pay = YorumsuzOku("Divisima.Bussiness/Concrete/IyzicoPaymentManager.cs");
            var mw = YorumsuzOku("Divisima.API/Middlewares/RedisRateLimitMiddleware.cs");

            Regex.Matches(auth, @"""Logout"", ""Info""").Count.Should().Be(2,
                "cikisin IKI dali da (tek oturum / tum oturumlar) iz birakmali");
            auth.Should().Contain("Kayıtlı olmayan e-posta ile giriş denemesi");
            auth.Should().Contain("Kilitli hesaba giriş denemesi",
                "kilitli dal da olay yazmali - aksi halde iki dal arasinda ZAMANLAMA farki dogar "
                + "ve kapatilmis enumeration oracle'i YENIDEN acilir");
            order.Should().Contain("SahiplikIhlaliAsync(\"address\"");
            pay.Should().Contain("SahiplikIhlaliAsync(\"order\"");
            mw.Should().Contain("LogAsync(\"RateLimitExceeded\"");
            pay.Should().Contain("LogAsync(\"PaymentSignatureInvalid\"");
        }

        // ── yardimcilar ──────────────────────────────────────────────────────────────────
        private static GuestCheckoutDto GecerliMisafir() => new()
        {
            guest_name = "Gecerli Ad",
            guest_email = "gf5.pin@example.com",
            guest_phone = "5551112233",
            city = "Istanbul",
            district = "Kadikoy",
            full_address = "Test adres",
            zip_code = "34700",
            coupon_code = "",
            payment_method = 1,
            items = new List<OrderItemRequestDto> { new() { product_id = 1, quantity = 1, size = "M" } }
        };

        private sealed class SessizBildirim : Divisima.Core.Utilities.Notifications.INotificationService
        {
            public Task NotifyAdminsAsync(string message) => Task.CompletedTask;
            public Task NotifyCustomerAsync(int customerId, string message) => Task.CompletedTask;
        }

        // `SecurityEventManager`in DB'ye YAZDIGI satiri yakalar - davranis pinlerinin gozu.
        private sealed class YakalayanOlayDal : Divisima.DataAccess.Abstract.ISecurityEventDal
        {
            public List<Divisima.Entity.Entities.SecurityEvent> Satirlar { get; } = new();

            public Task AddAsync(Divisima.Entity.Entities.SecurityEvent entity)
            {
                Satirlar.Add(entity);
                return Task.CompletedTask;
            }

            public Divisima.Entity.Entities.SecurityEvent Get(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter) => throw new NotSupportedException();
            public List<Divisima.Entity.Entities.SecurityEvent> GetList(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null) => throw new NotSupportedException();
            public void Add(Divisima.Entity.Entities.SecurityEvent entity) => throw new NotSupportedException();
            public void Update(Divisima.Entity.Entities.SecurityEvent entity) => throw new NotSupportedException();
            public void Delete(Divisima.Entity.Entities.SecurityEvent entity) => throw new NotSupportedException();
            public Task<Divisima.Entity.Entities.SecurityEvent> GetAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter) => throw new NotSupportedException();
            public Task<List<Divisima.Entity.Entities.SecurityEvent>> GetListAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null) => throw new NotSupportedException();
            public Task<List<Divisima.Entity.Entities.SecurityEvent>> GetListNoTrackingAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null) => throw new NotSupportedException();
            public Task<Divisima.Core.Utilities.Dtos.PagedResult<Divisima.Entity.Entities.SecurityEvent>> GetPagedAsync(Divisima.Core.Utilities.Dtos.PagingRequestDto paging, System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null, System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, object>> orderBy = null, bool descending = false) => throw new NotSupportedException();
            public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null) => throw new NotSupportedException();
            public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null) => throw new NotSupportedException();
            public Task UpdateAsync(Divisima.Entity.Entities.SecurityEvent entity) => throw new NotSupportedException();
            public Task<int> DeleteWhereAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> predicate) => throw new NotSupportedException();
            public Task DeleteAsync(Divisima.Entity.Entities.SecurityEvent entity) => throw new NotSupportedException();
            public Task<Divisima.Entity.Entities.SecurityEvent> GetIgnoringFiltersAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter) => throw new NotSupportedException();
            public Task<List<Divisima.Entity.Entities.SecurityEvent>> GetListIgnoringFiltersAsync(System.Linq.Expressions.Expression<Func<Divisima.Entity.Entities.SecurityEvent, bool>> filter = null) => throw new NotSupportedException();
        }
    }
}
