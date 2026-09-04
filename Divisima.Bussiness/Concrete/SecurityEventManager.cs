using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Http;
using Divisima.Core.Utilities.Notifications;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Güvenlik olay yöneticisi. DB'ye yazar, structured log'a düşer, Critical ise admin'e bildirir.
    public class SecurityEventManager : ISecurityEventService
    {
        private readonly ISecurityEventDal _dal;
        private readonly INotificationService _notification;
        private readonly ILogger<SecurityEventManager> _logger;
        // ══ GF-5 / K1 - IZ ARTIK "NEREDEN" SORUSUNU YANITLIYOR ═════════════════════════════
        //
        // OLCULEN ONCE-DURUM (AV-2 / SC-1, LAUNCH BLOKER): `LogAsync` ALTI arguman aliyor ve
        // 4./5. arguman (ip, userAgent) YEDI CAGRI YERININ YEDISINDE DE `null, null` gecilyordu.
        // Canli tabloda 40 satirin 40'inda ikisi de NULL (olculdu). Sonuc: `ops/serilog-siem.md`
        // deki BES alarm kuralindan IP tabanli olanlar VERI TEMELI OLMADIGI icin kosulamiyordu -
        // "ayni IP'den 10 basarisiz login" gibi en temel kural bile YAZILAMAZDI.
        //
        // NEDEN BURADA, CAGRI YERLERINDE DEGIL (merkez karari, GF-5 / D8): degerleri yedi cagri
        // yerine tasimak (a) `LogAsync` imzasini ya da cagri bicimlerini degistirir, (b) `AccountManager`a
        // `IHttpContextAccessor` enjekte etmeyi gerektirirdi - oysa `SecureControllerBase.cs:22-27`
        // "is katmani HTTP baglamini GORMEZ (bilincli sinir)" diyor ve o sinir bugune kadar
        // YALNIZ BIR KEZ, gerekcesi yazilarak delinmisti (AuthManager, GF-1b/K6). Ikinci bir
        // istisna acmak yerine okuma TEK NOKTAYA - olayin YAZILDIGI yere - alindi.
        // IMZA DEGISMEDI, YEDI CAGRI YERI DEGISMEDI.
        //
        // NULLABLE + VARSAYILANLI (GF-1b/K6 kalibi): arka plan isi ya da birim testi bu servisi
        // HttpContext olmadan cozerse alanlar sessizce null kalir - akis BOZULMAZ.
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public SecurityEventManager(ISecurityEventDal dal, INotificationService notification,
            ILogger<SecurityEventManager> logger, IHttpContextAccessor? httpContextAccessor = null)
        {
            _dal = dal;
            _notification = notification;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string eventType, string severity, int? customerId, string? ip, string? userAgent, string? detail)
        {
            // CAGIRAN DEGER VERDIYSE O KAZANIR: bugun yedi cagri yerinin yedisi de null geciyor,
            // ama ileride bir cagri yeri (ornegin bir middleware, K2) HttpContext'i ELINDE
            // tutmayan bir noktadan cagirirsa kendi olctugu degeri gecirebilsin. Doldurma
            // YALNIZCA bosluk doldurur, hicbir zaman UZERINE YAZMAZ.
            var izIp = ip ?? IstemciBilgisi.Ip(_httpContextAccessor);
            var izCihaz = userAgent ?? IstemciBilgisi.UserAgent(_httpContextAccessor);

            await _dal.AddAsync(new SecurityEvent
            {
                event_type = eventType,
                severity = severity,
                customer_id = customerId,
                ip_address = izIp,
                user_agent = izCihaz,
                detail = detail,
                created_at = DateTime.Now
            });
            // Açıklayıcı yorum: Structured log (Serilog -> SIEM'e akıtılabilir)
            _logger.LogWarning("SECURITY {EventType} {Severity} customer={CustomerId} ip={Ip} {Detail}",
                eventType, severity, customerId, izIp, detail);
            // Açıklayıcı yorum: Kritik olayda admin'e anlık bildirim
            if (severity == "Critical")
                await _notification.NotifyAdminsAsync($"[GÜVENLİK] {eventType}: {detail} (IP: {izIp})");
        }

        // GF-5 / K2 (D4): sahiplik ihlali izi. Gerekce ve kapsam siniri ISecurityEventService'te.
        //
        // OLAY TIPI `IdorAttempt` SECILDI, YENI AD UYDURULMADI: bu ad `ops/serilog-siem.md:31`de
        // ve `SecurityEvent.cs:10` yorumunda ZATEN yaziliydi ama kodda HICBIR YERDE URETILMIYORDU
        // (AV-2 olcumu; "belgede VAR, kodda YOK" bes tipten biri). Boylece K8'in belge duzeltmesi
        // bu tipi SILMEK yerine DOGRULAMAK zorunda kalir - belge ile kod ayni yone cekilir.
        //
        // SEVERITY "Warning", "Critical" DEGIL - BILINCLI: tetikleyicinin ON KOSULU KIMLIKLI
        // oturumdur (SDP 1.12.2). Critical isaretlemek her denemede admin bildirimi atesler
        // (`:39-40`) ve bugun o kanal BOS GRUBA yayin yapiyor - yani gurultu uretir, okuyucu
        // uretmez. Ayrica `DataRetentionJob.cs:33` Critical satirlari SONSUZA KADAR tutuyor.
        public Task SahiplikIhlaliAsync(string kaynak, int kaynakId, int? istekSahibi) =>
            LogAsync("IdorAttempt", "Warning", istekSahibi, null, null, $"{kaynak}:{kaynakId}");
    }
}
