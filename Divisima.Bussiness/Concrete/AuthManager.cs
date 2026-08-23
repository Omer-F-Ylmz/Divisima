using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Security.JWT;
using Divisima.Core.Security.Tokens;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Mail;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
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
        private readonly ICacheService _cache;
        // LAUNCH-FIX A1(c): dogrulama / sifre sifirlama maillerindeki TIKLANABILIR baglantinin
        // tek kaynagi. Gerekce IMailLinkBuilder'in basinda yazili.
        private readonly IMailLinkBuilder _links;

        // ══ LAUNCH-FIX A1(b) EKI - KAYIT/SIFIRLAMA MAILLERI DE OUTBOX'TAN GECIYOR ═══════════
        //
        // BU BULGU DALGA A'NIN KENDI OLCUMUNDE CIKTI (planlanan kalem degildi): yeni pin
        // yazilirken sahte mail servisi HER gonderimde istisna atacak sekilde ayarlandi ve
        // POST /api/auth/register **HTTP 500** dondu. Kok sebep siparis yolundakiyle AYNI:
        // gonderim istek hattinda, try/catch YOK, SmtpMailService hatayi FIRLATIYOR.
        // ZARAR SIPARISTEKINDEN AGIR: musteri satiri ZATEN yazilmis oluyor (AddAsync mail'den
        // ONCE), yani kullanici "kayit olamadim" sanip tekrar deniyor ve bu kez "var olan hesap"
        // dalina dusuyor - hesabi VAR ama dogrulama maili HIC GITMEMIS durumda kaliyor.
        //
        // COZUM AYNI MEVCUT KANAL: EngagementManager'in kullandigi "EmailNotification" outbox
        // tipi. Bedel ~1 dakikalik gecikme; kazanc, SMTP'nin kayit/sifirlama akisini
        // DUSUREMEMESI ve hatanin 5 kez yeniden denenmesi.
        //
        // 2FA KODU BILINCLI OLARAK HARIC: o bir GIRIS anahtaridir, 5 dakika omru vardir ve
        // gecikmeli/kayip gitmesi kullanicinin giris yapamamasi demektir - orada GURULTULU
        // basarisizlik dogru davranistir. Bugun zaten ulasilamaz bir dal (two_factor_enabled
        // hicbir kod yolunda true yapilmiyor - olculdu).
        private readonly Divisima.Bussiness.Outbox.IOutboxService _outboxService;

        public AuthManager(ICustomerDal customerDal, IUserSessionDal userSessionDal, ITokenHelper tokenHelper, IMailService mailService, ISecurityEventService securityEvents,
            IReferralService referralService, IConsentRecordDal consentDal, ICacheService cache,
            IMailLinkBuilder links, Divisima.Bussiness.Outbox.IOutboxService outboxService)
        {
            _outboxService = outboxService;
            _links = links;
            _cache = cache;
            _referralService = referralService;
            _consentDal = consentDal;
            _customerDal = customerDal;
            _userSessionDal = userSessionDal;
            _tokenHelper = tokenHelper;
            _mailService = mailService;
            _securityEvents = securityEvents;
        }

        // ══ LAUNCH-FIX A1(c) - MAIL GOVDELERI TEK YERDE ══════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM: dort ayri cagri yerinde govde su tek satirdi ->
        //   "Hesabinizi dogrulamak icin token: <token>"
        // Ne baglanti, ne yonerge, ne marka satiri vardi. Kullanici jetonu NEREYE yazacagini
        // e-postadan ogrenemiyordu.
        //
        // BICIM KARARI OLCUME DAYALI: depodaki TUM mailler duz metin (IsHtml=false) -
        // EngagementManager, StockNotificationManager, PriceDropManager, AuthManager. HTML sablon
        // katmani ACILMADI; duz metinde kendi satirinda duran ciplak URL her istemcide tiklanabilir.
        //
        // JETON HER IKI DURUMDA DA GOVDEDE KALIYOR: Giris ekranindaki mevcut dogrulama kutusu
        // (E1'den beri calisan yol) bozulmasin diye. Baglanti EK bir yoldur, YERINE GECEN degil.
        private string DogrulamaGovdesi(string token)
        {
            var link = _links.VitrinBaglantisi("#/dogrula/" + Uri.EscapeDataString(token));
            if (link == null)
                return "Merhaba,\n\nDivisima hesabını doğrulamak için Giriş ekranındaki doğrulama "
                     + $"kutusuna şu kodu gir:\n\n{token}\n\nDivisima";
            return "Merhaba,\n\nDivisima hesabını doğrulamak için aşağıdaki bağlantıya tıkla:\n\n"
                 + $"{link}\n\nBağlantı çalışmazsa Giriş ekranındaki doğrulama kutusuna şu kodu "
                 + $"gir: {token}\n\nDivisima";
        }

        private string SifreSifirlamaGovdesi(string token)
        {
            var link = _links.VitrinBaglantisi("#/sifre-sifirla/" + Uri.EscapeDataString(token));
            if (link == null)
                return "Merhaba,\n\nŞifreni sıfırlamak için Giriş ekranındaki \"Şifremi unuttum\" "
                     + $"adımında açılan kod alanına şu kodu gir (30 dakika geçerli):\n\n{token}\n\n"
                     + "Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; şifren değişmez.\n\nDivisima";
            return "Merhaba,\n\nŞifreni sıfırlamak için aşağıdaki bağlantıya tıkla "
                 + $"(30 dakika geçerli):\n\n{link}\n\nBu isteği sen yapmadıysan bu e-postayı yok "
                 + "sayabilirsin; şifren değişmez.\n\nDivisima";
        }

        // ══ GUVENLIK-FIX (G2) - KAYIT YANITI ARTIK "BU ADRES KAYITLI MI" SORUSUNU YANITLAMIYOR ══
        //
        // OLCULEN ONCE-DURUM:
        //   var olan adres -> HTTP 400 "Bu e-posta adresi zaten kayitli."
        //   yeni adres     -> HTTP 201 "Kaydiniz basariyla olusturuldu."
        // Yani anonim bir caginan, hangi e-postanin kayitli oldugunu TEK istekte ogreniyordu
        // (kimlik avi hedefleme, kredi-doldurma listesi dogrulama). Ayni depoda DOGRU desen
        // zaten vardi: ForgotPassword her iki durumda da AYNI 200'u ve ayni mesaji doner.
        //
        // UYGULANAN DESEN (forgot-password ile birebir): YANIT HER ZAMAN AYNI; gercek kullanici
        // ne oldugunu E-POSTADAN ogrenir. Dort durum, dort FARKLI e-posta, TEK yanit:
        //   1) adres bos                -> hesap acilir + dogrulama jetonu maili (bugunku davranis)
        //   2) hesap var, DOGRULANMIS   -> "zaten hesabin var, giris yap / sifreni sifirla"
        //   3) hesap var, DOGRULANMAMIS -> YENI dogrulama jetonu + mail. Bu bugunkunden IYI UX:
        //      onceden bu kullanici 400 yiyip sikisiyordu - jetonu kaybettiyse yolu KAPALIYDI.
        //   4) hesap var, ASKIYA ALINMIS -> "hesabin askida, destek ile iletisime gec"
        //
        // (3) BILINCLI: jeton yenilemek bekleyen bir dogrulamayi gecersiz kilar. Yeni jeton
        // HESAP SAHIBININ gelen kutusuna gider, yani saldirgan bundan bir sey KAZANMAZ; en fazla
        // kurbanin en yeni maili kullanmasi gerekir. Kazanc: sikisan gercek kullanici kurtulur.
        //
        // DURUST SINIR: bu yanit-esitligi ZAMANLAMAYI esitlemez. Yeni kayit yolu hash+INSERT
        // yapar, var olan yol yapmaz. Olculdu: 400 yolu 9 ms, 201 yolu 14 ms. Sabit-zamanli
        // kayit AYRI bir istir; bu duzeltme YANIT sizintisini kapatir, zamanlama kanalini degil.
        public async Task<(HttpStatusCode, Result)> Register(CustomerRegisterRequestDto dto)
        {
            // GLOBAL FILTREYI ATLAYAN arama: askiya alinmis hesabin adresi GetByEmailAsync'e
            // NULL gorunuyordu ve INSERT unique indekse takilip HTTP 500 donuyordu (olculdu).
            // O 500, yanit esitlendikten SONRA da (201 vs 500) sizintiyi acik birakirdi.
            var mevcut = await _customerDal.GetByEmailIgnoringFiltersAsync(dto.email);
            if (mevcut != null)
            {
                await VarOlanHesabaKayitBildirimiAsync(mevcut);
                return (HttpStatusCode.Created, new SuccessResult(Messages.RegisterSubmitted));
            }

            // Açıklayıcı yorum: HMAC-SHA512 ile şifre hash + salt (Cafixo HashingHelper)
            HashingHelper.CreatePasswordHash(dto.password, out byte[] passwordHash, out byte[] passwordSalt);

            var customer = new Customer
            {
                name = InputSanitizer.Sanitize(dto.name ?? ""),  // stored XSS savunması (admin panelinde render)
                user_type = (byte)UserTypeEnum.Customer,   // yeni kayıt her zaman Customer (admin DB'den atanır)
                email = (dto.email ?? "").Trim().ToLowerInvariant(),   // B1: KIMLIK dizgesi - kultursuz (bkz. EfCustomerDal)
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

            await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
            {
                To = customer.email,
                Subject = "Divisima - E-posta adresinizi doğrulayın",
                Body = DogrulamaGovdesi(customer.email_verification_token)
            });

            return (HttpStatusCode.Created, new SuccessResult(Messages.RegisterSubmitted));
        }

        // ══ GUVENLIK-FIX (G2) - VAR OLAN HESABA BILGI E-POSTASI ═══════════════════════════════
        // "Ayni yanit" tek basina UX'i bozardi: gercek kullanici hicbir sey ogrenemezdi.
        // Bilgi E-POSTAYA tasiniyor - alicisi HER ZAMAN adresin sahibi oldugu icin saldirgana
        // hicbir sey sizmaz. Hesap durumuna gore UC ayri metin.
        private async Task VarOlanHesabaKayitBildirimiAsync(Customer mevcut)
        {
            string konu;
            string govde;

            if (!mevcut.is_active)
            {
                konu = "Divisima - Hesabınız hakkında";
                govde = "Bu adrese kayıt denemesi yapıldı. Hesabınız şu anda askıya alınmış durumda; "
                      + "yeni bir hesap açılmadı. Devam etmek için destek ekibimizle iletişime geçin.";
            }
            else if (mevcut.email_verified)
            {
                konu = "Divisima - Bu adresle zaten bir hesabınız var";
                govde = "Bu adrese kayıt denemesi yapıldı. Zaten bir hesabınız olduğu için yeni hesap "
                      + "açılmadı. Giriş yapabilir, şifrenizi hatırlamıyorsanız Şifremi Unuttum "
                      + "adımıyla yenileyebilirsiniz.";
            }
            else
            {
                // Dogrulanmamis hesap: YENI jeton uretilir - kullanici eski jetonu kaybetmis olabilir
                // ve bugune kadar 400 yiyip sikisiyordu.
                mevcut.email_verification_token = SecureTokenGenerator.Generate();
                mevcut.email_verification_sent_at = DateTime.Now;
                await _customerDal.UpdateAsync(mevcut);
                konu = "Divisima - E-posta adresinizi doğrulayın";
                govde = DogrulamaGovdesi(mevcut.email_verification_token);
            }

            await _outboxService.WriteAsync("EmailNotification", new MailMessageDto { To = mevcut.email, Subject = konu, Body = govde });
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

            // ══ GUVENLIK-FIX-2 (SUPHELI #19) - KILIT BILGISI YALNIZ SIFRE DOGRUYSA ═══════════
            //
            // OLCULEN ONCE-DURUM: kilit kontrolu SIFRE DOGRULAMASINDAN ONCE kosuyordu. Bes
            // basarisiz denemeden sonra KAYITLI bir adres 403 "Cok fazla basarisiz deneme...",
            // KAYITSIZ bir adres 401 "E-posta veya sifre hatali." doner - yani saldirgan bes
            // istek harcayarak adresin kayitli olup olmadigini ogrenebiliyordu. G2/G2b kayit ve
            // dogrulama uclarindaki enumeration kanallarini kapatmisti; bu kanal ACIK KALMISTI.
            //
            // SECILEN COZUM (kullanici karari - secenek iii): kilit ANCAK sifre DOGRUYSA
            // bildirilir. Boylece
            //   yanlis sifre + kilitli hesap  -> 401, KAYITSIZ adresle BIREBIR AYNI yanit
            //   dogru sifre  + kilitli hesap  -> 403 "hesabiniz kilitlendi"
            // Gercek kullanici kaybetmez: sifresini DOGRU yazdiginda kilit mesajini almaya
            // devam eder. Kaybeden yalniz oracle.
            //
            // KILIT UZATMA (DoS) GUARD'I: kilitliyken YANLIS sifre sayaci ARTIRMAZ ve olay
            // YAZMAZ. Aksi halde saldirgan, kilitli bir hesabi surekli yanlis sifreyle doverek
            // kilidi SONSUZA KADAR uzatabilirdi (LockAccountAsync sayaci sifirliyor, yani sayac
            // yeniden 5'e ulasip yeni bir 15 dakika yazardi). Bugunku davranista da kilitliyken
            // sayac artmiyordu (kontrol dogrulamadan once kesiyordu) - o ozellik KORUNUYOR.
            bool kilitli = customer.lockout_end.HasValue && customer.lockout_end.Value > DateTime.Now;
            bool sifreDogru = HashingHelper.VerifyPasswordHash(dto.password, customer.password_hash, customer.password_salt);

            // Açıklayıcı yorum: Şifre yanlışsa başarısız sayacını artır, 5'te 15 dk kilitle.
            if (!sifreDogru)
            {
                if (kilitli)
                {
                    // Hesap ZATEN kilitli: sayac artmaz, olay yazilmaz, kilit uzamaz.
                    // Yanit KAYITSIZ adresin yanitiyla BIREBIR ayni - oracle kapali.
                    return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.LoginFailed));
                }

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

            // SIFRE DOGRU: kilit bilgisi artik SIZINTI DEGIL, kullanicinin bilmesi gereken sey.
            if (kilitli)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.AccountLocked));

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

        // ══ GUVENLIK-FIX (G1) - REFRESH TOKEN YENIDEN KULLANIM TESPITI ═══════════════════════
        //
        // OLCULEN ONCE-DURUM: rotasyon CALISIYORDU (her yenilemede yeni jeton) ve dondurulmus
        // jeton 401 aliyordu - ama HIRSIZLIK SINYALI degerlendirilmiyordu. Olculdu:
        //   1. yenileme (gecerli jeton)                  -> 200, rotasyon VAR
        //   2. yenileme (ESKI/dondurulmus jeton)         -> 401
        //   3. yenileme (YENI jeton, sinyalden SONRA)    -> 200   <-- ZINCIR AYAKTA
        // Yani refresh cerezini calan saldirgan rotasyona devam eder; kurbanin gordugu TEK sey
        // bir 401'dir ve ona gore HICBIR SEY yapilmaz.
        //
        // KOK SEBEP DAL'DAYDI: GetByRefreshTokenAsync sorgusu `&& s.is_active` tasiyor, yani
        // dondurulmus jeton NULL doner. "Bilinmeyen jeton" ile "zaten kullanilmis jeton" ayrimi
        // manager'a HIC ULASMIYORDU - asagidaki `|| !session.is_active` dali OLU koddu.
        //
        // SONRA: ayrim DAL'da geri kazanildi (GetByRefreshTokenAnyStateAsync) ve dondurulmus
        // jeton sunuldugunda O MUSTERININ TUM AKTIF OTURUMLARI kapatilir + Critical guvenlik
        // olayi yazilir (SecurityEventManager: DB + LogWarning + admin bildirimi).
        //
        // KAPSAM KARARI - "zincir" = MUSTERININ TUM AKTIF OTURUMLARI:
        // user_sessions'ta jeton AILESI (parent/family) kolonu YOK. Aile izlemek migration +
        // yeni kolon demek; onun yerine OAuth BCP'nin muhafazakar tavsiyesi uygulandi: sinyal
        // geldiginde HEPSINI kapat. BEDELI ACIK: kullanicinin DIGER cihazlari da cikis yapar.
        // Hirsizlik sinyalinde bu DOGRU taraftir - saldirganin elindeki zinciri ayakta birakmak
        // yerine kullaniciyi bir kez yeniden giris yapmaya zorlar.
        public async Task<(HttpStatusCode, Result)> RefreshToken(RefreshTokenRequestDto dto)
        {
            // DIKKAT: bu cagri `is_active` FILTRESIZ - "kullanilmis jeton" sinyali burada dogar.
            var session = await _userSessionDal.GetByRefreshTokenAnyStateAsync(dto.refresh_token);
            if (session == null)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));

            if (!session.is_active)
            {
                // YENIDEN KULLANIM: bu jeton daha once dondurulmus (ya da cikis yapilmis).
                // Mesru istemci dondurulmus bir jetonu ASLA ikinci kez sunmaz - bu bir sizma isaretidir.
                var kapatilan = await _userSessionDal.InvalidateAllForCustomerAsync(session.customer_id);

                // ALARM YALNIZ GERCEKTEN IPTAL VARSA - OLCUMLE EKLENDI.
                // Ilk yazimda kosulsuz Critical yaziliyordu; pin "2 olay" buldu. Sebep: zincir
                // iptal edildikten SONRA ayni musterinin HERHANGI bir jetonu artik pasif oldugu
                // icin her yeni deneme "yeniden kullanim" gibi gorunuyor. Kosulsuz alarm, tekrar
                // deneyen bir istemcide admin bildirimini SPAM'a cevirirdi ve gercek sinyal
                // gurultuye gomulurdu. `kapatilan == 0` demek "zincir zaten olu" demektir: 401
                // yine doner, ama YENI bir alarm URETILMEZ. Musteri tekrar giris yapip yeni bir
                // aktif oturum acarsa, sonraki bir sizma yine ALARM URETIR.
                if (kapatilan > 0)
                {
                    await _securityEvents.LogAsync("RefreshTokenReuse", "Critical", session.customer_id, null, null,
                        $"Dondurulmus refresh token yeniden sunuldu - oturum zinciri iptal edildi (kapatilan oturum: {kapatilan})");
                }
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));
            }

            // Açıklayıcı yorum: Refresh token süresi dolmuş mu
            if (session.expires_at < DateTime.Now)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenExpired));

            var customer = await _customerDal.GetAsync(c => c.id == session.customer_id);
            if (customer == null || !customer.is_active)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));

            // Açıklayıcı yorum: ROTATION - eski oturumu kapat, yeni oturum+JWT+refresh token üret (merkezi helper).
            // Eski refresh token artık geçersiz (replay engeli); istemci yeni refresh_token'ı cookie'den alır.
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

        // ══ GUVENLIK-FIX (G2b) - RESEND-VERIFICATION DE ENUMERATION KAPISIYDI ════════════════
        //
        // KAPSAM NOTU (durustluk): bu uc (C) dalgasinda OLCULMEMISTI; G2 duzeltilirken yanindaki
        // ayni kapinin acik oldugu gorulup olculdu. Kayit yolunu kapatip burayi acik birakmak
        // G2'yi ANLAMSIZ kilardi - saldirgan ayni soruyu bir uc oteden sorardi.
        //
        // OLCULEN ONCE-DURUM - UC AYRI YANIT (kayit ucundan DAHA cok sizdiriyordu):
        //   olmayan adres          -> HTTP 404 "E-posta veya sifre hatali."
        //   var + DOGRULANMIS      -> HTTP 200 "E-posta zaten dogrulanmis."
        //   var + DOGRULANMAMIS    -> HTTP 200 "Dogrulama e-postasi gonderildi."
        // Yani hem VARLIK hem DOGRULANMA DURUMU sizdiriliyordu.
        //
        // SONRA: TEK yanit (200 + ayni metin, forgot-password kalibi). Ayrim E-POSTADA:
        //   olmayan adres       -> hicbir sey gonderilmez
        //   var + dogrulanmamis -> YENI jeton (bugunku davranis, aynen)
        //   var + dogrulanmis   -> "hesabin zaten dogrulanmis, giris yapabilirsin" bilgisi
        //   var + askida        -> hicbir sey gonderilmez (dogrulama zaten ise yaramaz)
        public async Task<(HttpStatusCode, Result)> ResendVerification(string email)
        {
            var customer = await _customerDal.GetByEmailAsync(email);

            if (customer != null && customer.email_verified)
            {
                await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = customer.email,
                    Subject = "Divisima - Hesabınız zaten doğrulanmış",
                    Body = "Bu adres için yeniden doğrulama isteği alındı. Hesabınız zaten doğrulanmış "
                         + "durumda; doğrudan giriş yapabilirsiniz."
                });
            }
            else if (customer != null)
            {
                customer.email_verification_token = SecureTokenGenerator.Generate();
                customer.email_verification_sent_at = DateTime.Now;
                await _customerDal.UpdateAsync(customer);
                await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = customer.email,
                    // A3: "(yeniden)" KALDIRILDI. Bu dal iki durumda kosuyor: (a) kullanici
                    // ilk maili hic almadi, (b) misafir checkout'u bu ucu ILK KEZ tetikliyor.
                    // Ikisinde de "yeniden" YANLIS bir sey soyluyordu. Kayit mailiyle ayni konu.
                    Subject = "Divisima - E-posta adresinizi doğrulayın",
                    Body = DogrulamaGovdesi(customer.email_verification_token)
                });
            }

            // Adres kayitli OLMASA da AYNI yanit - varlik sizdirilmaz.
            return (HttpStatusCode.OK, new SuccessResult(Messages.EmailVerificationRequested));
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
                await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = customer.email,
                    Subject = "Divisima - Şifre sıfırlama",
                    Body = SifreSifirlamaGovdesi(customer.password_reset_token)
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
            // A2-FIX: burada AYNI kontrolun IKINCI bir kopyasi vardi (farkli mesajla) ve
            // ULASILAMAZDI - ustteki guard zaten donuyordu. Olu kod kaldirildi.

            // A2-FIX (SUPHELI #21) - ASIL BULGU: bu uc SIFREYE HIC BAKMIYORDU. dto.new_password
            // dogrudan CreatePasswordHash'e gidiyordu; yani "Sifremi unuttum" ile gelen biri,
            // KAYITTA reddedilecek bir sifreyi (ornegin "abc") belirleyebiliyordu. Bir politika
            // ancak EN ZAYIF girisi kadar gucludur ve bu, atlatilmasi EN KOLAY yoldu.
            // JETON KONTROLUNDEN ONCE: gecerli bir jetonu, sifre zaten reddedilecekse
            // HARCAMAYALIM (jeton TEK KULLANIMLIK - asagida null'laniyor).
            var sifreHatasi = SifrePolitikasi.Dogrula(dto.new_password);
            if (sifreHatasi != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(sifreHatasi));

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
            // NOT NULL TUZAGI (AccountManager.DeleteAccount ile ayni): customers.phone NOT NULL.
            // null yazmak silme ucunu 500 ile dusuruyordu. Anonim yer tutucu yazilir.
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

            // Hesap durumu cache'ini düşür - silinen hesabın access token'ı bir sonraki istekte
            // TokenBlacklistMiddleware tarafından reddedilsin (TTL beklenmesin).
            _cache.Remove(CacheKeys.CustomerActive(customerId));

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
