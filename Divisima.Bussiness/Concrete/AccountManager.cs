using System;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Security.Hashing;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Account;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Hesap yönetimi iş kuralları. Profil/şifre/tercih/silme.
    public class AccountManager : IAccountService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly IAddressDal _addressDal;

        public AccountManager(ICustomerDal customerDal, IUserSessionDal userSessionDal, IAddressDal addressDal)
        {
            _customerDal = customerDal;
            _userSessionDal = userSessionDal;
            _addressDal = addressDal;
        }

        public async Task<(HttpStatusCode, Result)> GetSummary(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            var dto = new AccountSummaryDto
            {
                id = c.id, name = c.name, email = c.email, phone = c.phone,
                birthdate = c.birthdate, email_verified = c.email_verified,
                two_factor_enabled = c.two_factor_enabled,
                loyalty_points = c.loyalty_points, store_credit = c.store_credit,
                referral_code = c.referral_code,
                notify_email = c.notify_email, notify_sms = c.notify_sms, notify_push = c.notify_push
            };
            return (HttpStatusCode.OK, new SuccessDataResult<AccountSummaryDto>(dto));
        }

        public async Task<(HttpStatusCode, Result)> UpdateProfile(int customerId, UpdateProfileRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.name))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProfileNameRequired));

            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            c.name = dto.name.Trim();
            c.phone = dto.phone;
            c.birthdate = dto.birthdate;
            c.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(c);
            return (HttpStatusCode.OK, new SuccessResult(Messages.ProfileUpdated));
        }

        public async Task<(HttpStatusCode, Result)> ChangePassword(int customerId, ChangePasswordRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.new_password) || dto.new_password.Length < 6)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordTooShort));

            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            // Açıklayıcı yorum: Mevcut şifre doğrulaması (yetkisiz değişim engeli)
            if (!HashingHelper.VerifyPasswordHash(dto.current_password ?? "", c.password_hash, c.password_salt))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CurrentPasswordWrong));

            HashingHelper.CreatePasswordHash(dto.new_password, out var hash, out var salt);
            c.password_hash = hash;
            c.password_salt = salt;
            c.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(c);

            // Açıklayıcı yorum: Şifre değişince diğer oturumları geçersiz kıl (çalınan token'ı öldür)
            // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
                await _userSessionDal.InvalidateAllForCustomerAsync(customerId);

            return (HttpStatusCode.OK, new SuccessResult(Messages.PasswordChanged));
        }

        public async Task<(HttpStatusCode, Result)> UpdateNotificationPreferences(int customerId, NotificationPreferencesDto dto)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            c.notify_email = dto.notify_email;
            c.notify_sms = dto.notify_sms;
            c.notify_push = dto.notify_push;
            c.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(c);
            return (HttpStatusCode.OK, new SuccessResult(Messages.NotificationPreferencesUpdated));
        }

        public async Task<(HttpStatusCode, Result)> DeleteAccount(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            // Açıklayıcı yorum: GDPR silme hakkı - KİŞİSEL VERİ ANONİMLEŞTİRME + pasifleştirme.
            // Hard-delete yerine anonimleştirme: sipariş/fatura geçmişi bütünlüğü (FK) korunur, PII silinir.
            c.name = "Silinmiş Kullanıcı";
            c.email = $"deleted_{c.id}@divisima.invalid";
            c.phone = null;
            c.address = null;
            c.city = null;
            c.birthdate = null;
            c.referral_code = null;
            c.password_hash = Array.Empty<byte>();
            c.password_salt = Array.Empty<byte>();
            c.email_verification_token = null;
            c.password_reset_token = null;
            c.two_factor_secret = null;
            c.two_factor_enabled = false;
            c.is_active = false;
            c.notify_email = false;
            c.notify_sms = false;
            c.notify_push = false;
            c.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(c);

            // KVKK/GDPR EKSİK SİLME DÜZELTMESİ: kayıtlı ADRES DEFTERİ de PII içerir (full_name/phone/full_address/title).
            // Müşteri kaydı anonimleştirilse bile adresler kalırsa erişim hakkı ihlali sürer -> adresleri de anonimleştir+pasifle.
            var addresses = await _addressDal.GetListAsync(a => a.customer_id == customerId);
            foreach (var a in addresses)
            {
                a.full_name = "Silinmiş";
                a.phone = null;
                a.full_address = "-";
                a.title = "-";
                a.is_active = false;
                a.updated_at = DateTime.Now;
                await _addressDal.UpdateAsync(a);
            }

            // Açıklayıcı yorum: Tüm oturumları kapat
            // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
                await _userSessionDal.InvalidateAllForCustomerAsync(customerId);

            return (HttpStatusCode.OK, new SuccessResult(Messages.AccountDeleted));
        }
    }
}
