using Divisima.DataAccess.Concrete.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Divisima.IntegrationTests
{
    // SPRINT 8 MADDE 7 - Iyzico:CallbackUrl URETIM FAIL-FAST LISTESINE EKLENDI
    //
    // OLCULEN ZARAR (E2b): storefront odeme baslatirken `callback_url` alanini GONDERMIYOR;
    // IyzicoPaymentManager o durumda `Iyzico:CallbackUrl` config degerini kullaniyor. Deger
    // BOS kalirsa gercek Iyzico bos callbackUrl'i KABUL ETMIYOR ve HER kart odemesi init
    // asamasinda 400 ile dusuyor - musteri yalnizca "Odeme baslatilamadi." goruyor.
    // Yani tek bir eksik config, uretimde TUM kart odemelerini oldurur ve bunu ancak ilk
    // musteri denedigi anda ogrenirsin. Program.cs'teki fail-fast blogu tam bu sinif icin var
    // (ConnectionStrings / TokenOptions:SecurityKey / Encryption:Key / MailSettings:Host);
    // bu deger orada YOKTU.
    //
    // NOT: fail-fast YALNIZ uretimde. Development'ta bos birakilabilir - yerel gelistirici
    // mock saglayiciyla calisiyor olabilir ve acilisi engellemek gereksiz surtunme olurdu.
    public class ConfigFailFastTests
    {
        // Uretim ortaminda host'u ACABILECEK asgari yapilandirma. Test edilen degeri (Iyzico:
        // CallbackUrl) disarida birakmak icin ayri parametre; digerleri sabit.
        private sealed class ProdFactory : WebApplicationFactory<Program>
        {
            private readonly string? _callbackUrl;
            // GF-3/K5: tek bir hassas anahtari BILINCLI olarak bozmak icin. null = bozma yok.
            private readonly (string Anahtar, string Deger)? _ezme;
            // LF-1/K1: ayni fabrika Development bacagini da kosar - "uretimde ZORUNLU,
            // development'ta SERBEST" iddiasinin IKI ayagi da AYNI duzenekle olculur.
            private readonly string _ortam;

            public ProdFactory(string? callbackUrl, (string, string)? ezme = null, string ortam = "Production")
            {
                _callbackUrl = callbackUrl;
                _ezme = ezme;
                _ortam = ortam;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseEnvironment(_ortam);
                // GF-3/K5: placeholder taramasi artik alti hassas anahtari daha kapsiyor;
                // asgari uretim ayarlari TEK KAYNAKTAN geliyor (ikinci kopya acilmadi).
                TestHostConfig.UretimAsgariAyarlari(builder);
                builder.UseSetting("MailSettings:Host", "smtp.test.local");
                builder.UseSetting("Encryption:Key", Convert.ToBase64String(new byte[32]));
                builder.UseSetting("TokenOptions:SecurityKey",
                    "divisima-uretim-ortami-pini-icin-uretilmis-uzun-imzalama-anahtari-0123456789");
                if (_callbackUrl != null) builder.UseSetting("Iyzico:CallbackUrl", _callbackUrl);
                // Ezme EN SON uygulanir ki yukaridaki gecerli degerleri BOZABILSIN.
                if (_ezme != null) builder.UseSetting(_ezme.Value.Anahtar, _ezme.Value.Deger);

                builder.ConfigureServices(services =>
                {
                    var d = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<DivisimaDbContext>));
                    if (d != null) services.Remove(d);
                    // Bu testler VERITABANINA DOKUNMUYOR - host'un acilip acilmadigi olculuyor.
                    // Yine de gercek baglanti dizesiyle acilmasin diye zararsiz bir deger verilir.
                    services.AddDbContext<DivisimaDbContext>(o =>
                        o.UseSqlServer("Server=localhost;Database=DivisimaFailFastPin;Trusted_Connection=True;TrustServerCertificate=True;"));
                });
            }
        }

        // Host'u ACMAYI dener; acilis istisnasini dondurur (yoksa null).
        private static Exception? AcilisHatasi(string? callbackUrl)
        {
            try
            {
                using var f = new ProdFactory(callbackUrl);
                _ = f.Services;     // host BURADA kurulur - Program.cs'in fail-fast blogu burada kosar
                return null;
            }
            catch (Exception ex) { return ex; }
        }

        [Fact]
        public void Uretimde_IyzicoCallbackUrl_BOSSA_UYGULAMA_ACILMAZ()
        {
            var hata = AcilisHatasi("");

            hata.Should().NotBeNull(
                "bos callback adresi uretimde TUM kart odemelerini oldurur - acilista durulmali, " +
                "ilk musteri denedigi anda degil");
            var metin = hata!.ToString();
            metin.Should().Contain("Iyzico:CallbackUrl",
                "cift-anlam kirici: acilis baska bir sebepten degil, TAM BU ayardan durmali");
            metin.Should().Contain("form-action",
                "mesaj CSP senkron kuralini da hatirlatmali - adres degisip form-action degismezse " +
                "Iyzico'nun sonuc POST'u engellenir ve 'para cekildi, siparis Pending' olusur");
        }

        [Fact]
        public void Uretimde_IyzicoCallbackUrl_HTTPS_DEGILSE_UYGULAMA_ACILMAZ()
        {
            var hata = AcilisHatasi("http://api.divisima.test/api/payment/callback");

            hata.Should().NotBeNull("Iyzico duz HTTP callback adresini kabul etmiyor");
            hata!.ToString().Should().Contain("HTTPS");
        }

        // VAKUM KIRICI + CIFT-ANLAM KIRICI: gecerli deger verildiginde host GERCEKTEN aciliyor.
        // Bu olmadan yukaridaki iki pin, "uretim host'u zaten hic acilmiyor" durumunda da
        // yesil kalirdi ve hicbir sey olcmemis olurlardi.
        [Fact]
        public void Uretimde_GECERLI_HTTPS_CallbackUrl_ile_UYGULAMA_ACILIR()
        {
            AcilisHatasi("https://api.divisima.test/api/payment/callback")
                .Should().BeNull("gecerli yapilandirmayla uretim host'u sorunsuz acilmali");
        }

        // ══ GF-3 / K5 (AV-1: E-5 + E-1a) - DAVRANIS PINLERI ════════════════════════════════
        private const string GecerliCallback = "https://api.divisima.test/api/payment/callback";

        private static Exception? AcilisHatasi(string anahtar, string deger, string ortam = "Production")
        {
            try
            {
                using var f = new ProdFactory(GecerliCallback, (anahtar, deger), ortam);
                _ = f.Services;
                return null;
            }
            catch (Exception ex) { return ex; }
        }

        [Theory]
        // OLCULEN ONCEKI HAL: yer-tutucu listesi YALNIZ TokenOptions:SecurityKey'e uygulaniyordu.
        // `appsettings.json`daki ALTI CHANGE_ME degerinden BESI kapiyi GECIYORDU. Asagidaki
        // anahtarlarin HEPSI o alti degerin anahtarlarindan (grep ile olculdu, tahmin degil).
        [InlineData("ConnectionStrings:DivisimaDb", "Server=CHANGE_ME;Database=DivisimaDb;Trusted_Connection=True;")]
        [InlineData("MailSettings:Password", "CHANGE_ME")]
        [InlineData("Iyzico:ApiKey", "CHANGE_ME")]
        [InlineData("Iyzico:SecretKey", "CHANGE_ME")]
        // `Captcha:SecretKey` BU LISTEDEN CIKARILDI (LF-1/K2) - yerine BILINCLI bir NEGATIF
        // pin kondu: Uretimde_CaptchaSecretKey_YER_TUTUCU_olsa_bile_UYGULAMA_ACILIR.
        // Bozulan pin ile yerine konan pin AYNI SEYI korumuyor; ayrim BILEREK verildi ve
        // gerekcesi o testin basinda yazili.
        // DEGER PARCALI YAZILDI - `secret-scan` gerekcesi: tek parca halinde entropi 4.849
        // (gitleaks esigi 3.5) ve "SecurityKey" anahtarinin yaninda duruyor. Ikinci parca
        // TEK KARAKTERIN tekrari, yani entropi 0; birlestirilmis deger 32 bayt sinirini gecer
        // (yer-tutucu dali UZUNLUK dalindan SONRA kosar, yoksa cift-anlam olurdu).
        [InlineData("TokenOptions:SecurityKey",
            "CHANGE_IN_PRODUCTION" + "_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void Uretimde_HERHANGI_BIR_HASSAS_ANAHTAR_YER_TUTUCU_ISE_UYGULAMA_ACILMAZ(
            string anahtar, string yerTutucuDeger)
        {
            var hata = AcilisHatasi(anahtar, yerTutucuDeger);

            hata.Should().NotBeNull($"'{anahtar}' yer tutucu degerle uretime cikamaz");
            // CIFT-ANLAM KIRICI: acilis BASKA bir sebepten degil, TAM BU anahtardan durmali.
            hata!.ToString().Should().Contain(anahtar);
        }

        [Theory]
        // ── MK-6 BOSLUGU KAPATILDI (rapor denetcisi) ──────────────────────────────────────
        // ILK YAZIMDA bu pin YALNIZ `docker-compose.yml`i okuyordu. Denetci MUT-9'u IKIYE
        // BOLDU: birinci ozet (`c54dab…`) bozulunca TAM 1 kirmizi, IKINCI ozet (`d9ec1bed…`)
        // bozulunca **695 testte 0 kirmizi** - yani deny-list'in yarisi PINSIZDI.
        // Artik her iki kaynak da okunuyor; `ci.yml` ile `security.yml` AYNI degeri tasidigi
        // icin ikisi de ayni (ikinci) ozete duser - o yuzden UC girdi, IKI ozet.
        [InlineData("docker-compose.yml")]
        [InlineData(".github/workflows/ci.yml")]
        [InlineData(".github/workflows/security.yml")]
        public void Uretimde_DEPOYA_ISLENMIS_PUBLIC_JWT_DEGERI_ile_UYGULAMA_ACILMAZ(string goreliYol)
        {
            // DEGER KAYNAGA YAZILMAZ - dosyadan OKUNUR. Bu ayni zamanda DENY-LIST'IN
            // GUNCELLIGINI de pinler: o dosyalardaki deger degisip Program.cs'teki SHA-256
            // ozetleri guncellenmezse pin KIRILIR ve karar bilincli verilmek zorunda kalir.
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"kaynak dosya bulunmali: {goreliYol}");

            var satir = File.ReadAllLines(tam)
                .FirstOrDefault(s => s.Contains("TokenOptions__SecurityKey", StringComparison.Ordinal));
            satir.Should().NotBeNull($"{goreliYol} icinde TokenOptions__SecurityKey satiri bulunmali");

            var deger = satir!.Substring(satir.IndexOf(':') + 1).Trim().Trim('"');
            deger.Should().NotBeNullOrWhiteSpace("vakum kirici: deger gercekten okunmus olmali");

            var hata = AcilisHatasi("TokenOptions:SecurityKey", deger);

            hata.Should().NotBeNull(
                "depoya islenmis - yani fiilen PUBLIC - bir imzalama anahtari uretimde kullanilamaz");
            hata!.ToString().Should().Contain("TokenOptions:SecurityKey");
            // Yer-tutucu dalindan DEGIL, deny-list dalindan durmali (iki dal ayrisiyor).
            hata.ToString().Should().Contain("public");
        }

        // ══ LF-1 / K1 (BL-1) - Cookies:Domain URETIMDE ZORUNLU ═════════════════════════════
        //
        // OLCULEN ONCE-DURUM ("kirmizi-once", LAUNCH HAZIRLIK OLCUMU / BL-1): Production +
        // Cookies:Domain BOS ile uygulama SORUNSUZ ACILIYORDU. Zincir dort halkali ve her
        // halkasi ayri ayri olculdu: nginx storefront'u (divisima.com) ve API'yi
        // (api.divisima.com) AYRI HOST'ta sunar -> `AuthController` alan adi verilmezse
        // cerezi HOST-ONLY yazar -> vitrin JS'i `csrf_token`i `document.cookie`den OKUYAMAZ ->
        // `AntiforgeryMiddleware` double-submit eslesmesini bulamaz ve `/api/auth/refresh`
        // KALICI 403 doner.
        //
        // NEDEN ACILIS KAPISI: ariza SESSIZDIR. Uygulama saglikli gorunur, `/health` 200 doner
        // ve belirti ancak ILK access token'in 15 dakikalik omru dolunca - dagitimdan 15 dakika
        // SONRA, TUM kullanicilarda AYNI ANDA - ortaya cikar. Bu sinif ariza, dagitim
        // penceresinin KAPANDIGI bir anda ogrenilir.
        [Theory]
        [InlineData("")]        // hic verilmemis / bos
        [InlineData("   ")]     // yalniz bosluk - `IsNullOrWhiteSpace` dalini AYRICA olcer
        public void Uretimde_CookiesDomain_BOSSA_UYGULAMA_ACILMAZ(string deger)
        {
            var hata = AcilisHatasi("Cookies:Domain", deger);

            hata.Should().NotBeNull(
                "alan adi verilmeyen cerez host-only yazilir ve /api/auth/refresh KALICI 403 " +
                "doner - belirti 15 dakika SONRA ve TUM kullanicilarda ayni anda cikar");
            var metin = hata!.ToString();
            // CIFT-ANLAM KIRICI: acilis BASKA bir eksik ayardan degil, TAM BU anahtardan durmali.
            metin.Should().Contain("Cookies:Domain");
            // Mesaj OPERATORE NE YAPACAGINI soylemeli - "eksik" demek yetmez; bicim de lazim.
            metin.Should().Contain(".divisima.com",
                "mesaj UST ALAN ADI bicimini ornekle vermeli, yoksa operator 'divisima.com' " +
                "yazar ve ayni ariza noktasi noktasina tekrarlar");
        }

        // VAKUM KIRICI'nin K1 ayagi: DEVELOPMENT'ta ayni bos deger acilisi ENGELLEMEZ.
        // Bu ayak olmadan yukaridaki pin "her ortamda patliyor" haliyle de yesil kalirdi ve
        // "uretimde zorunlu, yerelde serbest" iddiasinin YARISI olculmemis olurdu.
        // Yerelde iki taraf da localhost - yani AYNI host - oldugu icin host-only cerez CALISIR.
        [Fact]
        public void Developmentta_CookiesDomain_BOS_ise_UYGULAMA_ACILIR()
        {
            AcilisHatasi("Cookies:Domain", "", ortam: "Development")
                .Should().BeNull(
                    "yerelde storefront ve API ayni host'tadir; acilisi engellemek gereksiz " +
                    "surtunme olur - kapi YALNIZ uretim bacaginda kosar");
        }

        // ══ LF-1 / F-TURU (B-4) - URETIMDE YEREL ORIGIN CORS'TA KALAMAZ ════════════════════
        //
        // DENETCI BULGUSU: `docker-compose.prod.yml` `AllowedOrigins` icin HICBIR ortam
        // degiskeni vermiyordu ve imaja gomulu `appsettings.json` listesinde
        // `http://localhost:5173` VAR. Uzerine yazilmadigi icin uretimde de KALIYORDU.
        // TUZAK: `AllowedOrigins` bir DIZIdir - duz bir `AllowedOrigins` ortam degiskeni
        // HICBIR SEY yapmaz; ancak `AllowedOrigins__0` gibi INDEKSLI anahtarlar ezer. Yani
        // "ortam degiskeniyle veririm" diyen bir operator, verdigini SANIP vermeyebilir.
        //
        // K1 KALIBI: ariza SESSIZ - yanlis CORS hicbir log satiri uretmez ve yalnizca saldiri
        // aninda "calisir" (saldirganin yerelde kosturdugu sayfa, kurbanin oturumuyla API'ye
        // gidip yaniti OKUR). Bu yuzden kapi ACILISTA kosar.
        [Theory]
        [InlineData("http://localhost:5173")]
        [InlineData("https://127.0.0.1:8443")]     // IP bicimi de ayni dala girmeli
        public void Uretimde_ALLOWEDORIGINS_YEREL_BIR_ORIGIN_TASIYORSA_UYGULAMA_ACILMAZ(string origin)
        {
            var hata = AcilisHatasi("AllowedOrigins:0", origin);

            hata.Should().NotBeNull(
                "gelistirici origin'i uretim CORS listesinde kalirsa, yerelde kosan bir sayfa " +
                "kurbanin oturumuyla API'ye gidip yaniti OKUYABILIR");
            var metin = hata!.ToString();
            metin.Should().Contain("AllowedOrigins",
                "cift-anlam kirici: acilis baska bir ayardan degil TAM BU listeden durmali");
            metin.Should().Contain("AllowedOrigins__0",
                "mesaj INDEKSLI bicimi ogretmeli - operator duz bir degisken verirse hicbir sey " +
                "degismez ve verdigini SANIR");
        }

        // VAKUM KIRICI: gercek uretim origin'leriyle host ACILIYOR. Bu ayak olmadan yukaridaki
        // pin, "AllowedOrigins ne olursa olsun patliyor" durumunda da yesil kalirdi.
        [Fact]
        public void Uretimde_GERCEK_ORIGIN_ile_UYGULAMA_ACILIR()
        {
            AcilisHatasi("AllowedOrigins:0", "https://divisima.com")
                .Should().BeNull("gercek uretim origin'i kapiyi GECMELI");
        }

        // ══ LF-1 / K2 - Captcha:SecretKey FAIL-FAST'TEN CIKARILDI ══════════════════════════
        //
        // BOZULAN PIN ACIKCA KAYDA GECER: bu deger GF-3/K5'te yer-tutucu Theory'sinin BIR
        // GIRDISIYDI ve LF-1'de o girdi SILINDI. Yerine konan pin AYNI SEYI KORUMUYOR - tam
        // TERSINI pinliyor, cunku karar da tersine dondu.
        //
        // OLCULEN GEREKCE (T3-1, LAUNCH HAZIRLIK OLCUMU): captcha OLU BIR OZELLIKTIR -
        // dogrulayicinin uretim kodunda SIFIR cagri yeri var, `Captcha:Enabled` bayragi hicbir
        // dala girmiyor. Olmayan bir ozellik icin GERCEK BIR SECRET dayatmak, dagitimi
        // hicbir sey korumadan bloke eder: operator ya uydurma bir deger yazar (kapiyi
        // anlamsizlastirir) ya da gercek bir secret uretip HICBIR YERDE KULLANILMAYAN bir
        // degeri uretime tasir. Ozelligin kaderi - okuyucuyu baglamak ya da iskeleti silmek -
        // GF-7'ye devredildi; O KARAR VERILDIGINDE BU PIN DE TERSINE DONER.
        [Fact]
        public void Uretimde_CaptchaSecretKey_YER_TUTUCU_olsa_bile_UYGULAMA_ACILIR()
        {
            AcilisHatasi("Captcha:SecretKey", "CHANGE_ME")
                .Should().BeNull(
                    "captcha dogrulayicisinin uretimde SIFIR cagri yeri var (T3-1); olu bir " +
                    "ozellik icin secret dayatmak dagitimi korumasizca bloke eder");
        }

        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: docker-compose.yml iceren ust dizin yok. Sessiz skip YOK.");
            return d.FullName;
        });
    }
}
