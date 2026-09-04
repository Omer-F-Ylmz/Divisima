using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Security.JWT;
using Divisima.Core.Security.Tokens;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Http;
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

        // GF-1 / K2: access token iptali icin kara liste. Program.cs'te AddScoped ile kayitli.
        private readonly Divisima.Core.Security.JWT.ITokenBlacklist _tokenBlacklist;
        // GF-1b / K1: "tum cihazlardan cik" dalinda toplu iptal esigini yazar.
        private readonly Divisima.Core.Security.JWT.IUserTokenRevocation _tokenRevocation;
        // GF-3 / K10: refresh rotasyonunun UC yazmasini (CAS + denetim + yeni oturum) TEK
        // transaction'a almak icin. Gerekce RefreshToken govdesinde.
        private readonly Divisima.Core.DataAccess.IUnitOfWork _unitOfWork;

        // GF-1b / K6: oturum satirina cihaz/IP yazmak icin (gerekce IssueSessionAndTokenAsync'te).
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;
        // GF-1b / F2: CAS yollarinda denetim izi (gerekce DenetimKaydiYazAsync'in basinda).
        private readonly IAuditLogDal _auditLogDal;
        // Access token omru - iptal kaydinin TTL'i bundan turer (appsettings.json:8 ile AYNI).
        private const int AccessTokenOmruDk = 15;

        // Jeton bitisi okunamazsa kullanilan TTL. Access token omru 15 dk oldugundan bu
        // ust siniri ASLA gecmez; iptal kaydi jetonun kendisinden UZUN yasamaz.
        private static readonly TimeSpan VarsayilanIptalTtl = TimeSpan.FromMinutes(15);

        public AuthManager(ICustomerDal customerDal, IUserSessionDal userSessionDal, ITokenHelper tokenHelper, IMailService mailService, ISecurityEventService securityEvents,
            IReferralService referralService, IConsentRecordDal consentDal,
            IMailLinkBuilder links, Divisima.Bussiness.Outbox.IOutboxService outboxService,
            Divisima.Core.Security.JWT.ITokenBlacklist tokenBlacklist,
            Divisima.Core.Security.JWT.IUserTokenRevocation tokenRevocation,
            IAuditLogDal auditLogDal,
            Divisima.Core.DataAccess.IUnitOfWork unitOfWork,
            Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null)
        {
            _unitOfWork = unitOfWork;
            _tokenBlacklist = tokenBlacklist;
            _tokenRevocation = tokenRevocation;
            _auditLogDal = auditLogDal;
            _httpContextAccessor = httpContextAccessor;
            _outboxService = outboxService;
            _links = links;
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
                // ══ GF-5 / K2 - KAYITSIZ E-POSTA ILE GIRIS DENEMESI ARTIK IZ BIRAKIYOR ══════
                //
                // OLCULEN ONCE-DURUM (AV-2 / S-C): bu dal HICBIR SEY yazmiyordu; matriste
                // "login basarisiz (KAYITSIZ)" TAM BOSLUKTU. Yani bir saldirgan binlerce
                // e-postayi deneyip gecebiliyor, geriye TEK SATIR iz kalmiyordu.
                //
                // `customer_id` NULL, `detail` SABIT METIN - E-POSTA YAZILMIYOR (bilincli):
                // (a) KVKK - `security_events` bugun kimlik tasimiyor, yeni bir veri sinifi
                //     ACILMAZ; (b) LOG FORGING - `detail` `SecurityEventManager.cs`teki Serilog
                //     sablonuna giriyor ve Serilog CRLF AYIKLAMAZ (GF-3/A-3); kullanici
                //     kontrollu bir deger buraya konursa saldirgan log satiri BOLEBILIR.
                //     Ayirt edicilik KAYBOLMUYOR: "LoginFailed + customer_id NULL" kombinasyonu
                //     kayitsiz denemeyi, "customer_id DOLU" ise gercek hesaba yanlis sifreyi
                //     gosterir. IP/user-agent K1 ile ZATEN doluyor - alarm kurali icin yeten
                //     eksen budur ("ayni IP'den 10 basarisiz login").
                await _securityEvents.LogAsync("LoginFailed", "Warning", null, null, null,
                    "Kayıtlı olmayan e-posta ile giriş denemesi");
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

            // ══ GF-1 / K6 (C-4) - SESSIZ v1 -> v2 YENIDEN YAZIM ════════════════════════════
            //
            // Eski (HMAC-SHA512, iterasyonsuz) kayitlar DOGRULANMAYA devam eder; kullanici
            // DOGRU sifresini girdigi ANDA - yani duz sifre elimizdeyken - kayit PBKDF2'ye
            // tasinir. Kullanicidan hicbir sey istenmez, sifre DEGISMEZ.
            //
            // SIRA KRITIK: yalniz `sifreDogru` dalinda. Yanlis sifreyle cagrilsaydi elimizde
            // dogru parola OLMAZDI ve hesap KILITLENIRDI.
            // Anonimlestirilmis (0 bayt) kayitlar `SurumGuncelGerekiyorMu` tarafindan ZATEN
            // disarida birakiliyor - onlara DOKUNULMAZ.
            if (sifreDogru && HashingHelper.SurumGuncelGerekiyorMu(customer.password_hash))
            {
                HashingHelper.CreatePasswordHash(dto.password, out var yeniHash, out var yeniTuz);
                customer.password_hash = yeniHash;
                customer.password_salt = yeniTuz;
                await _customerDal.UpdateAsync(customer);
            }

            // Açıklayıcı yorum: Şifre yanlışsa başarısız sayacını artır, 5'te 15 dk kilitle.
            if (!sifreDogru)
            {
                if (kilitli)
                {
                    // Hesap ZATEN kilitli: sayac artmaz, kilit uzamaz.
                    // Yanit KAYITSIZ adresin yanitiyla BIREBIR ayni - oracle kapali.
                    //
                    // ══ GF-5 / K2 - "OLAY YAZILMAZ" KAYDI DEGISTI, GEREKCESI KORUNDU ═══════
                    //
                    // Bu satirda eskiden "olay yazilmaz" yaziyordu ve GEREKCESI su cumleydi:
                    // yanit kayitsiz adresin yanitiyla BIREBIR ayni kalsin. K2 kayitsiz dala
                    // (`:259-266`) bir olay yazmasi eklediginde o gerekce TERSINE DONDU:
                    // kayitsiz dal bir DB INSERT yaparken bu dal yapmasaydi, KILITLI hesap
                    // OLCULEBILIR SEKILDE DAHA HIZLI yanit verir ve saldirgan "bu e-posta VAR
                    // ve kilitli" bilgisini SURE FARKINDAN cikarabilirdi - yani tam da
                    // kapatilmis olan oracle YENIDEN ACILIRDI.
                    //
                    // Yani buraya olay eklemek o karari BOZMAZ, KORUR: iki dal da ayni isi
                    // yapar. Yan kazanc, kayitli bir bosluktu - kilitli bir hesaba yapilan
                    // israrli denemeler bugune kadar HICBIR IZ birakmiyordu.
                    // `customer_id` YAZILIR (hesap bilinmektedir); `detail` sabit metindir.
                    await _securityEvents.LogAsync("LoginFailed", "Warning", customer.id, null, null,
                        "Kilitli hesaba giriş denemesi");
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
            var response = await IssueSessionAndTokenAsync(customer, DateTime.UtcNow);
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
            var response = await IssueSessionAndTokenAsync(customer, DateTime.UtcNow);
            return (HttpStatusCode.OK, new SuccessDataResult<CustomerLoginResponseDto>(response, Messages.LoginSuccess));
        }

        // Açıklayıcı yorum: MERKEZİ oturum+token üretimi (login / 2FA-doğrulama / refresh HEPSİ buradan - DRY).
        // JWT + kriptografik refresh_token üretir, oturumu KAYDEDER (refresh_token + refresh penceresi expiry), response döner.
        // Önceden 3 yerde tekrarlanıyordu ve refresh_token HİÇ set edilmiyordu (refresh mekanizması ölüydü).
        // GF-1b / K5: deger artik BURADA DEGIL - `OturumOmru.RefreshGun` tek kaynaktir ve
        // refresh cerezinin omru de ORADAN turer (ikisi 7 / 30 diye AYRISIYORDU).
        private const int RefreshTokenDays = OturumOmru.RefreshGun;
        // ══ GF-1 / K3 (C-2) - `devralinanAuthTime` ═════════════════════════════════════════
        //
        // Bu helper UC yolu birden besliyor: login, 2FA dogrulamasi ve REFRESH ROTASYONU.
        //   login / 2FA  -> `null` gecilir  => auth_time = SIMDI (ikisi de KIMLIK DOGRULAMADIR;
        //                   2FA sonrasi step-up'in acilmasi DOGRUDUR, haksiz degil)
        //   refresh      -> ESKI oturumun auth_time'i gecilir => step-up saati SIFIRLANMAZ
        // `created_at` bu isi GOREMEZ: bu metot her cagrida YENI satir ekler, yani rotasyondan
        // sonra `created_at` ROTASYON anidir. (Ilk olcumumde bunu yanlis degerlendirmistim.)
        //
        // UTC YAZILIR: `RequireRecentAuth` karsilastirmayi `DateTime.UtcNow` ile yapiyor.
        // Dosyanin geri kalani `DateTime.Now` (yerel) kullaniyor - bu alan BILINCLI OLARAK
        // AYRISIYOR ve jeton tarafinda Kind Utc'ye sabitleniyor (bkz. JwtHelper).
        // ══ GF-1b / K-7 - MIRAS (NULL) auth_time ARTIK FAIL-CLOSED ════════════════════════
        //
        // OLCULEN BOSLUK: `auth_time` NULL olan oturumlarda bu metot `DateTime.UtcNow`a
        // dusuyordu, yani NULL'lu bir oturumun ILK refresh'i step-up saatini SIFIRLIYOR ve
        // TAM 10 dakikalik pencere aciyordu. Canli olcum: aktif ve suresi dolmamis 70
        // oturumun 69'u (%98,6) NULL. Ustelik istemci 401'de otomatik refresh yaptigi icin
        // step-up SESSIZCE atlatiliyordu - yani K2'nin step-up bacagi bugunku oturumlarin
        // neredeyse tamaminda ETKISIZ olurdu.
        //
        // COZUM (merkez karari, secenek 3): NULL "BILINMIYOR" demektir ve jetona EPOCH
        // yazilir -> step-up filtresi bunu "sonsuz eski" gorup 401 verir (FAIL-CLOSED).
        // DIGER YOLLARDA STATUKO: satir NULL KALIR (geriye donuk doldurma YOK) ve jeton
        // her sey icin gecerlidir; yalnizca HASSAS islemler yeniden giris ister.
        // `JwtHelper` DEGISMEDI - epoch'u cagiran taraf veriyor, yani `SellerAuthManager`in
        // (DOKUNULMAZ) jeton uretimi ETKILENMEZ.
        //
        // CAGRI YERLERI ARTIK ACIK: login ve 2FA `DateTime.UtcNow` GECER (ikisi de KIMLIK
        // DOGRULAMADIR), refresh `session.auth_time` gecer (NULL olabilir = bilinmiyor).
        private static readonly DateTime BilinmeyenAuthTime = DateTime.UnixEpoch;

        // ══ GF-1b / K6 (GF1-B7) - OTURUM SATIRI ARTIK KIMIN/NEREDEN OLDUGUNU TASIYOR ═══════
        //
        // OLCULEN ONCE-DURUM: `user_sessions.device` (nvarchar 200) ve `ip_address` (nvarchar 64)
        // kolonlari SEMADA VARDI, migration'da VARDI, DbContext'te esleniyordu - ama HICBIR
        // uretim yolu bu alanlara YAZMIYORDU (grep: tek yazan yer bir TEST fiksturuydu).
        // Sonuc: "cihazlarim" turu bir ekran yapilamiyordu ve daha onemlisi, GF-1b/K4'un
        // atesledigi `RefreshTokenReuse` KRITIK olayinda "hangi cihaz/IP" sorusu
        // YANITSIZ kaliyordu - hirsizlik sinyali var, izi YOK.
        //
        // NEDEN IMZA DEGISMEDI: `IssueSessionAndTokenAsync` UC yolu birden besliyor ve GF-1/K3
        // pini cagri bicimlerini KAYNAK duzeyinde tariyor. Degerleri parametreye tasimak o
        // pinleri kirardi ve uc cagri yerinde AYNI okumanin UC KOPYASINI acardi. Bunun yerine
        // deger, ZATEN kayitli olan `IHttpContextAccessor`dan TEK YERDE okunuyor.
        //
        // SINIR NOTU (bilincli): GF-1/K2'de "is katmani HTTP baglamini GORMEZ" siniri yazildi
        // ve `jti`/`exp` controller'dan PARAMETRE olarak gecirildi. Burada aksi yapildi cunku
        // (a) imza pinlerini kirmamak, (b) uc cagri yerinde kopya acmamak gerekiyordu; ayrica
        // ayni kalip `AuditInterceptor`da (Divisima.Dal) ZATEN kullaniliyor. Bagimlilik
        // TEK METODLA sinirli ve HttpContext YOKSA (arka plan isi, birim testi) alanlar
        // sessizce null kalir - akis BOZULMAZ.
        //
        // MASKELEME/SINIR: user-agent DB kolonu 200, IP kolonu 64 karakter. Uzun degerler
        // KIRPILIR - kirpmadan yazmak EF tarafinda insert-time 500 uretirdi (`guest_name`
        // ailesinin ayni tuzagi).
        //
        // ══ GF-5 / K1 - IKI DUZELTME (biri OLCULEN HATA, biri KIRPMA SINIRI) ══════════════
        //
        // (1) BU YORUM YANLISTI - OLCULDU VE DUZELTILDI. Onceki hali "PII notu: ikisi de zaten
        //     `security_events` tablosunda TUTULUYOR; yeni bir veri sinifi ACILMIYOR" diyordu.
        //     TUTULMUYORDU: `LogAsync`in ip/userAgent argumanlari YEDI cagri yerinin yedisinde
        //     de `null` geciliyordu ve canli tabloda 40 satirin 40'inda ikisi de NULL'di
        //     (AV-2/SC-1 olcumu, GF-5 on olcumunde iki ajan tarafindan bagimsiz dogrulandi).
        //     Yani cumle bir VARSAYIMI olcum gibi yaziyordu. GF-5/K1 ile iddia ARTIK DOGRU:
        //     `SecurityEventManager` degerleri kendi icinde dolduruyor. "YORUM != OLCUM"
        //     dersinin bu depodaki kayitli ornegi budur.
        //
        // (2) KIRPMA ARTIK 60, 64 DEGIL. `security_events.ip_address` kolonu 60 KARAKTER
        //     (sys.columns olcumu), `user_sessions.ip_address` ise 64. Ayni deger artik IKI
        //     tabloya da gidiyor; tek sinir SECILDI ve KUCUK OLAN alindi (60 <= 60 ve 60 <= 64).
        //     Bu, user_sessions tarafinda bilincli bir SIKILASTIRMADIR ve gercek bir adresi
        //     KIRPMAZ: en uzun IPv6 metni 45 karakterdir. Gerekce IstemciBilgisi'nin basinda.
        //
        // OKUMA ARTIK BURADA DEGIL - TEK NOKTA `Divisima.Core.Utilities.Http.IstemciBilgisi`.
        // K1 ucuncu bir okuyucu (`SecurityEventManager`) ekliyordu; ucuncu kopyayi acmak
        // yerine okuma+kirpma ortak yardimciya tasindi. Asagidaki iki metot KORUNDU (cagri
        // yerleri ve `IssueSessionAndTokenAsync` govdesi DEGISMESIN diye) ama artik yalnizca
        // DEVREDIYOR. X-Forwarded-For'un neden okunmadigi da orada yazili.
        private string? KisaltUserAgent() => IstemciBilgisi.UserAgent(_httpContextAccessor);

        private string? IstemciIp() => IstemciBilgisi.Ip(_httpContextAccessor);

        private async Task<CustomerLoginResponseDto> IssueSessionAndTokenAsync(Customer customer,
            DateTime? authTime)
        {
            var accessToken = _tokenHelper.CreateToken(customer, authTime ?? BilinmeyenAuthTime);
            var refreshToken = SecureTokenGenerator.Generate();
            await _userSessionDal.AddAsync(new UserSession
            {
                customer_id = customer.id,
                // GF-1b / K3: DB'de OZET durur, istemciye DUZ jeton doner (asagida).
                // DB okuma yetkisi ya da bir yedek dosyasi artik CANLI oturum jetonu VERMEZ.
                refresh_token = JetonOzeti.Hesapla(refreshToken),
                // ══ GF-3 / K11 - OTURUM ZAMAN EKSENI UTC ═══════════════════════════════════
                // DAR KAPSAM (merkez karari): yalniz `expires_at` · `created_at` · JWT
                // `exp`/`nbf`. Ayni dosyadaki `lockout_end`, `password_reset_expiry` ve
                // `two_factor_code_expiry` YEREL kalir ve BILINEN olarak kaydedilir - gerekce:
                // her biri KENDI okuyucusuyla CIFT halinde tasinmak zorunda ve `lockout_end`in
                // UCUNCU okuyucusu `SellerAuthManager` DOKUNULMAZ listesinde. Kismi bir gecis
                // kilidi ANINDA gecersiz kilardi (kaba kuvvet korumasi sessizce kapanirdi).
                //
                // MEVCUT SATIRLAR: bu degisiklikten ONCE yazilmis `expires_at` degerleri YEREL
                // eksende duruyor; UtcNow ile karsilastirilinca tr-TR'de (UTC+3) uc saat DAHA
                // UZUN yasarlar. Launch oncesi KABUL (merkez karari, D-YAN); geriye donuk
                // donusum YAPILMADI.
                expires_at = DateTime.UtcNow.AddDays(RefreshTokenDays),
                is_active = true,
                created_at = DateTime.UtcNow,   // GF-3/K11 - expires_at ile AYNI eksende
                auth_time = authTime,
                // GF-1b / K6 (GF1-B7): bu iki kolon VARDI ama HICBIR uretim yolu YAZMIYORDU.
                device = KisaltUserAgent(),
                ip_address = IstemciIp()
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
        // ══ YENIDEN KULLANIM KURALI - TEK KAYNAK ══════════════════════════════════════════
        //
        // Refresh jetonunun yeniden kullanildigini IKI ayri yol tespit edebilir:
        //   (a) sunulan jeton ZATEN pasif  -> jeton daha once dondurulmus/cikis yapilmis
        //   (b) atomik kapatma yarisi KAYBEDILDI (K4) -> ayni jeton AYNI ANDA iki kez sunuldu
        // Ikisinin de yanIti AYNIDIR ve o yanit BURADA, TEK YERDE durur. Ilk K4 yaziminda
        // (b) icin satir ici bir kopya acilmisti; MK-4b denetcisi olcup yakaladi (ITIRAZ-2).
        //
        // ══ GF-1b / F2 - CAS YOLLARINDA DENETIM KAYDI ELLE YAZILIR ════════════════════════
        //
        // OLCULEN ONCE-DURUM (MK-4b rapor denetcisi BULGU-3, kendi olcumumle dogrulandi):
        // `AuditInterceptor` bir `SaveChangesInterceptor`tir ve `ChangeTracker.Entries()`
        // uzerinden calisir. `ExecuteUpdateAsync` SaveChanges'i ATLAR - dolayisiyla bu
        // dalganin actigi CAS yollari denetim izi BIRAKMIYORDU:
        //   K10 basarili sifre sifirlama -> `audit_logs` satiri YOK (ve `security_events` de
        //        YOKTU: `ResetPassword` olayi hic yazilmiyordu). Guvenlik acisindan EN
        //        onemli olaylardan biri TAMAMEN IZSIZ kaliyordu.
        //   K4 rotasyon kapatmasi        -> `audit_logs` satiri YOK
        //   toplu oturum iptali          -> zaten yoktu (InvalidateAll ONCEDEN de ExecuteUpdate)
        //
        // COZUM YENI SOYUTLAMA ACMAZ: `IAuditLogDal` MEVCUT yazma API'sidir (`AddAsync`) ve
        // `AuditInterceptor._ignored` ZATEN `AuditLog`u disliyor, yani bu yazim kendi kendini
        // tetiklemez. Interceptor'a HIC dokunulmadi - o, tracked yazmalarda calismaya devam eder.
        //
        // `changes` alani JSON DEGIL duz aciklamadir: interceptor'in urettigi eski->yeni
        // JSON'u burada TAKLIT etmek, CAS'in eski degeri OKUMADIGI gercegini gizlerdi.
        // Kolon `nullable` ve serbest metin; okuyucu (AuditLogController) alani AYNEN gecirir.
        //
        // `action` KOLONU 20 KARAKTER (DivisimaDbContext.cs:620) - OLCULDU, TAHMIN EDILMEDI:
        // ilk yazimda "session_chain_revoked" (21) kullanildi ve SQL Server
        // "String or binary data would be truncated" ile DUSTU; K4B pini bunu yakaladi.
        // Eylem adlari bu yuzden KISA tutulur ve asagida kirpma da vardir - sinir asilirsa
        // denetim kaydi yazilamaz ve BUTUN AKIS duser (kayit yan etki degil, YOLUN PARCASI).
        private const int EylemEnUzun = 20;

        private async Task DenetimKaydiYazAsync(string tablo, int kayitId, string eylem, string aciklama)
        {
            await _auditLogDal.AddAsync(new AuditLog
            {
                table_name = tablo,
                entity_id = kayitId.ToString(),
                action = eylem.Length <= EylemEnUzun ? eylem : eylem.Substring(0, EylemEnUzun),
                changes = aciklama,
                user_id = kayitId.ToString(),
                created_at = DateTime.Now
            });
        }

        // ══ ALARM KOSULU IKI YOLDA FARKLIDIR - GF-1b/F1 (L3 DENETCISI OLCTU) ══════════════
        //
        // (a) PASIF JETON YOLU - alarm `kapatilan > 0` KOSULUNA baglidir. Gerekce OLCULDU:
        //     zincir iptal edildikten SONRA o musterinin HER jetonu pasiftir, dolayisiyla
        //     tekrar deneyen mesru bir istemci her seferinde "yeniden kullanim" gibi gorunur.
        //     Kosulsuz alarm burada admin bildirimini SPAM'a cevirir ve gercek sinyali gomer.
        //
        // (b) CAS YARISI KAYBI YOLU - alarm KOSULSUZ yazilir. Gerekce: CAS'i kaybetmek,
        //     "ayni jeton AYNI ANDA iki kez sunuldu"nun TEK BASINA KESIN kanitidir; tekrar
        //     denemeyle uretilemez, dolayisiyla SPAM riski YOKTUR.
        //     L3 DENETCISI OLCTU: kaybeden yol `InvalidateAllForCustomerAsync`i KAZANANIN yeni
        //     oturumu INSERT edilmeden ONCE kosarsa etkilenen satir 0 olur; eski kosullu alarm
        //     bu durumda HIC YAZILMIYORDU. Olculen sıklık: kapili duzenekte 23 turun 15'inde,
        //     K4B deseninde 25 turun 19'unda. Yani hirsizlik sinyali TEK TURDA GARANTI DEGILDI.
        //     Artik alarm HER ZAMAN yazilir - iptal sayisi kac olursa olsun.
        //
        // AILE IPTALI BEST-EFFORT'TUR (BILINEN, merkez karari): kaybeden kazananin INSERT'inden
        // once kosarsa aile iptali O TURDA gecikir; kaybeden eski jetonla ikinci kez denedigin-
        // de (a) yoluna duser ve zincir O ZAMAN kapanir. KALICI COZUM GF-3'e devredildi:
        // rotasyon TEK DB TRANSACTION'i olacak (CAS + INSERT birlikte commit), boylece kaybeden
        // CAS'i ancak commit SONRASI gorur ve supurme kazananin satirini DA kapsar.
        private async Task<(HttpStatusCode, Result)> YenidenKullanimiIsleAsync(
            int customerId, string sebep, bool alarmKosulsuz)
        {
            var kapatilan = await _userSessionDal.InvalidateAllForCustomerAsync(customerId);
            if (alarmKosulsuz || kapatilan > 0)
            {
                await _securityEvents.LogAsync("RefreshTokenReuse", "Critical", customerId, null, null,
                    $"{sebep} - oturum zinciri iptal edildi (kapatilan oturum: {kapatilan})");
            }
            // GF-1b / F2: toplu iptal ExecuteUpdateAsync'tir, interceptor GORMEZ.
            if (kapatilan > 0)
                await DenetimKaydiYazAsync(nameof(UserSession), customerId, "chain_revoked",
                    $"{sebep}; kapatilan oturum: {kapatilan}");
            return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));
        }

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
                return await YenidenKullanimiIsleAsync(session.customer_id,
                    "Dondurulmus refresh token yeniden sunuldu", alarmKosulsuz: false);
            }

            // Açıklayıcı yorum: Refresh token süresi dolmuş mu
            // GF-3/K11: YAZAN ve OKUYAN AYNI ANDA tasindi. Biri tasinip oteki birakilsaydi
            // tr-TR'de (UTC+3) ya oturum ANINDA gecersiz olurdu ya da uc saat fazla yasardi -
            // kismi gecisin iki yonu de hasar verir.
            if (session.expires_at < DateTime.UtcNow)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenExpired));

            var customer = await _customerDal.GetAsync(c => c.id == session.customer_id);
            if (customer == null || !customer.is_active)
                return (HttpStatusCode.Unauthorized, new ErrorResult(Messages.RefreshTokenInvalid));

            // ══ GF-1b / K4 (GF1-B5) - ROTASYON ARTIK ATOMIK (CAS) ═════════════════════════
            //
            // OLCULEN ONCE-DURUM: eski satir `session.is_active = false` + TRACKED tam-varlik
            // `UpdateAsync` ile kapatiliyordu; kosul YOKTU. Iki es zamanli refresh ISTEGI de
            // ayni satiri "aktif" gorup gecebiliyor ve TEK jetondan IKI GECERLI OTURUM
            // doguyordu - ustelik bu, hirsizlik sinyalini de ATESLEMIYORDU.
            // (Iki ayri SaveChanges, transaction YOK, `IsRowVersion` YOK - hepsi olculdu.)
            //
            // COZUM: kapatma `WHERE is_active = 1` sartiyla VERITABANINDA yapilir. Etkilenen
            // satir 1 DEGILSE yaris kaybedilmistir - yani ayni jeton bir kez daha sunulmus
            // demektir ve bu, YENIDEN KULLANIM sinyalinin ta kendisidir.
            //
            // ── DUZELTME (MK-4b rapor denetcisi / ITIRAZ-2) ────────────────────────────────
            // ILK YAZIMDA BURAYA SU YAZILMISTI: "IKINCI KOPYA ACILMADI - kaybeden yol YUKARIDA
            // ZATEN VAR OLAN reuse dalina gider." **BU IDDIA YANLISTI ve KOD ONU YALANLIYORDU:**
            // kaybeden yol o dala GITMIYORDU, kuralin (iptal + kosullu Critical + 401) SATIR ICI
            // IKINCI BIR KOPYASINI kosuyordu. Olculdu: Critical olay yazan cagri sayisi
            // b857fd3'te 1, ilk K4 yaziminda 2. Yani bu depoda YEDI KEZ bedeli odenmis
            // "ayni kuralin ikinci kopyasi" ailesinin YENI bir ornegi acilmisti - ustelik
            // acmadigini SOYLEYEN bir yorumla birlikte.
            // SIMDI GERCEKTEN TEK KAYNAK: iki yol da `YenidenKullanimiIsleAsync`i cagirir.
            //
            // DIKKAT (CLAUDE.md tuzagi): `ExecuteUpdateAsync` change-tracker'i ATLAR, yani
            // elimizdeki TRACKED `session` nesnesi BAYAT kalir. Bu noktadan sonra o nesne
            // uzerinden TAM-VARLIK yazma YAPILMAZ - yalnizca `auth_time` degeri OKUNUR.
            // ══ GF-3 / K10 (GF-1b BILINEN #5) - ROTASYON TEK TRANSACTION ═══════════════════
            //
            // OLCULEN ONCE-DURUM: basarili yolda UC AYRI COMMIT NOKTASI vardi ve transaction
            // YOKTU - CAS kapatmasi, denetim satiri ve YENI OTURUM INSERT'i ayri ayri
            // kalicilasiyordu. Sonucu GF-1b'de "BILINEN #5" olarak kaydedilmisti:
            //   "es zamanli yarista KAYBEDEN, kazananin INSERT'inden ONCE kosarsa aile iptali
            //    o turda gerceklesmez; ikinci denemede yakalanir" (BEST-EFFORT).
            // Yani hirsizlik sinyali ateslenirken YENI acilan oturum HENUZ GORUNMUYOR olabilir
            // ve iptal onu ISKALIYORDU.
            //
            // COZUM: uc yazma TEK transaction'da. Kaybeden istegin CAS'i, kazananin satir
            // kilidinde BEKLER; kilit ancak COMMIT ile birakilir ve o an INSERT de kalicidir.
            // Dolayisiyla kaybeden `YenidenKullanimiIsleAsync`e girdiginde yeni oturumu
            // MUTLAKA gorur -> aile iptali DETERMINISTIK olur, best-effort degil.
            //
            // NEDEN `ExecuteInTransactionAsync` (BeginTransaction DEGIL): `EnableRetryOnFailure`
            // ile uyumlu TEK yol odur (execution strategy begin->is->commit'i tek retriable
            // delege olarak sarar). Bugun retry KAPALI ama sozlesme ileriye donuk korunur.
            //
            // DEADLOCK OLCUMU (TAVIZ kriteri): ic ice transaction YAPISAL OLARAK IMKANSIZ
            // (`UnitOfWork` tek `_transaction` alani tutar) ve bu blok yalniz `user_sessions`
            // (ayni satir) + `audit_logs` (INSERT) tabloIarina dokunur - kilit sirasi TEK YONLU,
            // dongu olusmaz. Kaybeden yolun SignalR yayini ve aile iptali transaction'in
            // DISINDA kalir (bilincli: ag cagrisi kilit altinda tutulmaz).
            var yeniOturum = await _unitOfWork.ExecuteInTransactionAsync<CustomerLoginResponseDto?>(async () =>
            {
                var n = await _userSessionDal.DeactivateIfActiveAsync(session.id);
                if (n != 1) return null;   // YARIS KAYBEDILDI - hicbir sey yazilmadi, commit no-op

                // GF-1b / F2: rotasyon kapatmasi da CAS'tir (DeactivateIfActiveAsync ->
                // ExecuteUpdateAsync), interceptor GORMEZ. Kapanan oturum kayda gecer.
                await DenetimKaydiYazAsync(nameof(UserSession), session.id, "session_rotated",
                    $"refresh rotasyonu: oturum kapatildi (musteri: {session.customer_id})");

                // GF-1 / K3: ESKI oturumun giris ani YENI satira TASINIR - refresh step-up
                // saatini SIFIRLAMAZ. `null` (GF-1 oncesi acilmis oturum) ise IssueSession
                // `simdi` kullanir, yani o satirlarda davranis STATUKO kalir.
                return await IssueSessionAndTokenAsync(customer, session.auth_time);
            });

            if (yeniOturum == null)
                return await YenidenKullanimiIsleAsync(session.customer_id,
                    "Es zamanli refresh yarisi kaybedildi - ayni jeton iki kez sunuldu",
                    alarmKosulsuz: true);

            return (HttpStatusCode.OK, new SuccessDataResult<CustomerLoginResponseDto>(yeniOturum, Messages.TokenRefreshed));
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
                // ══ GF-1b / K3 - DUZ JETON MAILE, OZET DB'YE ═══════════════════════════════
                // DUZ deger YALNIZ kullanicinin gelen kutusuna gider; DB'de yalniz ozeti
                // durur. Boylece DB okuma yetkisi tek basina HESAP ELE GECIRMEYE yetmez.
                var sifirlamaJetonu = SecureTokenGenerator.Generate();
                customer.password_reset_token = JetonOzeti.Hesapla(sifirlamaJetonu);
                customer.password_reset_expiry = DateTime.Now.AddMinutes(30); // kısa ömür
                await _customerDal.UpdateAsync(customer);
                await _outboxService.WriteAsync("EmailNotification", new MailMessageDto
                {
                    To = customer.email,
                    Subject = "Divisima - Şifre sıfırlama",
                    Body = SifreSifirlamaGovdesi(sifirlamaJetonu)
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

            var jetonOzeti = JetonOzeti.Hesapla(dto.token);
            var customer = await _customerDal.GetAsync(c => c.password_reset_token == jetonOzeti);
            if (customer == null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordResetInvalid));
            if (!customer.password_reset_expiry.HasValue || customer.password_reset_expiry.Value < DateTime.Now)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordResetExpired));

            // ══ GF-1b / K10 (GF1-B10) - JETON TUKETIMI ARTIK ATOMIK ═══════════════════════
            //
            // OLCULEN ONCE-DURUM (3/3 yeniden uretildi): yukaridaki okuma ile asagidaki yazma
            // arasinda KOSUL YOKTU. Ayni jetonu AYNI ANDA sunan IKI istek de "gecerli" gorup
            // geciyordu - ikisi de 200 donuyor, ikisi de sifre yaziyordu.
            //   assert: es zamanli 2 istekten OK sayisi -> beklenen 1, OLCULEN **2**
            // ZARAR: jetonu ele geciren saldirgan kurbanin sifirlama istegiyle YARISA girip
            // SON YAZAN olabilir; kurban "sifremi degistirdim" der, hesap saldirgandadir.
            // Ustelik "jeton TEK KULLANIMLIK" sozlesmesi tam da onemsedigi anda cokuyordu.
            //
            // COZUM K4 ile AYNI AILE: kosul VERITABANINA birakilir. Jetonun gecerliligi ve
            // TUKETILMESI ve yeni sifrenin yazilmasi TEK ifadededir; etkilenen satir 1
            // degilse jeton baska bir istek tarafindan ZATEN harcanmistir.
            //
            // YUKARIDAKI OKUMA KALDIRILMADI: hata mesajlarini ayirt etmek (GECERSIZ jeton mu,
            // SURESI DOLMUS jeton mu) ve `customer.id`yi ogrenmek icin gerekli. Okuma artik
            // KARAR VERMIYOR, yalnizca TESHIS uretiyor - karar asagidaki tek ifadede.
            //
            // DIKKAT (CLAUDE.md tuzagi): `ExecuteUpdateAsync` change-tracker'i ATLAR. Elimizdeki
            // TRACKED `customer` nesnesi bu noktadan sonra BAYAT - uzerinden tam-varlik
            // `UpdateAsync` CAGRILMAZ; cagrilsaydi jetonu ve eski sifreyi GERI YAZARDI.
            HashingHelper.CreatePasswordHash(dto.new_password, out var hash, out var salt);
            var tuketildi = await _customerDal.TryConsumeResetTokenAsync(
                jetonOzeti, DateTime.Now, hash, salt);
            if (tuketildi != 1)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PasswordResetInvalid));

            // Açıklayıcı yorum: Şifre değişince mevcut tüm oturumları geçersiz kıl (çalınan token'ı öldür)
            // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
            var kapatilanOturum = await _userSessionDal.InvalidateAllForCustomerAsync(customer.id);

            // ══ GF-1b / F3 - SIFIRLAMA DA BIR SIFRE DEGISIMIDIR ═══════════════════════════
            //
            // OLCULEN ONCE-DURUM: toplu access-token iptali uretimde IKI yerden cagriliyordu
            // (AccountManager change-password ve AuthManager logout-all); SIFIRLAMA yolundan
            // CAGRILMIYORDU. Oysa "sifremi unuttum" TAM DA hesabin ele gecirildigi supheli
            // durumda kullanilan yoldur: ustteki satir REFRESH tarafini kapatiyor, ama
            // saldirganin elindeki ACCESS token 15 dakikaya kadar CALISMAYA DEVAM ediyordu.
            await _tokenRevocation.RevokeAllBeforeNowAsync(
                (int)Divisima.Core.Utilities.Enums.UserTypeEnum.Customer, customer.id,
                TimeSpan.FromMinutes(AccessTokenOmruDk));

            // ══ GF-1b / F2 - CAS YOLUNDA DENETIM IZI ELLE YAZILIR ═════════════════════════
            // Gerekce `DenetimKaydiYazAsync`in basinda.
            await DenetimKaydiYazAsync(nameof(Customer), customer.id, "password_reset",
                $"sifre sifirlama jetonuyla degistirildi; kapatilan oturum: {kapatilanOturum}");
            await _securityEvents.LogAsync("ResetPassword", "Warning", customer.id, null, null,
                $"Sifre sifirlama jetonuyla degistirildi - tum oturumlar kapatildi "
                + $"({kapatilanOturum}) ve access token'lar iptal edildi");

            return (HttpStatusCode.OK, new SuccessResult(Messages.PasswordResetSuccess));
        }


        // Açıklayıcı yorum: Çıkış - refresh token verildiyse o oturumu, verilmediyse tüm oturumları kapat.
        // Böylece çalınan/eski refresh token bir daha kullanılamaz (JWT revocation - oturum tarafı).
        public async Task<(HttpStatusCode, Result)> Logout(int customerId, string? refreshToken,
            string? jti = null, DateTime? jtiExpiresAt = null)
        {
            // ══ GF-1 / K2 (C-1) - ACCESS TOKEN'I DA IPTAL ET ═══════════════════════════════
            //
            // OLCULEN ONCE-DURUM: `RevokeAsync` uretimde SIFIR yerden cagriliyordu. Cikis
            // OTURUMU (refresh tarafini) kapatiyordu ama ELDEKI ACCESS TOKEN 15 dakikaya kadar
            // CALISMAYA DEVAM EDIYORDU - yani "cikis yaptim" diyen kullanicinin calinmis jetonu
            // hala gecerliydi. Okuma tarafi (`TokenBlacklistMiddleware`) ZATEN canliydi; eksik
            // olan YALNIZ yazma tarafiydi.
            //
            // SINIR (durust kayit): bu, SUNULAN jetonu iptal eder. Kullanicinin BASKA
            // cihazlardaki access token'lari jti'leri saklanmadigi icin iptal EDILEMEZ; onlar
            // en fazla 15 dk daha yasar. Tam coklu-cihaz iptali `tokens_valid_from` benzeri bir
            // KOLON ister ve bu dalganin TEK migration'i K3'e ayrildi.
            await AccessTokenIptalEtAsync(jti, jtiExpiresAt);

            if (!string.IsNullOrEmpty(refreshToken))
            {
                // ══ GF-3 / K10 - LOGOUT'TA CHECK-THEN-ACT KALDIRILDI ═══════════════════════
                //
                // ONCEKI HAL: satir OKUNUYOR, bellekte `is_active=false` yapiliyor ve TAM-VARLIK
                // `UpdateAsync` ile yaziliyordu. Uc kusuru vardi:
                //  (1) OKU-SONRA-YAZ arasinda baska bir istek ayni satiri dondurebilir; iki yol
                //      da "kapattim" der ve rotasyonun CAS invariant'i BU YOLDAN delinir.
                //  (2) TAM-VARLIK yazma TUM kolonlari basar - `ExecuteUpdateAsync` ile atomik
                //      guncellenmis bir kolon bu yoldan SESSIZCE GERI ALINABILIR
                //      (CLAUDE.md bolum 5'te kayitli tuzak).
                //  (3) Kuralin IKINCI KOPYASIYDI: rotasyon CAS kullanirken logout kendi
                //      kopyasini tasiyordu.
                // Artik rotasyonla AYNI yardimci: `DeactivateIfActiveAsync` (tek atomik CAS).
                //
                // `GetByRefreshTokenAnyStateAsync` KULLANILIYOR: `GetByRefreshTokenAsync`
                // `is_active` FILTRELI oldugu icin bayat/rotasyonlanmis bir cerezle gelen cikis
                // istegi satiri HIC BULAMIYOR ve sessizce 200 donuyordu. Filtresiz okuma +
                // CAS ile davranis ayni (zaten kapali satir icin CAS 0 doner) ama YARIS YOK.
                // NOT: "bayat cerezle cikis 200 doner" gozlemi SUPHELI olarak raporlandi -
                // semantik karar merkezin, bu dalgada DAVRANIS DEGISTIRILMEDI.
                var session = await _userSessionDal.GetByRefreshTokenAnyStateAsync(refreshToken);
                if (session != null && session.customer_id == customerId)
                {
                    // ══ GF-5 / K2 - CIKIS ARTIK IZ BIRAKIYOR (AV-2 / S-C: TAM BOSLUK) ═══════
                    //
                    // OLCULEN ONCE-DURUM: `is_active` 1 -> 0 oluyordu ama defter deltasi 0 idi;
                    // yani "bu oturum ne zaman, kim tarafindan kapatildi" sorusunun yaniti
                    // HICBIR YERDE yoktu. Bir saldirgan kurbani cikarabilir ve bu gorunmezdi.
                    //
                    // CAS SONUCU DETAY'A YAZILIR: `DeactivateIfActiveAsync` atomik CAS'tir ve
                    // ETKILENEN SATIR SAYISINI doner. 0 donmesi "oturum ZATEN kapaliydi"
                    // demektir (bayat cerezle gelen cikis - SUPHELI olarak kayitli davranis).
                    // Sayiyi yazmak o iki durumu defterde AYIRT EDILEBILIR kilar.
                    var kapanan = await _userSessionDal.DeactivateIfActiveAsync(session.id);
                    await _securityEvents.LogAsync("Logout", "Info", customerId, null, null,
                        $"Tek oturum kapatıldı (etkilenen satır: {kapanan})");
                }
            }
            else
            {
                // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
                // GF-5 / K2: DONUS DEGERI ARTIK KULLANILIYOR. Onceden atiliyordu (AV-2 on
                // olcumunde S-A8 olarak isaretlenmisti); ayni cagri `:636`da zaten bir
                // degiskene aliniyordu, yani tutarsizlikti. Deger cikis olayinin detayina
                // giriyor - "tum cihazlardan cik" kac oturumu kapatti sorusu artik defterde.
                var kapatilanOturum = await _userSessionDal.InvalidateAllForCustomerAsync(customerId);

                // ══ GF-1b / K1 - "TUM CIHAZLARDAN CIK" ═══════════════════════════════════
                //
                // Bu dal (refresh token VERILMEDI) TUM oturumlari kapatmayi AMACLIYOR, ama
                // ustteki satir yalniz REFRESH tarafini dusuruyordu; diger cihazlarin
                // ACCESS token'lari 15 dakikaya kadar CALISMAYA DEVAM ediyordu.
                // Esik yazimi o boslugu kapatir.
                //
                // TEK OTURUM CIKISI (refresh token VERILEN dal) BILINCLI OLARAK KAPSAM DISI:
                // orada kullanici yalniz O cihazdan cikmak istiyor - statuko (merkez karari).
                await _tokenRevocation.RevokeAllBeforeNowAsync(
                    (int)UserTypeEnum.Customer, customerId, TimeSpan.FromMinutes(AccessTokenOmruDk));

                // GF-5 / K2: "tum cihazlardan cik" dalinin izi. Esik yaziminin (access iptali)
                // ARDINDAN yazilir - olay, isin TAMAMLANDIGINI bildirsin diye.
                await _securityEvents.LogAsync("Logout", "Info", customerId, null, null,
                    $"Tüm oturumlar kapatıldı (etkilenen satır: {kapatilanOturum})");
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.LogoutSuccess));
        }

        // GF-1 / K2: sunulan access token'in `jti`sini kara listeye yazar. `jti` yoksa (ornegin
        // servis HTTP disindan cagrildiysa) SESSIZCE gecer - iptal edilecek bir jeton YOKTUR.
        // TTL jetonun KENDI bitisinden turer; okunamazsa access token omru kadar varsayilan
        // kullanilir (kayit jetondan uzun yasamaz).
        private async Task AccessTokenIptalEtAsync(string? jti, DateTime? jtiExpiresAt)
        {
            if (string.IsNullOrEmpty(jti)) return;
            var bitis = jtiExpiresAt ?? DateTime.UtcNow.Add(VarsayilanIptalTtl);
            await _tokenBlacklist.RevokeAsync(jti, bitis);
        }

        // ═══ FIX-1A / F1 - `DeleteAccount` GOVDESI BURADAN KALDIRILDI ══════════════════════════
        // Burada `AccountManager.DeleteAccount`in IKINCI BIR KOPYASI duruyordu ve ayrisiyordu:
        // adres defterine HIC DOKUNMUYORDU (`IAddressDal` bu sinifa enjekte bile edilmemis),
        // e-postayi farkli bir kalipla anonimlestiriyor ve parola alanina rastgele bir ozet
        // yaziyordu. FAZ 1'de OLCULDU: bu uctan silinen hesabin adresi `full_name`/`phone`/
        // `full_address` DOLU ve `is_active=TRUE` kaliyordu - ustelik `frontend/api-client.js`
        // TAM DA bu ucu (`/api/auth/account`) cagiriyordu.
        //
        // Cozum ikinci kopyayi DUZELTMEK degil KALDIRMAK oldu (bu depoda ayni sinif hata
        // defalarca isirdi). Uc `AuthController` uzerinden `IAccountService.DeleteAccount`e
        // delege ediyor; ROTA DEGISMEDI. `IAuthService.DeleteAccount` de kaldirildi - derleme,
        // baska cagri yeri OLMADIGININ kanitidir (Sprint 8 madde 11 kalibi).

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
