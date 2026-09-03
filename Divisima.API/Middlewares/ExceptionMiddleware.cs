using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Global hata yakalama - RFC 7807 Problem Details formatında standart hata yanıtı.
    // Yakalanmayan tüm exception'ları tek noktada application/problem+json'a çevirir; stack trace sızmaz.
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Açıklayıcı yorum: Hatayı logla (detay sunucuda kalır), istemciye RFC 7807 problem dön
                //
                // GF-3/K2 - PII TASIYICI {Path} DEGIL, ISTISNA NESNESIDIR. AV-1/E-3 bu satiri
                // dogru isaretledi ama mekanizmasi baska: `Request.Path` bir `PathString`tir ve
                // SORGU DIZESINI ICERMEZ (o `Request.QueryString`), yani yoldan sizinti YOK.
                // Sizan sey `LogError(ex, ...)`in Serilog {Exception} alanina HAM yazdigi
                // ex.ToString()'tir: SmtpMailService once logaladigi istisnayi YUKARI FIRLATIR
                // (`throw;`) ve MailKit'in adres ayrisma istisnalari ALICI ADRESINI mesajlarinda
                // tasir. Bu yuzden nesne gecilmiyor, metni MASKEDEN gecirilip yaziliyor.
                // MALIYETI OLCULDU: 113 gercek yigin satirinda maskeye takilan parca YALNIZ 5
                // ve besi de derleyici uretimi ad (`c__DisplayClass28_0`, `2.GetListNoTrackingAsync`
                // gibi) - teshis degeri korunuyor.
                // RFC 7807 GOVDESI DEGISMEZ: yanit `HandleExceptionAsync`te uretiliyor, dokunulmadi.
                _logger.LogError("Beklenmeyen hata: {Path} | {Hata}", context.Request.Path,
                    Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(ex.ToString()));
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            // Açıklayıcı yorum: RFC 7807 - application/problem+json medya tipi
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // Açıklayıcı yorum: Korelasyon için traceId (log ile eşleştirme). İç detay gizli.
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            var problem = new
            {
                type = "https://httpstatuses.io/500",
                title = "Sunucu Hatası",
                status = (int)HttpStatusCode.InternalServerError,
                detail = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.",
                instance = context.Request.Path.Value,
                traceId
            };

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await context.Response.WriteAsync(json);
        }
    }
}
