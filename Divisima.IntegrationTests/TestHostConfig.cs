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
        }
    }
}
