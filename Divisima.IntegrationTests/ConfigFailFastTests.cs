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
            public ProdFactory(string? callbackUrl) { _callbackUrl = callbackUrl; }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                TestHostConfig.Apply(builder);
                builder.UseEnvironment("Production");
                builder.UseSetting("MailSettings:Host", "smtp.test.local");
                builder.UseSetting("Encryption:Key", Convert.ToBase64String(new byte[32]));
                builder.UseSetting("TokenOptions:SecurityKey",
                    "divisima-uretim-ortami-pini-icin-uretilmis-uzun-imzalama-anahtari-0123456789");
                if (_callbackUrl != null) builder.UseSetting("Iyzico:CallbackUrl", _callbackUrl);

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
    }
}
