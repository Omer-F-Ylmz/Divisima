using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Core.Utilities.Mail
{
    // Açıklayıcı yorum: IMailLinkBuilder'in tek uygulamasi. Sozlesme ve gerekceler arayuz
    // dosyasinda; burada yalnizca okuma + normalizasyon + gurultulu bos-durum var.
    public class MailLinkBuilder : IMailLinkBuilder
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MailLinkBuilder> _logger;

        public MailLinkBuilder(IConfiguration config, ILogger<MailLinkBuilder> logger)
        {
            _config = config;
            _logger = logger;
        }

        public string? VitrinBaglantisi(string hashYolu)
        {
            var taban = (_config["Storefront:BaseUrl"] ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(taban))
            {
                // GURULTULU: sessizce baglantisiz mail atmak, kullanicinin akisi tamamlayamamasi
                // demektir. Operator bunu logda GORMELI.
                _logger.LogError(
                    "MAIL BAGLANTISI URETILEMEDI: 'Storefront:BaseUrl' bos. Kullaniciya baglanti "
                    + "yerine yedek yonerge gidiyor. Istenen yol: {Yol}", hashYolu);
                return null;
            }
            var yol = hashYolu.StartsWith("/") ? hashYolu.Substring(1) : hashYolu;
            return taban + "/" + yol;
        }

        public string? ApiBaglantisi(string yolVeSorgu)
        {
            // Sprint 8 madde 10'daki dususu AYNEN koruyor: gorseller de API'nin wwwroot'undan
            // servis edildigi icin Storage:PublicBaseUrl ayni origin'dir.
            var taban = (_config["Api:PublicBaseUrl"] ?? _config["Storage:PublicBaseUrl"] ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(taban))
            {
                _logger.LogError(
                    "MAIL BAGLANTISI URETILEMEDI: 'Api:PublicBaseUrl' ve 'Storage:PublicBaseUrl' bos. "
                    + "Kullaniciya baglanti yerine yedek yonerge gidiyor. Istenen yol: {Yol}", yolVeSorgu);
                return null;
            }
            var yol = yolVeSorgu.StartsWith("/") ? yolVeSorgu : "/" + yolVeSorgu;
            return taban + yol;
        }
    }
}
