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
    // onu gectikten SONRA gorunurse KALICI olarak kacardi. Bu yuzden `created_at` payindan eski
    // satirlar islenir. Kalan sinir: payindan uzun acik kalan transaction'in satiri kacar.
    //
    // KESINTISIZ ONEK (MON-1 tur 2, L3/B1 REPRO'SU): imlec yalniz id sirasindaki YERLESMIS onek
    // kadar ilerler; ilk taze satirda durur. Ilk yazim zaman filtreli okuyup EN BUYUK id'ye
    // atliyordu - kucuk id'li taze satir, buyuk id'li yerlesmis satirla ayni turda KALICI kaciyordu.
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
            var kesim = DateTime.Now - YerlesmePayi;
            var imlec = await _imlec.OkuAsync();
            if (imlec == null)
            {
                // ILK KOSUM: taban kurulur, GECMIS bildirilmez (dagitimdan onceki olcum/test
                // olaylari tek bir anlamsiz alarm uretirdi). Gecmis icin gunluk SQL sorgusu durur.
                // ALICI KONTROLUNDEN ONCE (L3/B2 REPRO'SU): taban DAGITIM ANINDA kurulur. Ilk
                // yazimda alici bosken kurulmuyordu; alici verildigi ilk tur tabani O AN kurup
                // arada biriken Critical'lari YUTUYORDU.
                var enSon = await _olayDal.GetPagedAsync(new PagingRequestDto { page = 1, size = 1 },
                    e => e.created_at <= kesim, e => e.id, descending: true);
                var taban = enSon.Items.Count == 0 ? 0 : enSon.Items[0].id;
                await _imlec.YazAsync(taban);
                _logger.LogInformation("MON-1 alarm imleci ilk kez kuruldu: {Taban}", taban);
                return 0;
            }

            var alici = _config[AliciAnahtari]?.Trim();
            if (string.IsNullOrWhiteSpace(alici))
            {
                // IMLEC ILERLEMEZ: alici sonradan verildiginde bekleyen olaylar YINE bildirilir.
                // ERROR seviyesi bilincli - gunluk ozetin ERR sayacina dusup gorunur olur (D4).
                _logger.LogError("MON-1 ALARM KANALI KAPALI: {Anahtar} bos, kritik olay bildirimi YAPILMADI.", AliciAnahtari);
                return 0;
            }

            var sonIslenen = imlec.Value;
            var adaylar = await _olayDal.GetListNoTrackingAsync(e =>
                e.id > sonIslenen && IzlenenTipler.Contains(e.event_type));
            // GELECEK TARIHLI SATIR (tur 3 / YB-2 + tur 4 / N1-N2): created_at > simdi olan satir "taze"
            // SAYILMAZ - saat geri adimi, TZ degisimi ya da elle INSERT onu gunlerce taze tutar ve onek
            // onda durursa arkasindaki TUM alarmlar sinyalsiz tikanir. ATLANMAZ da: tur 3'te atlanan
            // Critical icin HIC mail gitmiyordu (denetci olctu). Ozete "gelecek tarihli" ETIKETIYLE ayni
            // turda girer, imlec ilerler, ayri tolerans esigi YOK; satir basina TEK WARNING.
            var simdi = DateTime.Now;
            var yeni = new List<Divisima.Entity.Entities.SecurityEvent>();
            var gelecekTarihli = new List<int>();
            foreach (var olay in adaylar.OrderBy(e => e.id))
            {
                if (olay.created_at > simdi)
                {
                    _logger.LogWarning("MON-1 gelecek tarihli olay id={Id} (created_at simdiden ileride); ozete etiketle alindi.", olay.id);
                    gelecekTarihli.Add(olay.id);
                }
                else if (olay.created_at > kesim)
                {
                    break;   // kesintisiz yerlesmis onek burada biter
                }
                yeni.Add(olay);
            }
            if (yeni.Count == 0) return 0;

            var yeniImlec = yeni[^1].id;
            var kritikSayisi = yeni.Count(e => e.severity == "Critical");
            var yazilan = 0;
            if (kritikSayisi > 0)
            {
                // KONU TABLOYLA AYNI KUMEYI SAYAR (L3/B5): toplam = tablodaki satir sayilarinin
                // toplami; kritik alt sayi parantezde.
                await _outbox.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = alici,
                    Subject = $"{KonuOneki} - {yeni.Count} güvenlik olayı ({kritikSayisi} kritik)",
                    Body = KanitMaskesi.Maskele(Govde(yeni, sonIslenen, yeniImlec, kritikSayisi, gelecekTarihli))!,
                });
                yazilan = 1;
            }

            // AT-LEAST-ONCE: imlec mailden SONRA yazilir. Arada cokerse ayni ozet bir kez daha
            // gider; tersi sira (once imlec) cokmede alarmi SESSIZCE kaybederdi.
            await _imlec.YazAsync(yeniImlec);
            return yazilan;
        }

        private static string Govde(List<Divisima.Entity.Entities.SecurityEvent> olaylar, int oncekiImlec, int yeniImlec, int kritikSayisi,
            List<int> gelecekTarihli)
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
            if (gelecekTarihli.Count > 0)
                sb.Append("\ngelecek tarihli: id ").Append(string.Join(", ", gelecekTarihli))
                  .Append(" (created_at şimdiden ileride - sunucu saati ya da elle kayıt incelenmeli)\n");
            sb.Append("\nBu e-posta olay ayrıntısı (IP, müşteri, açıklama) TAŞIMAZ. İnceleme:\n");
            sb.Append("SELECT id, event_type, severity, customer_id, created_at FROM security_events WHERE id BETWEEN ")
              .Append(oncekiImlec + 1).Append(" AND ").Append(yeniImlec).Append(" ORDER BY id;\n");
            sb.Append("PaymentAfterTerminal satırı ELLE İADE gerektirir. Susturma ve yanlış alarm usulü: ops/monitoring.md\n");
            return sb.ToString();
        }
    }
}
