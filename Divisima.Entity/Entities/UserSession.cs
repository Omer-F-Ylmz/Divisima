using Divisima.Core.Entities.Abstract;

namespace Divisima.Entity.Entities
{
    // Açıklayıcı yorum: Oturum kaydı (Cafixo UserSession kalıbı) - login'de oluşur.
    public class UserSession : IEntity
    {
        public int id { get; set; }
        public int customer_id { get; set; }
        public string refresh_token { get; set; }
        public string? device { get; set; }
        public string? ip_address { get; set; }
        public DateTime expires_at { get; set; }
        public bool is_active { get; set; }
        public DateTime created_at { get; set; }

        // ══ GF-1 / K3 (C-2) - OTURUM ZINCIRININ GIRIS ANI ══════════════════════════════════
        //
        // NEDEN `created_at` YETMEDI (olculdu): `IssueSessionAndTokenAsync` HER cagrida YENI
        // satir ekliyor ve refresh ROTASYONU da onu cagiriyor - yani `created_at` ROTASYON
        // ANIDIR, giris ani DEGIL. Ilk olcumumde "created_at kullanilir, migration gerekmez"
        // demistim; bu YANLISTI ve kaynaktan duzeltildi.
        //
        // Bu alan zincir boyunca TASINIR: login ve 2FA tamamlanmasi onu `now` yapar (ikisi de
        // KIMLIK DOGRULAMADIR), refresh rotasyonu ESKI satirdan KOPYALAR. Jetondaki `auth_time`
        // claim'i buradan uretilir, boylece `RequireRecentAuth(10)` step-up'i calinmis bir
        // refresh cerezi ile SURESIZ uzatilamaz.
        //
        // NULL = GF-1 ONCESI acilmis oturumlar. O satirlarda davranis DEGISMEZ (jeton uretim
        // ani kullanilir - statuko); alan geriye donuk doldurulmaz.
        public DateTime? auth_time { get; set; }
    }
}
