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
        Task<(HttpStatusCode, Result)> ChangePassword(int customerId, ChangePasswordRequestDto dto);
        Task<(HttpStatusCode, Result)> UpdateNotificationPreferences(int customerId, NotificationPreferencesDto dto);
        Task<(HttpStatusCode, Result)> DeleteAccount(int customerId);
    }
}
