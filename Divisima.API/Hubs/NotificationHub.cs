using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Divisima.Core.Utilities.Enums;

namespace Divisima.API.Hubs
{
    // Açıklayıcı yorum: SignalR hub - istemciler buraya bağlanır (admin paneli, müşteri).
    // GÜVENLİK: [Authorize] - hub'a bağlanmak için kimlik doğrulama zorunlu (anonim bağlantı yok).
    [Authorize]
    public class NotificationHub : Hub
    {
        // Açıklayıcı yorum: Admin grubuna katıl (yeni sipariş bildirimleri için).
        // GÜVENLİK DÜZELTMESİ: YALNIZCA admin katılabilir. Önceden HERHANGİ bağlı istemci (müşteri dahil)
        // JoinAdminGroup çağırıp admin bildirimlerini alabiliyordu = YETKİ YÜKSELTME. Artık user_type claim'i doğrulanır.
        public async Task JoinAdminGroup()
        {
            var claim = Context.User?.FindFirst("user_type");
            if (claim == null || !int.TryParse(claim.Value, out var userType) || userType != (int)UserTypeEnum.Admin)
                throw new HubException("Bu işlem için admin yetkisi gerekir.");

            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        }
    }
}
