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

            public ProdFactory(string? callbackUrl, (string, string)? ezme = null)
            {
                _callbackUrl = callbackUrl;
                _ezme = ezme;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseEnvironment("Production");
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

        private static Exception? AcilisHatasi(string anahtar, string deger)
        {
            try
            {
                using var f = new ProdFactory(GecerliCallback, (anahtar, deger));
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
        [InlineData("Captcha:SecretKey", "CHANGE_ME")]
        [InlineData("TokenOptions:SecurityKey", "CHANGE_IN_PRODUCTION_uzun_bir_deger_0123456789012345")]
        public void Uretimde_HERHANGI_BIR_HASSAS_ANAHTAR_YER_TUTUCU_ISE_UYGULAMA_ACILMAZ(
            string anahtar, string yerTutucuDeger)
        {
            var hata = AcilisHatasi(anahtar, yerTutucuDeger);

            hata.Should().NotBeNull($"'{anahtar}' yer tutucu degerle uretime cikamaz");
            // CIFT-ANLAM KIRICI: acilis BASKA bir sebepten degil, TAM BU anahtardan durmali.
            hata!.ToString().Should().Contain(anahtar);
        }

        [Fact]
        public void Uretimde_DEPOYA_ISLENMIS_PUBLIC_JWT_DEGERI_ile_UYGULAMA_ACILMAZ()
        {
            // DEGER KAYNAGA YAZILMAZ - `docker-compose.yml`den OKUNUR. Bu ayni zamanda
            // DENY-LIST'IN GUNCELLIGINI de pinler: o dosyadaki deger degisip Program.cs'teki
            // SHA-256 ozeti guncellenmezse bu pin KIRILIR ve karar bilincli verilmek zorunda kalir.
            var satir = File.ReadAllLines(Path.Combine(KokDizin.Value, "docker-compose.yml"))
                .FirstOrDefault(s => s.Contains("TokenOptions__SecurityKey", StringComparison.Ordinal));
            satir.Should().NotBeNull("docker-compose.yml'de TokenOptions__SecurityKey satiri bulunmali");

            var deger = satir!.Substring(satir.IndexOf(':') + 1).Trim().Trim('"');
            deger.Should().NotBeNullOrWhiteSpace("vakum kirici: deger gercekten okunmus olmali");

            var hata = AcilisHatasi("TokenOptions:SecurityKey", deger);

            hata.Should().NotBeNull(
                "depoya islenmis - yani fiilen PUBLIC - bir imzalama anahtari uretimde kullanilamaz");
            hata!.ToString().Should().Contain("TokenOptions:SecurityKey");
            // Yer-tutucu dalindan DEGIL, deny-list dalindan durmali (iki dal ayrisiyor).
            hata.ToString().Should().Contain("public");
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
