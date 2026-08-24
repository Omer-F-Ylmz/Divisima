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
        }
    }
}
