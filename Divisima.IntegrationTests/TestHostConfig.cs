using Microsoft.AspNetCore.Hosting;

namespace Divisima.IntegrationTests
{
    // Açıklayıcı yorum: TÜM test host'larının paylaştığı ayar geçersiz kılmaları.
    //
    // Neden gerekli: SmtpMailService artık gerçek gönderim yapıyor ve BAŞARISIZLIKTA İSTİSNA
    // FIRLATIYOR (sözleşme böyle - çağıranların telafi mantığı ancak istisna görürse çalışır).
    // Geliştirici makinesindeki appsettings.Development.json'da MailSettings:Host genelde
    // "smtp.example.com" gibi ERİŞİLEMEZ bir yer tutucu; bu da /api/auth/register çağrısını
    // 500'e düşürüyor ve TestAuthHelper kullanan HER test sınıfını çökertiyordu.
    //
    // Host'u boşaltmak servisi "gönderme + uyarı logla" dalına sokar. Testler zaten SMTP
    // TESLİMATINI ölçmüyor; mail sözleşmesi (başarı -> outbox işlendi, hata -> istisna + telafi)
    // MailDeliveryContractTests'te SAHTE bir IMailService ile ayrıca pinleniyor.
    internal static class TestHostConfig
    {
        public static void Apply(IWebHostBuilder builder)
        {
            builder.UseSetting("MailSettings:Host", "");

            // ══ DALGA C / C3 - GELISTIRICI SECRET'LARI TEST HOST'UNA SIZIYORDU ═══════════════
            //
            // OLCULDU (bu pin yazilirken ORTAYA CIKTI): WebApplicationFactory<Program> uygulamanin
            // TAM yapilandirmasini yukler - Development ortaminda USER-SECRETS DAHIL. Bu makinede
            // `dotnet user-secrets list` ciktisinda AdminSeed:Enabled=true (+ e-posta/sifre) VARDI,
            // dolayisiyla HER test host'u acilirken o testin veritabanina BEKLENMEYEN bir admin
            // satiri yaziyordu.
            //
            // Zarari iki yonlu: (a) "admin sayisi" olcen bir pin yerelde KIRMIZI, CI'da (secret yok)
            // YESIL olur - yani sonuc MAKINEYE gore degisir; (b) tersi de mumkun: bir yetki pini
            // hazir bulunan admin yuzunden YANLIS SEBEPTEN yesil kalabilir.
            //
            // Varsayilan KAPALI'ya cekiliyor. Tohumlamayi OLCEN testler (DalgaCYayinAltyapisiTests)
            // bayragi KENDILERI aciyor - `UseSetting` daha SONRA cagrildigi icin oradaki deger
            // kazanir. Boylece davranis makinedeki secret'lardan BAGIMSIZ hale gelir.
            builder.UseSetting("AdminSeed:Enabled", "false");

            // ══ DALGA D / D1 - TEST YUKLEMELERI DEPOYU KIRLETMESIN ═══════════════════════════
            //
            // OLCULDU: her kosum `Divisima.API/wwwroot/uploads/products` altina 64 baytlik sahte
            // PNG'ler birakiyordu (olcum aninda 96 dosya) - hicbirinin veritabaninda karsiligi
            // YOK. Bu bir hata DEGIL, eksikti: Sprint 8 madde 4 LocalImageStorage'i DOGRU sekilde
            // WebRootPath'e tasidi; test host'unun ContentRoot'u Divisima.API oldugu icin de
            // WebRoot dogal olarak deponun kendi wwwroot'u oluyordu.
            //
            // Test host'u artik UCUNCU bir koke yaziyor (gecici dizin).
            // SPRINT 8 MADDE 4'UN PINI ZAYIFLAMIYOR, GUCLENIYOR: yazma ile sunum HALA ayni
            // kokten turuyor - yalnizca o kok ne CWD ne de ContentRoot. Yani "yazma ve sunum
            // ortusur" iddiasi bagimsiz bir dizinde de kanitlanmis oluyor.
            // `UseContentRoot(Directory.GetCurrentDirectory())` GERI GELMEDI (bkz. AdminFactory).
            builder.UseWebRoot(TestWebRoot.Yol);

            // ══ DALGA D - ARKA PLAN ISLERI TEST HOST'LARINDA KAPALI ═════════════════════════
            //
            // OLCULEN ZARAR (CI kirmizisi cd51a52): `AddHangfireServer()` ve recurring job
            // kayitlari KOSULSUZDU; her test host'u bir Hangfire sunucusu calistirip
            // "outbox-processor" isini DAKIKADA BIR kosuyordu. Bir test kendi drenajini yapip
            // `retry_count == 1` beklerken arka plan isi araya girip 2 yapabiliyordu.
            // CI'da birebir goruldu: PaymentCallbackSecurityTests.YanEtkiHatasi_... -> "found 2".
            //
            // YARIS ONCEDEN VARDI, YALNIZCA GORUNMUYORDU: dakikalik bir is ancak host YETERINCE
            // UZUN yasarsa atesler. Yerelde suit 1 dk 20 sn, CI'da (soguk SQL konteyneri) daha
            // uzun; ustelik bu dalgada iki yeni test SINIFI eklendi. Yani "benim degisikligim
            // kirdi" DEGIL, "benim degisikligim ORTAYA CIKARDI" - ama sonuc ayni: CI kirmizi.
            // CLAUDE.md'de kaydi olan ISIMSIZ FLAKE'lerin de en olasi aciklamasi budur.
            //
            // AYRICA: Hangfire depolamasi `ConnectionStrings:DivisimaDb`e bagli - yani her test
            // host'u GELISTIRICININ veritabanina recurring job tanimi yaziyordu.
            //
            // Testler arka plan zamanlamasina DAYANMIYOR: outbox'i olcen her test isleyiciyi
            // KENDISI cagiriyor (`OutboxProcessor.ProcessPendingAsync`). Yani kapatmak hicbir
            // testin olctugu seyi kaldirmaz - yalnizca YARISI kaldirir.
            builder.UseSetting("BackgroundJobs:Enabled", "false");
        }

        // ══ GF-3 / K5 - URETIM BACAGINI ACABILECEK ASGARI YAPILANDIRMA ═════════════════════
        //
        // NEDEN AYRI METOT: `Production` ortamini ayaga kaldiran IKI fikstur var
        // (`ConfigFailFastTests.ProdFactory` ve `RefreshCookieContractTests`) ve ikisi de ayni
        // asgari ayar kumesini tasiyor. K5 o kumeyi BUYUTTU (placeholder taramasi artik alti
        // hassas anahtari daha kapsiyor); listeyi iki dosyaya KOPYALAMAK "ayni kuralin ikinci
        // kopyasi" ailesinin yeni bir vakasi olurdu. Tek kaynak burasi.
        //
        // DEGERLER KURGUDUR ve YER TUTUCU DEGILDIR - kapinin reddettigi dizgelerin
        // ("CHANGE_ME", "TODO", "your-", "placeholder", "xxxxx", "CHANGE_IN_PRODUCTION")
        // hicbirini icermezler. Sir DEGILLER: hicbiri gercek bir saglayiciya karsi gecerli
        // degil; amaclari yalnizca host'un ACILMASI.
        //
        // NOT: `Encryption:Key` ve `TokenOptions:SecurityKey` cagiran fiksturlerde ZATEN
        // veriliyor ve orada KALIYOR - o iki deger testin KENDI konusuna (32 bayt / uzunluk)
        // dokunuyor, buraya tasinsa testin ne olctugu okunmaz hale gelirdi.
        public static void UretimAsgariAyarlari(IWebHostBuilder builder)
        {
            // appsettings.json'daki "Server=CHANGE_ME;..." K5'ten SONRA kapiya TAKILIR.
            builder.UseSetting("ConnectionStrings:DivisimaDb",
                "Server=localhost;Database=DivisimaUretimBacagiPin;Trusted_Connection=True;TrustServerCertificate=True;");
            builder.UseSetting("MailSettings:Password", "kurgu-smtp-parolasi-0123456789");
            builder.UseSetting("Iyzico:ApiKey", "kurgu-iyzico-api-anahtari-0123456789");
            builder.UseSetting("Iyzico:SecretKey", "kurgu-iyzico-gizli-anahtari-0123456789");
            builder.UseSetting("Captcha:SecretKey", "kurgu-captcha-gizli-anahtari-0123456789");
        }
    }
}
