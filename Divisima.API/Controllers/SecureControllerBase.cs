using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Divisima.API.Controllers
{
    // Açıklayıcı yorum: Kimliğe bağlı controller'lar için taban. JWT'deki gerçek kullanıcı id'sini verir
    // ve sahiplik ihlallerinde (IDOR) tek noktadan 403 döner. Route'tan gelen customerId'ye ASLA güvenilmez.
    public abstract class SecureControllerBase : ControllerBase
    {
        // Açıklayıcı yorum: JWT "NameIdentifier" claim'inden gerçek kullanıcı id (client değiştiremez)
        protected int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

        protected int CurrentCustomerId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        // Açıklayıcı yorum: Satıcı oturumunda JWT'deki gerçek satıcı id (aynı NameIdentifier claim'i, user_type=Seller).
        // SellerController tüm sorguları bu id'ye göre izole eder - client'tan gelen bir seller_id'ye ASLA güvenilmez.
        protected int CurrentSellerId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        // ══ GF-1 / K2 - SUNULAN ACCESS TOKEN'IN KIMLIGI ve BITISI ══════════════════════════
        //
        // Kara liste `jti` ile calisir. Is katmani HTTP baglamini GORMEZ (bilincli sinir), bu
        // yuzden claim BURADA okunup servise PARAMETRE olarak gecirilir - `CurrentCustomerId`
        // ile AYNI kalip. `IHttpContextAccessor`i is katmanina sizdirmak, bugun HTTP bilmeyen
        // manager'lari HTTP'ye baglardi.
        //
        // TTL icin `exp` gerekir: iptal kaydini jetonun KENDI omrunden uzun tutmak, kara
        // listeyi suresiz buyutur (jeton zaten gecersizken kayit yasamaya devam ederdi).
        protected string? CurrentJti => User.FindFirst("jti")?.Value;

        // `exp` Unix saniyesidir ve UTC'dir. Cozumlenemezse null doner; cagiran taraf o
        // durumda iptali ATLAMAZ, kisa bir varsayilan TTL kullanir (bkz. cagri yerleri).
        protected DateTime? CurrentTokenExpiry =>
            long.TryParse(User.FindFirst("exp")?.Value, out var saniye)
                ? DateTimeOffset.FromUnixTimeSeconds(saniye).UtcDateTime
                : null;

        // NOT: Burada bir EnsureOwner yardımcısı vardı ve HİÇBİR yerden çağrılmıyordu (grep: sıfır
        // kullanım). Üstelik 403 döndürüyordu; sahiplik ihlalinde artık tek sözleşme 404 (varlık
        // sızdırılmaz). Ölü ve sözleşmeye aykırı bir yardımcıyı bırakmak, bir gün "hazır var" diye
        // kullanılıp tutarsızlığı geri getirmek demekti - kaldırıldı. Sahiplik kontrolleri iş
        // katmanında (manager'larda) yapılır; kaynak zaten orada okunuyor, kontrol de orada olmalı.
    }
}
