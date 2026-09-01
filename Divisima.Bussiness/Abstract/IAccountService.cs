using System.Net;
using Divisima.Core.Utilities.Results;
using Divisima.Entity.Dtos.Account;

namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Hesap yönetimi - profil, şifre, bildirim tercihleri, GDPR silme.
    public interface IAccountService
    {
        Task<(HttpStatusCode, Result)> GetSummary(int customerId);
        Task<(HttpStatusCode, Result)> UpdateProfile(int customerId, UpdateProfileRequestDto dto);
        // GF-1 / K2: `jti` + `jtiExpiresAt` VARSAYILAN DEGERLI eklendi (mevcut cagiranlar
        // degismek zorunda degil). Verilirse sifre degisimi, oturumlarin yaninda SUNULAN
        // access token'i da kara listeye yazar.
        Task<(HttpStatusCode, Result)> ChangePassword(int customerId, ChangePasswordRequestDto dto,
            string? jti = null, System.DateTime? jtiExpiresAt = null);
        Task<(HttpStatusCode, Result)> UpdateNotificationPreferences(int customerId, NotificationPreferencesDto dto);
        Task<(HttpStatusCode, Result)> DeleteAccount(int customerId);
    }
}
