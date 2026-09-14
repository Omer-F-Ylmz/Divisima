using System.Text;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Dtos;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Text;
using Divisima.DataAccess.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Jobs
{
    // ══ MON-1 / D1 - security_events ICIN TEK OKUYUCU (Hangfire, 5 dk) ═══════════════════════
    //
    // OLCULEN ONCE-DURUM: Critical olaylarin tek otomatik kanali `NotifyAdminsAsync` idi ve o
    // SignalR "admins" grubuna yayin yapar - grup BOSTUR (`51·AV-2` BILINEN). `PaymentAfterTerminal`
    // (elle iade gerektirir) yalniz GUNLUK ELLE SQL sorgusuyla gorulebiliyordu.
    //
    // KANAL MEVCUT OUTBOX: "EmailNotification" mesaji `OutboxProcessor` -> `IMailService` yolundan
    // gider; yeniden deneme ve kalici hata (`failed-jobs`) gorunurlugu bedava gelir. Yeni
    // bagimlilik YOK.
    //
    // TETIK: son turdan beri izlenen tiplerde en az bir `Critical` satir. Ozet o turdaki TUM
    // izlenen satirlari (Warning dahil) sayar; yalniz Warning iceren tur mail URETMEZ, imleci
    // yine ilerletir (tarif: "yeni Critical varsa").
    //
    // GOVDE: olay tipi + sayi + ilk uc id. `detail`, `ip_address`, `user_agent`, `customer_id`
    // GOVDEYE GIRMEZ - outbox payload'i DB'de duz durur ve mail ucuncu taraf saglayicidan gecer.
    //
    // YERLESME PAYI: imlec id uzerinden ilerler; identity degeri INSERT aninda ayrilir ama satir
    // COMMIT'te gorunur. Acik bir transaction'daki dusuk id, daha buyuk id'li satir islenip imlec
    // onu gectikten SONRA gorunurse KALICI olarak kacardi. Bu yuzden yalniz `created_at` payindan
    // eski satirlar okunur. Kalan sinir: payindan uzun acik kalan transaction'in satiri kacar.
    public class KritikOlayAlarmJob
    {
        public const string KonuOneki = "Divisima ALARM";
        public const string AliciAnahtari = "Monitoring:AlarmEmail";

        // Tarifin saydigi bes tip. Uretilen tiplerin tam listesi `ops/serilog-siem.md`de.
        public static readonly string[] IzlenenTipler =
        {
            "PaymentAfterTerminal", "RefreshTokenReuse", "PaymentSignatureInvalid", "AccountLocked", "ProductImportRejected",
        };

        public static readonly TimeSpan YerlesmePayi = TimeSpan.FromSeconds(60);

        private readonly ISecurityEventDal _olayDal;
        private readonly IOutboxService _outbox;
        private readonly IAlarmImleci _imlec;
        private readonly IConfiguration _config;
        private readonly ILogger<KritikOlayAlarmJob> _logger;

        public KritikOlayAlarmJob(ISecurityEventDal olayDal, IOutboxService outbox, IAlarmImleci imlec,
            IConfiguration config, ILogger<KritikOlayAlarmJob> logger)
        {
            _olayDal = olayDal;
            _outbox = outbox;
            _imlec = imlec;
            _config = config;
            _logger = logger;
        }

        // Donus: bu turda yazilan alarm maili sayisi (0/1).
        public async Task<int> RunAsync()
        {
            var alici = _config[AliciAnahtari]?.Trim();
            if (string.IsNullOrWhiteSpace(alici))
            {
                // IMLEC ILERLEMEZ: alici sonradan verildiginde bekleyen olaylar YINE bildirilir.
                // ERROR seviyesi bilincli - gunluk ozetin ERR sayacina dusup gorunur olur (D4).
                _logger.LogError("MON-1 ALARM KANALI KAPALI: {Anahtar} bos, kritik olay bildirimi YAPILMADI.", AliciAnahtari);
                return 0;
            }

            var kesim = DateTime.Now - YerlesmePayi;
            var imlec = await _imlec.OkuAsync();
            if (imlec == null)
            {
                // ILK KOSUM: taban kurulur, GECMIS bildirilmez (dagitimdan onceki olcum/test
                // olaylari tek bir anlamsiz alarm uretirdi). Gecmis icin gunluk SQL sorgusu durur.
                var enSon = await _olayDal.GetPagedAsync(new PagingRequestDto { page = 1, size = 1 },
                    e => e.created_at <= kesim, e => e.id, descending: true);
                var taban = enSon.Items.Count == 0 ? 0 : enSon.Items[0].id;
                await _imlec.YazAsync(taban);
                _logger.LogInformation("MON-1 alarm imleci ilk kez kuruldu: {Taban}", taban);
                return 0;
            }

            var sonIslenen = imlec.Value;
            var yeni = await _olayDal.GetListNoTrackingAsync(e =>
                e.id > sonIslenen && e.created_at <= kesim && IzlenenTipler.Contains(e.event_type));
            if (yeni.Count == 0) return 0;

            var yeniImlec = yeni.Max(e => e.id);
            var kritikSayisi = yeni.Count(e => e.severity == "Critical");
            var yazilan = 0;
            if (kritikSayisi > 0)
            {
                await _outbox.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = alici,
                    Subject = $"{KonuOneki} - {kritikSayisi} kritik güvenlik olayı",
                    Body = KanitMaskesi.Maskele(Govde(yeni, sonIslenen, yeniImlec, kritikSayisi))!,
                });
                yazilan = 1;
            }

            // AT-LEAST-ONCE: imlec mailden SONRA yazilir. Arada cokerse ayni ozet bir kez daha
            // gider; tersi sira (once imlec) cokmede alarmi SESSIZCE kaybederdi.
            await _imlec.YazAsync(yeniImlec);
            return yazilan;
        }

        private static string Govde(List<Divisima.Entity.Entities.SecurityEvent> olaylar, int oncekiImlec, int yeniImlec, int kritikSayisi)
        {
            var sb = new StringBuilder();
            sb.Append("Divisima güvenlik alarmı: son turdan beri ").Append(kritikSayisi).Append(" kritik olay.\n");
            sb.Append("Kayıt aralığı: id ").Append(oncekiImlec + 1).Append(" - ").Append(yeniImlec).Append("\n\n");
            sb.Append("Olay tipi | sayı | ilk 3 kayıt id\n");
            foreach (var grup in olaylar.GroupBy(e => e.event_type).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var ilkUc = string.Join(", ", grup.OrderBy(e => e.id).Take(3).Select(e => e.id));
                sb.Append(grup.Key).Append(" | ").Append(grup.Count()).Append(" | ").Append(ilkUc).Append('\n');
            }
            sb.Append("\nBu e-posta olay ayrıntısı (IP, müşteri, açıklama) TAŞIMAZ. İnceleme:\n");
            sb.Append("SELECT id, event_type, severity, customer_id, created_at FROM security_events WHERE id BETWEEN ")
              .Append(oncekiImlec + 1).Append(" AND ").Append(yeniImlec).Append(" ORDER BY id;\n");
            sb.Append("PaymentAfterTerminal satırı ELLE İADE gerektirir. Susturma ve yanlış alarm usulü: ops/monitoring.md\n");
            return sb.ToString();
        }
    }
}
