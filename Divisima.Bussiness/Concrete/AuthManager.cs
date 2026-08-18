using System.Net;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Sanitization;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Security.Tokens;
using Divisima.Core.Security.JWT;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Mail;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Auth;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Kimlik doğrulama iş kuralları (Cafixo AuthManager kalıbı).
    // Register -> HashingHelper.CreatePasswordHash; Login -> VerifyPasswordHash + JwtHelper.CreateToken + session.
    public class AuthManager : IAuthService
    {
        private readonly IReferralService _referralService;
        private readonly IConsentRecordDal _consentDal;
        private readonly ICustomerDal _customerDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly ITokenHelper _tokenHelper;
        private readonly IMailService _mailService;
        private readonly ISecurityEventService _securityEvents;

        public AuthManager(ICustomerDal customerDal, IUserSessionDal userSessionDal, ITokenHelper tokenHelper, IMailService mailService, ISecurityEventService securityEvents,
            IReferralService referralService, IConsentRecordDal consentDal)
        {
            _referralService = referralService;
            _consentDal = consentDal;
            _customerDal = customerDal;
            _userSessionDal = userSessionDal;
            _tokenHelper = tokenHelper;
            _mailService = mailService;
            _securityEvents = securityEvents;
        }

        // Açıklayıcı yorum: Kayıt. E-posta benzersiz + şifre hash+salt (düz metin değil). Customer.name tek alan.
        public async Task<(HttpStatusCode, Result)> Register(CustomerRegisterRequestDto dto)
        {
            var existing = await _customerDal.GetByEmailAsync(dto.email);
            if (existing != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.EmailAlreadyExists));

            // Açıklayıcı yorum: HMAC-SHA512 ile şifre hash + salt (Cafixo HashingHelper)
            HashingHelper.CreatePasswordHash(dto.password, out byte[] passwordHash, out byte[] passwordSalt);

            var customer = new Customer
            {
                name = InputSanitizer.Sanitize(dto.name ?? ""),  // stored XSS savunması (admin panelinde render)
                user_type = (byte)UserTypeEnum.Customer,   // yeni kayıt her zaman Customer (admin DB'den atanır)
                email = (dto.email ?? "").Trim().ToLower(),
                phone = dto.phone,
                password_hash = passwordHash,
                password_salt = passwordSalt,
                gender = dto.gender,
                is_active = true,
                created_at = DateTime.Now
            };
            // Açıklayıcı yorum: Opsiyonel referans kodu - davet edeni çöz + bağla
            if (!string.IsNullOrWhiteSpace(dto.referral_code))
            {
                var referrerId = await _referralService.ResolveReferrer(dto.referral_code);
                if (referrerId.HasValue) customer.referred_by = referrerId.Value;
            }

            // Açıklayıcı yorum: E-posta doğrulama token'ı üret + doğrulama maili gönder
            customer.email_verified = false;
            customer.email_verification_token = SecureTokenGenerator.Generate();
            customer.email_verification_sent_at = DateTime.Now;
            await _customerDal.AddAsync(customer);

            // Aciklayici yorum: KVKK ACIK RIZA KAYDI - kabul edilen sozlesmeler kanit icin saklanir (metni gostermek yetmez).
            // customer.id AddAsync sonrasi dolu. Pazarlama rizasi kabul VE ret olarak saklanir (ETK kaniti).
            var consentVersion = string.IsNullOrWhiteSpace(dto.consent_version) ? "1.0" : dto.consent_version;
            var consentTime = DateTime.Now;
            if (dto.accepted_terms)
                await _consentDal.AddAsync(new ConsentRecord { customer_id = customer.id, consent_type = "terms", document_version = consentVersion, granted = true, created_at = consentTime });
            if (dto.accepted_privacy)
                await _consentDal.AddAsync(new ConsentRecord { customer_id = customer.id, consent_type = "privacy", document_version = consentVersion, granted = true, created_at = consentTime });
            await _consentDal.AddAsync(new ConsentRecord { customer_id = customer.id, consent_type = "marketing", document_version = consentVersion, granted = dto.accepted_marketing, created_at = consentTime });

            await _mailService.SendAsync(new MailMessageDto
            {
                To = customer.email,
                Subject = "Divisima - E-posta adresinizi doğrulayın",
                Body = $"Hesabınızı doğrulamak için token: {customer.email_verification_token}"
            });

            return (HttpStatusCode.Created, new SuccessResult(Messages.RegisterSuccess));
        }

        // Açıklayıcı yorum: Giriş. E-posta bul -> şifre doğrula -> JWT üret -> oturum kaydet.
        public async Task<(HttpStatusCode, Result)> Login(CustomerLoginRequestDto dto)
        {
            var customer = await _customerDal.GetByEmailAsync(dto.email);
            if (customer == null)
            {
                // Açıklayıcı yorum: ENUMERATION TIMING engeli - kullanıcı yoksa da hash doğrulama süresi harcanır,
                // böylece "var/yok" yanıt süresi farkından e-posta enumerasyonu yapılamaz.
                HashingHelper.CreatePasswordHash("dummy_timing_equalizer", out var dh, out var ds);
                HashingHelper.VerifyPasswordHash(dto.password ?? "x", dh, ds);
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.LoginFailed));
            }

            // Açıklayıcı yorum: Hesap kilitli mi (kaba kuvvet koruması)
            if (customer.lockout_end.HasValue && customer.lockout_end.Value > DateTime.Now)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.AccountLocked));

            // Açıklayıcı yorum: Şifre doğrulama. Yanlışsa başarısız sayacını artır, 5'te 15 dk kilitle.
            if (!HashingHelper.VerifyPasswordHash(dto.password, customer.password_hash, customer.password_salt))
            {
                // GUVENLIK DUZELTMESI: ATOMIK sayac artisi - paralel brute-force denemeleri artisi KAYBETMEZ
                // (tracked += ile 100 eszamanlı deneme sayaci 1'de tutup kilidi atlardi).
                int attempts = await _customerDal.IncrementFailedLoginAsync(customer.id);
                bool nowLocked = attempts >= 5;
                if (nowLocked)
                    await _customerDal.LockAccountAsync(customer.id, DateTime.Now.AddMinutes(15));
                // Açıklayıcı yorum: Güvenlik olayı - başarısız login (kilitlenmede Critical)
                await _securityEvents.LogAsync(nowLocked ? "AccountLocked" : "LoginFailed",
                    nowLocked ? "Critical" : "Warning", customer.id, null, null,
                    nowLocked ? "5 başarısız denemeden sonra hesap kilitlendi" : "Hatalı şifre");
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.LoginFailed));
            }

            if (!customer.is_active)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.AccountInactive));

            // Açıklayıcı yorum: E-posta doğrulama ZORUNLU - doğrulanmamış hesap giriş yapamaz (sahte kayıt engeli).
            if (!customer.email_verified)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.EmailNotVerified));

            // Açıklayıcı yorum: Başarılı giriş - başarısız sayaç + kilit sıfırla, son giriş güncelle
            // ATOMIK login durumu sifirla (sayac + kilit + son giris)
            await _customerDal.ResetLoginStateAsync(customer.id, DateTime.Now);

            // Açıklayıcı yorum: 2FA ENFORCEMENT - iki-faktör açıksa şifre TEK BAŞINA yetmez. 6 haneli e-posta OTP
            // üretilir (hash'li saklanır, 5 dk), token VERİLMEZ. Kullanıcı /api/auth/verify-2fa ile kodu doğrular.
            // (Önceden two_factor_enabled bir bayraktı ama login'de hiç kontrol edilmiyordu = 2FA koruması SIFIRDI.)
            if (customer.two_factor_enabled)
            {
                var otp = SecureTokenGenerator.GenerateNumericCode(6);
                customer.two_factor_code = HashSha256(otp);
                customer.two_factor_code_expiry = DateTime.Now.AddMinutes(5);
                await _customerDal.UpdateAsync(customer);
                await _mailService.SendAsync(new MailMessageDto
                {
                    To = customer.email,
                    Subject = "Divisima - Giriş doğrulama kodu",
                    Body = $"Giriş doğrulama kodunuz: {otp} (5 dakika geçerli). Siz istemediyseniz şifrenizi değiştirin.",
                    IsHtml = false
                });
                await _securityEvents.LogAsync("TwoFactorChallenge", "Info", customer.id, null, null, "2FA kodu gönderildi");
                return (HttpStatusCode.Accepted, new SuccessResult(Messages.TwoFactorRequired));
            }

            // Açıklayıcı yorum: Oturum + JWT + refresh token üret (merkezi helper - DRY)
            var response = await IssueSessionAndTokenAsync(customer);
            return (HttpStatusCode.OK, new SuccessDataResult<CustomerLoginResponseDto>(response, Messages.LoginSuccess));
        }

        // Açıklayıcı yorum: 2FA DOĞRULAMA - login'de gönderilen e-posta OTP'sini doğrular, doğruysa JWT verir.
        // Kod hash'li karşılaştırılır (constant-time), süre kontrol edilir, tek kullanımlık (doğrulama/hata sonrası temizlenir).
        public async Task<(HttpStatusCode, Result)> VerifyTwoFactor(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.TwoFactorInvalid));

            var customer = await _customerDal.GetAsync(c => c.email == email && c.is_active);
            if (customer == null || !customer.two_factor_enabled || string.IsNullOrEmpty(customer.two_factor_code))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.TwoFactorInvalid));

            // Süre doldu mu
            if (!customer.two_factor_code_expiry.HasValue || customer.two_factor_code_expiry.Value < DateTime.Now)
            {
                customer.two_factor_code = null; customer.two_factor_code_expiry = null;
                await _customerDal.UpdateAsync(customer);
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.TwoFactorExpired));
            }

            // Constant-time hash karşılaştırma; kod HER durumda (doğru/yanlış) temizlenir -> brute-force için tek deneme.
            bool match = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(customer.two_factor_code),
                System.Text.Encoding.UTF8.GetBytes(HashSha256(code)));
            customer.two_factor_code = null; customer.two_factor_code_expiry = null;
            await _customerDal.UpdateAsync(customer);
            if (!match)
            {
                await _securityEvents.LogAsync("TwoFactorFailed", "Warning", customer.id, null, null, "Yanlış 2FA kodu");
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.TwoFactorInvalid));
            }

            // Doğru - oturum + JWT + refresh token (merkezi helper - DRY)
            var response = await IssueSessionAndTokenAsync(customer);
            return (HttpStatusCode.OK, new SuccessDataResult<CustomerLoginResponseDto>(response, Messages.LoginSuccess));
        }

        // Açıklayıcı yorum: MERKEZİ oturum+token üretimi (login / 2FA-doğrulama / refresh HEPSİ buradan - DRY).
        // JWT + kriptografik refresh_token üretir, oturumu KAYDEDER (refresh_token + refresh penceresi expiry), response döner.
        // Önceden 3 yerde tekrarlanıyordu ve refresh_token HİÇ set edilmiyordu (refresh mekanizması ölüydü).
        private const int RefreshTokenDays = 7;
        private async Task<CustomerLoginResponseDto> IssueSessionAndTokenAsync(Customer customer)
        {
            var accessToken = _tokenHelper.CreateToken(customer);
            var refreshToken = SecureTokenGenerator.Generate();
            await _userSessionDal.AddAsync(new UserSession
            {
                customer_id = customer.id,
                refresh_token = refreshToken,
                expires_at = DateTime.Now.AddDays(RefreshTokenDays),
                is_active = true,
                created_at = DateTime.Now
            });
            return new CustomerLoginResponseDto
            {
                customer_id = customer.id,
                name = customer.name,
                email = customer.email,
                token = accessToken.Token,
                expiration = accessToken.Expiration,
                refresh_token = refreshToken
            };
        }

        // Açıklayıcı yorum: OTP hash (kısa ömürlü kod - SHA256 yeterli; plaintext saklanmaz).
        private static string HashSha256(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        public async Task<(HttpStatusCode, Result)> RefreshToken(RefreshTokenRequestDto dto)
        {
            var session = await _userSessionDal.GetByRefreshTokenAsync(dto.refresh_token);
            if (session == null || !session.is_active)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));

            // Açıklayıcı yorum: Refresh token süresi dolmuş mu
            if (session.expires_at < DateTime.Now)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenExpired));

            var customer = await _customerDal.GetAsync(c => c.id == session.customer_id);
            if (customer == null || !customer.is_active)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));

            // Açıklayıcı yorum: ROTATION - eski oturumu kapat, yeni oturum+JWT+refresh token üret (merkezi helper).
            // Eski refresh token artık geçersiz (replay engeli); istemci yeni refresh_token'ı response'tan alır.
            session.is_active = false;
            await _userSessionDal.UpdateAsync(session);
            var response = await IssueSessionAndTokenAsync(customer);
            return (HttpStatusCode.OK, new SuccessDataResult<CustomerLoginResponseDto>(response, Messages.TokenRefreshed));
        }


        // Açıklayıcı yorum: E-posta doğrulama - token eşleşirse hesabı doğrulanmış işaretle
        public async Task<(HttpStatusCode, Result)> VerifyEmail(string token)
        {
            // Aciklayici yorum: BOS TOKEN GUARD (defense) - bos/null token, dogrulanmis (token=null) hesaba eslesmesin.
            if (string.IsNullOrWhiteSpace(token))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidVerificationToken));
            // Açıklayıcı yorum: Savunma derinliği - boş/null token null-alanlı kayıtlarla eşleşmesin
            if (string.IsNullOrWhiteSpace(token))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.EmailVerificationInvalid));

            var customer = await _customerDal.GetAsync(c => c.email_verification_token == token);
            if (customer == null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.EmailVerificationInvalid));
            if (customer.email_verified)
                return (HttpStatusCode.OK, new SuccessResult(Messages.EmailAlreadyVerified));

            customer.email_verified = true;
            customer.email_verification_token = null;
            await _customerDal.UpdateAsync(customer);
            return (HttpStatusCode.OK, new SuccessResult(Messages.EmailVerified));
        }

        // Açıklayıcı yorum: Doğrulama mailini yeniden gönder (token yenilenir)
        public async Task<(HttpStatusCode, Result)> ResendVerification(string email)
        {
            var customer = await _customerDal.GetByEmailAsync(email);
            if (customer == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));
            if (customer.email_verified)
                return (HttpStatusCode.OK, new SuccessResult(Messages.EmailAlreadyVerified));

            customer.email_verification_token = SecureTokenGenerator.Generate();
            customer.email_verification_sent_at = DateTime.Now;
            await _customerDal.UpdateAsync(customer);
            await _mailService.SendAsync(new MailMessageDto
            {
                To = customer.email,
                Subject = "Divisima - E-posta doğrulama (yeniden)",
                Body = $"Doğrulama token: {customer.email_verification_token}"
            });
            return (HttpStatusCode.OK, new SuccessResult(Messages.EmailVerificationSent));
        }


        // Açıklayıcı yorum: Şifre sıfırlama talebi. E-posta varsa token üret + mail. Kullanıcı sızdırma yok:
        // e-posta olsa da olmasa da AYNI başarı mesajı döner (enumeration engeli).
        public async Task<(HttpStatusCode, Result)> ForgotPassword(ForgotPasswordRequestDto dto)
        {
            var customer = await _customerDal.GetByEmailAsync(dto.email);
            if (customer != null && customer.is_active)
            {
                customer.password_reset_token = SecureTokenGenerator.Generate();
                customer.password_reset_expiry = DateTime.Now.AddMinutes(30); // kısa ömür
                await _customerDal.UpdateAsync(customer);
                await _mailService.SendAsync(new MailMessageDto
                {
                    To = customer.email,
                    Subject = "Divisima - Şifre sıfırlama",
                    Body = $"Şifrenizi sıfırlamak için token: {customer.password_reset_token} (30 dk geçerli)"
                });
            }
            // Açıklayıcı yorum: Her durumda aynı yanıt (hesap var mı bilgisini sızdırma)
            return (HttpStatusCode.OK, new SuccessResult(Messages.PasswordResetMailSent));
        }

        // Açıklayıcı yorum: Token ile yeni şifre belirle. Token geçerli+süresi dolmamışsa şifreyi değiştir,
        // token'ı geçersiz kıl, TÜM oturumları kapat (çalınan token güvenliği).
        public async Task<(HttpStatusCode, Result)> ResetPassword(ResetPasswordRequestDto dto)
        {
            // Açıklayıcı yorum: BOŞ TOKEN GUARD (defense) - boş/null token, reset istememiş (token=null) bir
            // müşteriye eşleşmesin diye önce reddedilir. Aksi halde null==null eşleşme riski (expiry ile de korunuyor).
            if (string.IsNullOrWhiteSpace(dto.token))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidResetToken));
            // Açıklayıcı yorum: Savunma derinliği - boş/null token null-alanlı kayıtlarla eşleşmesin
            if (string.IsNullOrWhiteSpace(dto.token))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordResetInvalid));

            var customer = await _customerDal.GetAsync(c => c.password_reset_token == dto.token);
            if (customer == null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordResetInvalid));
            if (!customer.password_reset_expiry.HasValue || customer.password_reset_expiry.Value < DateTime.Now)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordResetExpired));

            HashingHelper.CreatePasswordHash(dto.new_password, out var hash, out var salt);
            customer.password_hash = hash;
            customer.password_salt = salt;
            customer.password_reset_token = null;
            customer.password_reset_expiry = null;
            customer.failed_login_attempts = 0;
            customer.lockout_end = null;
            await _customerDal.UpdateAsync(customer);

            // Açıklayıcı yorum: Şifre değişince mevcut tüm oturumları geçersiz kıl (çalınan token'ı öldür)
            // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
                await _userSessionDal.InvalidateAllForCustomerAsync(customer.id);

            return (HttpStatusCode.OK, new SuccessResult(Messages.PasswordResetSuccess));
        }


        // Açıklayıcı yorum: Çıkış - refresh token verildiyse o oturumu, verilmediyse tüm oturumları kapat.
        // Böylece çalınan/eski refresh token bir daha kullanılamaz (JWT revocation - oturum tarafı).
        public async Task<(HttpStatusCode, Result)> Logout(int customerId, string? refreshToken)
        {
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var session = await _userSessionDal.GetByRefreshTokenAsync(refreshToken);
                if (session != null && session.customer_id == customerId)
                {
                    session.is_active = false;
                    await _userSessionDal.UpdateAsync(session);
                }
            }
            else
            {
                // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
                await _userSessionDal.InvalidateAllForCustomerAsync(customerId);
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.LogoutSuccess));
        }

        // Açıklayıcı yorum: Hesap silme (KVKK/GDPR unutulma hakkı). Kişisel veriyi anonimleştirir,
        // hesabı pasifleştirir, tüm oturumları kapatır. Sipariş geçmişi (yasal saklama) korunur ama
        // kimlik bilgisi anonimleştirilir. Tam silme yerine anonimleştirme: yasal kayıt bütünlüğü + gizlilik.
        public async Task<(HttpStatusCode, Result)> DeleteAccount(int customerId)
        {
            var customer = await _customerDal.GetAsync(c => c.id == customerId);
            if (customer == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            // Açıklayıcı yorum: Kişisel veriyi anonimleştir (geri döndürülemez) - TÜM PII temizlenir (KVKK/GDPR).
            // Sipariş/fatura FK bütünlüğü için hard-delete yerine anonimleştirme.
            customer.name = "Silinmiş Kullanıcı";
            customer.email = $"deleted-{Guid.NewGuid():N}@anonymized.local";
            customer.phone = null;
            customer.address = null;
            customer.city = null;
            customer.birthdate = null;
            customer.referral_code = null;
            customer.two_factor_secret = null;
            customer.two_factor_enabled = false;
            customer.two_factor_code = null;
            customer.password_reset_token = null;
            customer.email_verification_token = null;
            customer.notify_email = false;
            customer.notify_sms = false;
            customer.notify_push = false;
            HashingHelper.CreatePasswordHash(Guid.NewGuid().ToString(), out var h, out var salt);
            customer.password_hash = h;
            customer.password_salt = salt;
            customer.is_active = false;
            await _customerDal.UpdateAsync(customer);

            // Açıklayıcı yorum: Tüm oturumları kapat
            var sessions = await _userSessionDal.GetListAsync(us => us.customer_id == customerId && us.is_active);
            foreach (var s in sessions) { s.is_active = false; await _userSessionDal.UpdateAsync(s); }

            await _securityEvents.LogAsync("AccountDeleted", "Warning", customerId, null, null, "Kullanıcı hesabını sildi (anonimleştirildi)");
            return (HttpStatusCode.OK, new SuccessResult(Messages.AccountDeleted));
        }

        // Açıklayıcı yorum: Veri dışa aktarma (GDPR taşınabilirlik). Kullanıcının kişisel verisini döndürür.
        public async Task<(HttpStatusCode, Result)> ExportMyData(int customerId)
        {
            var customer = await _customerDal.GetAsync(c => c.id == customerId);
            if (customer == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            // Açıklayıcı yorum: Hassas alanlar (hash/secret/token) HARİÇ - yalnız kullanıcının kendi verisi
            var export = new
            {
                customer.id,
                customer.name,
                customer.email,
                customer.phone,
                customer.created_at,
                customer.email_verified,
                two_factor_enabled = customer.two_factor_enabled
            };
            return (HttpStatusCode.OK, new SuccessDataResult<object>(export, Messages.DataExported));
        }

    }
}
