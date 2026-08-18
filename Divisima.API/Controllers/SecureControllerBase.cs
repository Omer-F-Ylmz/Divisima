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

        // Açıklayıcı yorum: İstenen kaynak sahibi ile oturum kullanıcısı aynı mı? Değilse 403 (IDOR engeli).
        protected IActionResult EnsureOwner(int resourceOwnerId, out bool ok)
        {
            ok = resourceOwnerId == CurrentCustomerId && CurrentCustomerId > 0;
            return ok ? null : StatusCode(403, new { Success = false, Message = "Bu kaynağa erişim yetkiniz yok." });
        }
    }
}
