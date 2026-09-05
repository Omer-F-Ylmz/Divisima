using Divisima.DataAccess.Abstract;

namespace Divisima.Bussiness.Outbox
{
    // Açıklayıcı yorum: Veri saklama/temizlik işi (Hangfire günlük). Eski pasif oturumları, işlenmiş outbox
    // mesajlarını ve eski güvenlik/denetim loglarını temizler (KVKK/GDPR saklama süreleri + performans).
    public class DataRetentionJob
    {
        private readonly IUserSessionDal _sessionDal;
        private readonly IOutboxMessageDal _outboxDal;
        private readonly ISecurityEventDal _securityEventDal;

        public DataRetentionJob(IUserSessionDal sessionDal, IOutboxMessageDal outboxDal, ISecurityEventDal securityEventDal)
        {
            _sessionDal = sessionDal;
            _outboxDal = outboxDal;
            _securityEventDal = securityEventDal;
        }

        public async Task RunAsync()
        {
            var now = DateTime.Now;
            // PERFORMANS: TOPLU sil - tek SQL DELETE ... WHERE (foreach GetList->tek-tek DeleteAsync N+1 idi:
            // binlerce eski kayıt = binlerce round-trip + hepsini belleğe yükleme). ExecuteDeleteAsync tek sorguda siler.

            // Açıklayıcı yorum: 90 günden eski pasif oturumları sil
            await _sessionDal.DeleteWhereAsync(s => !s.is_active && s.created_at < now.AddDays(-90));

            // Açıklayıcı yorum: 30 günden eski işlenmiş outbox mesajlarını sil
            await _outboxDal.DeleteWhereAsync(m => m.status == 1 && m.created_at < now.AddDays(-30));

            // ══ GF-5 / K9 (SC-12) - ISLENMEMIS ve OLU MESAJLAR DA TEMIZLENIR ══════════════
            //
            // OLCULEN ONCE-DURUM: bu is YALNIZ `status == 1` (islenmis) satirlari siliyordu.
            // Ureten ifadeler (LITERAL DEGIL - sayilar her kosumda degisir, MK-3):
            //     SELECT COUNT(*) FROM outbox_messages;                        -> olcumde 400
            //     SELECT COUNT(*) FROM outbox_messages WHERE status = 0;       -> olcumde 301
            //     SELECT COUNT(*) FROM outbox_messages WHERE status = 2;       -> olcumde   1
            //     SELECT DATEDIFF(day, MIN(created_at), GETDATE())
            //       FROM outbox_messages WHERE status = 0;                     -> olcumde  13
            // Yani islenmemis satirlarin buyuk cogunlugu OLU idi ve HICBIR ZAMAN silinmiyordu.
            //
            // NEDEN ONEMLI - SIR HIJYENI: mail govdeleri DUZ JETON tasiyor
            // (`AuthManager.cs` dogrulama, `ResetPasswordRequest` sifirlama). `customers`
            // tablosunda ayni jetonlar GF-1b/K3 ile SHA-256 OZETLI saklanirken, outbox
            // payload'i HAM tasiyor - yani ozetleme BIR KANAL OTEDEN deliniyordu ve o kanal
            // suresiz birikiyordu. Bu kalem "maskeleme" ile KAPATILAMAZ: jetonun kullaniciya
            // ULASMASI gerekir, dolayisiyla gonderilene kadar okunabilir olmak ZORUNDADIR.
            // Kapatilabilecek olan sey OMURDUR.
            //
            // KAPSAM BU DALGADA YALNIZ SAKLAMA (merkez karari D11): payload'in sifrelenmesi ya
            // da ozetlenmesi LAUNCH SONRASINA (GF-6, SA-1 ile birlikte) birakildi - bugunku
            // `AesEncryptionProvider` tek anahtarli ve cozemedigi degeri OLDUGU GIBI donduruyor
            // (SA-2), yani sifreleme once O kalemin duzeltilmesini ister. MIGRATION YOK.
            //
            // PENCERE JETON OMRUNDEN TURETILIR, ELLE YAZILMAZ: en uzun jeton omru sifirlama
            // jetonununkidir (`AuthManager.cs` -> 30 dk). Uzerine 24 saat marj konur ki
            // outbox islemcisi gecici bir kesintiden sonra kuyrugu BOSALTABILSIN; 24 saatte
            // gonderilememis bir mail zaten OLUDUR ve icindeki jeton COKTAN gecersizdir.
            var jetonEnUzunOmur = TimeSpan.FromMinutes(30);   // password_reset_expiry
            var islenmemisPencere = jetonEnUzunOmur + TimeSpan.FromHours(24);
            var esik = now - islenmemisPencere;
            // status 0 = islenmemis (kuyrukta kalmis), 2 = olu mektup. status 1 YUKARIDA,
            // KENDI (30 gunluk) penceresiyle silinir - o satirlar BASARIYLA gonderilmistir ve
            // is/teshis degeri tasir; buradaki kisa pencere onlari KAPSAMAZ.
            await _outboxDal.DeleteWhereAsync(m => (m.status == 0 || m.status == 2) && m.created_at < esik);

            // Açıklayıcı yorum: 1 yıldan eski güvenlik loglarını sil (Critical hariç - onlar saklanır)
            await _securityEventDal.DeleteWhereAsync(e => e.severity != "Critical" && e.created_at < now.AddYears(-1));
        }
    }
}
