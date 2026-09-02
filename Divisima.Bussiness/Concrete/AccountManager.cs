using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Security;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Account;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Hesap yönetimi iş kuralları. Profil/şifre/tercih/silme.
    //
    // ═══ FIX-1A / F1 - HESAP SILMENIN TEK UYGULAMASI ═══════════════════════════════════════
    // FAZ 1'de OLCULDU: iki ayri silme ucu vardi ve DAVRANISLARI AYRISIYORDU -
    // `DELETE /api/Account/delete` (bu sinif) adres defterini anonimlestiriyor ama SecurityEvent
    // yazmiyordu; `DELETE /api/auth/account` (AuthManager) SecurityEvent yaziyor ama adres
    // defterine HIC DOKUNMUYORDU (kanit: silme sonrasi adres satiri `full_name`/`phone`/
    // `full_address` DOLU ve `is_active=TRUE`). H27'nin "KVKK/GDPR EKSIK SILME DUZELTMESI"
    // ikizlerden YALNIZ BIRINE uygulanmisti; ustelik `frontend/api-client.js` TAM DA
    // duzeltilmemis ucu cagiriyordu.
    //
    // KOK DERS: ayni kuralin IKINCI KOPYASI. Bu depoda ayni sinif hata defalarca isirdi
    // (B10 onay yan etkileri, D5 rate limit kovalari, K7 yol->kova eslesmesi, Faz 0/K1 olu
    // onek). Bu yuzden cozum "eksik kopyayi da duzeltmek" DEGIL, KOPYAYI KALDIRMAK oldu:
    // `AuthManager.DeleteAccount` govdesi SILINDI, `AuthController` bu sinifa delege ediyor.
    // Rota DEGISMEDI - frontend'in cagirdigi `/api/auth/account` calismaya DEVAM EDIYOR.
    //
    // BU SINIF SECILDI cunku: (a) dogru adres kaskadi ZATEN buradaydi, (b) iki mevcut pin
    // (AuthorizationIdorTests) bu ucun davranisini sabitliyor, (c) `IAddressDal` zaten enjekte.
    // Eksik olan uc parca (SecurityEvent / cihaz / city-district-zip) buraya TASINDI.
    public class AccountManager : IAccountService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IUserSessionDal _userSessionDal;
        private readonly IAddressDal _addressDal;
        // K2: KVKK silmesinde abonelik temizligi icin (tablolarda customer_id YOK, kopru e-posta).
        private readonly IStockNotificationRequestDal _stockNotificationDal;
        private readonly IPriceDropSubscriptionDal _priceDropDal;
        private readonly ICustomerDeviceDal _deviceDal;
        private readonly IAuditLogDal _auditLogDal;
        private readonly ISecurityEventService _securityEvents;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        // GF-1 / K2: sifre degisiminde SUNULAN access token'i iptal etmek icin.
        private readonly Divisima.Core.Security.JWT.ITokenBlacklist _tokenBlacklist;
        // GF-1b / K1: TUM cihazlardaki access token'lari tek yazimla dusurmek icin.
        private readonly Divisima.Core.Security.JWT.IUserTokenRevocation _tokenRevocation;

        // Access token omru - iptal kaydinin TTL'i bundan turer. `TokenOptions`tan okumak
        // bu yardimciyi yapilandirmaya baglardi; deger `appsettings.json:8` ile AYNI ve
        // sapma olursa kayit yalnizca DAHA KISA yasar (guvenli taraf).
        private const int AccessTokenOmruDk = 15;

        public AccountManager(ICustomerDal customerDal, IUserSessionDal userSessionDal, IAddressDal addressDal,
            IStockNotificationRequestDal stockNotificationDal, IPriceDropSubscriptionDal priceDropDal,
            ICustomerDeviceDal deviceDal, IAuditLogDal auditLogDal, ISecurityEventService securityEvents,
            IUnitOfWork unitOfWork, ICacheService cache,
            Divisima.Core.Security.JWT.ITokenBlacklist tokenBlacklist,
            Divisima.Core.Security.JWT.IUserTokenRevocation tokenRevocation)
        {
            _customerDal = customerDal;
            _userSessionDal = userSessionDal;
            _addressDal = addressDal;
            _stockNotificationDal = stockNotificationDal;
            _priceDropDal = priceDropDal;
            _deviceDal = deviceDal;
            _auditLogDal = auditLogDal;
            _securityEvents = securityEvents;
            _unitOfWork = unitOfWork;
            _cache = cache;
            _tokenBlacklist = tokenBlacklist;
            _tokenRevocation = tokenRevocation;
        }

        public async Task<(HttpStatusCode, Result)> GetSummary(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            var dto = new AccountSummaryDto
            {
                id = c.id,
                name = c.name,
                email = c.email,
                phone = c.phone,
                birthdate = c.birthdate,
                email_verified = c.email_verified,
                two_factor_enabled = c.two_factor_enabled,
                loyalty_points = c.loyalty_points,
                store_credit = c.store_credit,
                referral_code = c.referral_code,
                notify_email = c.notify_email,
                notify_sms = c.notify_sms,
                notify_push = c.notify_push
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

        public async Task<(HttpStatusCode, Result)> ChangePassword(int customerId, ChangePasswordRequestDto dto,
            string? jti = null, System.DateTime? jtiExpiresAt = null)
        {
            // A2-FIX (SUPHELI #21): eski kural YALNIZCA ">= 6 karakter" idi - kayit ucunun
            // istedigi karmasikliktan (buyuk/kucuk/rakam) HABERSIZDI. Ayni hesabin sifresini
            // belirleyen iki yolun farkli guc istemesi savunulabilir degil; kural artik TEK
            // MERKEZDEN (Divisima.Core.Security.SifrePolitikasi) geliyor.
            // BU BIR SIKILASTIRMADIR ve bilinclidir: 6 -> 8 + karmasiklik.
            var sifreHatasi = SifrePolitikasi.Dogrula(dto.new_password);
            if (sifreHatasi != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(sifreHatasi));

            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            // ══ GF-1b / K2 (GF1-B2) - MEVCUT SIFRE ARTIK LOGIN KILIDINE TABI ═══════════════
            //
            // OLCULEN ONCE-DURUM: bu yolda hesap kilidi HIC calismiyordu - `IncrementFailedLogin`
            // cagrisi 0'di. Ayni sirri (kullanicinin sifresi) dogrulayan `/api/auth/login`
            // 5-yanlista-15dk kilidi tasirken bu uc SINIRSIZ deneme kabul ediyordu.
            // Kilit AYNI mekanizmadir - yeni bir sayac/kural KOPYASI ACILMADI (`ICustomerDal`
            // uzerindeki atomik metotlar dogrudan cagriliyor).
            //
            // KILIT KONTROLU DOGRULAMADAN ONCE: aksi halde kilitli hesapta her istek yine bir
            // TAM PBKDF2 kosturur ve kilit CPU-DoS'u ENGELLEMEZDI.
            // YANIT KODU LOGIN ILE AYNI: login "dogru sifre + kilitli -> 403 AccountLocked"
            // diyor. Burada cagiran ZATEN KIMLIK DOGRULAMIS durumda, yani hesabin var oldugunu
            // BILIYOR - 403 bir SIZINTI DEGIL, kullanicinin bilmesi gereken sey.
            var kilitli = c.lockout_end.HasValue && c.lockout_end.Value > DateTime.Now;
            if (kilitli)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.AccountLocked));

            // Açıklayıcı yorum: Mevcut şifre doğrulaması (yetkisiz değişim engeli)
            if (!HashingHelper.VerifyPasswordHash(dto.current_password ?? "", c.password_hash, c.password_salt))
            {
                // ATOMIK artis (login ile AYNI metot): paralel denemeler artisi KAYBETMEZ.
                // Bu dal `UpdateAsync`ten ONCE DONUYOR, yani CLAUDE.md bolum 5'teki
                // "ExecuteUpdateAsync + tam-varlik UpdateAsync" cakismasi BURADA OLUSMAZ.
                var deneme = await _customerDal.IncrementFailedLoginAsync(customerId);
                var simdiKilitlendi = deneme >= 5;
                if (simdiKilitlendi)
                    await _customerDal.LockAccountAsync(customerId, DateTime.Now.AddMinutes(15));

                // Guvenlik olayi: bu yolda ONCEDEN HIC olay yazilmiyordu (olculdu).
                await _securityEvents.LogAsync(simdiKilitlendi ? "AccountLocked" : "ChangePasswordFailed",
                    simdiKilitlendi ? "Critical" : "Warning", customerId, null, null,
                    simdiKilitlendi
                        ? "Sifre degistirmede 5 basarisiz mevcut-sifre denemesi - hesap kilitlendi"
                        : "Sifre degistirmede hatali mevcut sifre");

                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CurrentPasswordWrong));
            }

            HashingHelper.CreatePasswordHash(dto.new_password, out var hash, out var salt);
            c.password_hash = hash;
            c.password_salt = salt;
            c.updated_at = DateTime.Now;
            await _customerDal.UpdateAsync(c);

            // Açıklayıcı yorum: Şifre değişince diğer oturumları geçersiz kıl (çalınan token'ı öldür)
            // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
            await _userSessionDal.InvalidateAllForCustomerAsync(customerId);

            // ══ GF-1 / K2 (C-1) - ACCESS TOKEN'I DA IPTAL ET ═══════════════════════════════
            //
            // Ustteki satir REFRESH tarafini kapatiyordu; ACCESS token'a DOKUNMUYORDU. Yani
            // "sifremi degistirdim" diyen kullanicinin calinmis access token'i 15 dakikaya
            // kadar CALISMAYA DEVAM EDIYORDU - `RevokeAsync` uretimde SIFIR yerden cagriliyordu.
            //
            // SINIR (durust kayit): yalnizca SUNULAN jeton iptal edilir. Sifre degisimi
            // MANTIKEN tum cihazlari dusurmelidir, ama diger cihazlarin `jti`leri hicbir yerde
            // SAKLANMIYOR; tam kapanis `tokens_valid_from` benzeri bir KOLON ister ve bu
            // dalganin TEK migration'i K3'e ayrildi. Kalan maruziyet: diger cihazlarda en
            // fazla 15 dk.
            if (!string.IsNullOrEmpty(jti))
                await _tokenBlacklist.RevokeAsync(jti, jtiExpiresAt ?? DateTime.UtcNow.AddMinutes(15));

            // ══ GF-1b / K1 - ARTIK TUM CIHAZLAR DUSUYOR ═══════════════════════════════════
            //
            // GF-1'in BILINEN SINIRI buydu ve OLCULMUSTU: sifre degisiminden sonra cihaz1
            // 401 alirken IKINCI CIHAZ 200 almaya devam ediyordu (ustteki `jti` iptali
            // yalniz SUNULAN jetonu oldurur). Esik yazimi o boslugu kapatir: bu andan ONCE
            // uretilmis TUM access token'lar reddedilir.
            // Ustteki `InvalidateAllForCustomerAsync` REFRESH tarafini zaten kapatiyordu;
            // eksik olan ACCESS tarafiydi.
            await _tokenRevocation.RevokeAllBeforeNowAsync(
                (int)Divisima.Core.Utilities.Enums.UserTypeEnum.Customer, customerId,
                TimeSpan.FromMinutes(AccessTokenOmruDk));

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

        // KVKK/GDPR unutulma hakki - TEK UYGULAMA (bkz. sinif basindaki F1 notu).
        // `/api/Account/delete` VE `/api/auth/account` uclarinin IKISI DE buraya iner.
        //
        // TAMAMI TEK TRANSACTION ICINDE: musteri anonimlestirme -> adres defteri -> cihaz baglari
        // -> oturum iptali -> DENETIM IZI REDAKSIYONU -> guvenlik olayi. Herhangi bir adim
        // duserse HICBIRI kalici olmaz. Gerekce: KVKK yolunda "yarim silinmis" bir hesap
        // (musteri anonim, adresler PII dolu) EN KOTU sonuctur - bu depoda bir kez yasandi.
        public async Task<(HttpStatusCode, Result)> DeleteAccount(int customerId)
        {
            var c = await _customerDal.GetAsync(x => x.id == customerId);
            if (c == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.LoginFailed));

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // MANTIK-FIX-3 / K2: ABONELIK KAYITLARI DA SILINIR [KVKK].
                // OLCULEN YAPISAL BOSLUK: stock_notification_requests ve price_drop_subscriptions
                // tablolarinda `customer_id` KOLONU YOK (16 kolon olculdu; FK'lar yalniz products'a),
                // bu yuzden silme yolu onlari bugune kadar YAPISAL OLARAK bulamiyordu. Tek kopru
                // E-POSTADIR ve o dogrudan asagida anonimlestiriliyor.
                //
                // *** SIRA KRITIK: BU BLOK c.email ANONIMLESTIRILMEDEN ONCE KOSAR. ***
                // Sonra kossaydi `deleted_<id>@divisima.invalid` arar, HICBIR SATIR bulamaz ve
                // HATA DA VERMEZDI - sessiz no-op. (Iki bagimsiz on olcum ajani ayni uyariyi verdi.)
                //
                // DUZ ESITLIK KULLANILIR, KANONIKLESTIRME (PostaKutusu.Kanonik) KULLANILMAZ:
                // kanonik eksende `a+etiket@x` ile `a@x` AYNI kutuya duser ve BASKA MUSTERILERIN
                // aboneligi silinirdi. Bu veritabaninda CANLI ornek var (uc musteri tek kanonik
                // kutuyu paylasiyor). Saklanan e-postalar zaten kanonik oldugu icin (B1
                // normalizasyonu; BIN2 collation ile olculdu, sapma 0) duz esitlik YETERLIDIR.
                //
                // SILME > anonimlestirme: `email` NOT NULL ve (product_id[,size],email) uzerinde
                // filtreli UNIQUE indeks var; anonimlestirme Guid'li yer tutucu + jeton yenileme +
                // is_notified isaretlemesi yani UC yazma isterdi. Bu tablolarda HasQueryFilter YOK,
                // dolayisiyla K1'deki pasif-adres tuzaginin ikizi burada DOGMAZ.
                var silinecekEposta = c.email;
                await _stockNotificationDal.DeleteWhereAsync(s => s.email == silinecekEposta);
                await _priceDropDal.DeleteWhereAsync(s => s.email == silinecekEposta);

                // Açıklayıcı yorum: GDPR silme hakkı - KİŞİSEL VERİ ANONİMLEŞTİRME + pasifleştirme.
                // Hard-delete yerine anonimleştirme: sipariş/fatura geçmişi bütünlüğü (FK) korunur, PII silinir.
                c.name = "Silinmiş Kullanıcı";
                c.email = $"deleted_{c.id}@divisima.invalid";
                // `customers.phone` NULLABLE'dir (Customer.phone -> string?), bu yuzden NULL yazilir.
                // NOT: burada eskiden "kolon NOT NULL, anonim yer tutucu yaziliyor" diyen bir yorum
                // vardi; KODLA CELISIYORDU (kod zaten null yaziyor) ve FAZ 1'de bayat oldugu olculdu.
                c.phone = null;
                c.address = null;
                c.city = null;
                c.birthdate = null;
                c.gender = null;
                c.referral_code = null;
                // TEK BICIM (F1): silinen hesabin parola alani BOS DIZI olur.
                // AuthManager ikizi buraya RASTGELE bir ozet yaziyordu; ikilik bitti.
                // Guvenli: `HashingHelper.VerifyPasswordHash` `CryptographicOperations.FixedTimeEquals`
                // kullaniyor ve uzunluk farkinda GUVENLE false donuyor (olculdu) - bos ozet HICBIR
                // parolayla dogrulanamaz. Rastgele ozet ise DB'de ve denetim izinde gecerli bir
                // kimlik bilgisinden AYIRT EDILEMEZ; bos dizi "kimlik bilgisi YOK" der.
                c.password_hash = Array.Empty<byte>();
                c.password_salt = Array.Empty<byte>();
                c.email_verification_token = null;
                c.password_reset_token = null;
                c.two_factor_secret = null;
                c.two_factor_code = null;
                c.two_factor_enabled = false;
                c.is_active = false;
                c.notify_email = false;
                c.notify_sms = false;
                c.notify_push = false;
                c.updated_at = DateTime.Now;
                await _customerDal.UpdateAsync(c);

                // KVKK/GDPR EKSİK SİLME DÜZELTMESİ: kayıtlı ADRES DEFTERİ de PII içerir.
                // Müşteri kaydı anonimleştirilse bile adresler kalırsa erişim hakkı ihlali sürer.
                // F11: `city` / `district` / `zip_code` DE konum verisidir ve eskiden GERIDE KALIYORDU
                // (olculdu: silinmis hesabin adresinde "Istanbul" / "Kadikoy" duruyordu).
                // MANTIK-FIX-3 / K1: SORGU FILTRESI BU YOLDA DELINIR.
                // OLCULEN KUSUR: Address uzerinde global HasQueryFilter(is_active) var
                // (DivisimaDbContext.cs:825) ve duz GetListAsync o filtreyi DELMIYOR
                // (EfEntityRepositoryBase.cs:45) -> PASIF (soft-delete edilmis) adresler
                // kaskada HIC GIRMIYORDU. Kod DOGRU GORUNUYOR, pin YESIL, PII KALIYORDU.
                // CANLI KANIT (R-H1 once): silinen bir hesabin pasif adresinde ad, telefon,
                // acik adres, sehir, ilce ve posta kodu OKUNABILIR halde duruyordu.
                // GUVENLIK SINIRI: IgnoreQueryFilters YALNIZ bu KVKK silme yolunda -
                // filtrenin GENEL davranisi AYNEN korunur (AdminCustomerManager.cs:47/80 kalibi).
                // Yeni DAL yuzeyi ACILMADI: GetListIgnoringFiltersAsync ZATEN arayuzde
                // (IEntityRepository.cs:45). NoTracking doner; UpdateAsync detached varligi
                // Update() ile ekleyip tum kolonlari modified isaretledigi icin yazma CALISIR.
                var addresses = await _addressDal.GetListIgnoringFiltersAsync(a => a.customer_id == customerId);
                foreach (var a in addresses)
                {
                    a.full_name = "Silinmiş";
                    a.phone = null;
                    a.full_address = "-";
                    a.title = "-";
                    a.city = "-";
                    a.district = "-";
                    a.zip_code = null;
                    a.is_active = false;
                    a.updated_at = DateTime.Now;
                    await _addressDal.UpdateAsync(a);
                }

                // F10: CIHAZ BAGI. `device_token` KALICI BIR CIHAZ TANIMLAYICISIDIR (push kimlik
                // bilgisi) - `is_active=false` yetmez, deger DURDUKCA silinen hesap bir cihazla
                // eslestirilebilir kalir. Eskiden iki silme ucu da customer_devices'a HIC DOKUNMUYOR
                // ve satirlar `is_active=TRUE` kaliyordu (olculdu).
                // SILMEK YERINE DEGERI YOK EDIYORUZ: satir, denetim/gecmis icin korunur; token
                // tahmin edilemez bir yer tutucuyla degistirilir. Yer tutucu Guid tasir cunku
                // `IX_customer_devices_device_token` FILTRESIZ UNIQUE'tir - sabit bir yer tutucu
                // ikinci silmede cakisir ve silme ucunu 500'e dusururdu.
                var devices = await _deviceDal.GetListAsync(d => d.customer_id == customerId);
                foreach (var d in devices)
                {
                    d.device_token = $"deleted-{Guid.NewGuid():N}";
                    d.is_active = false;
                    d.last_used_at = DateTime.Now;
                    await _deviceDal.UpdateAsync(d);
                }

                // Açıklayıcı yorum: Tüm oturumları kapat
                // Tum aktif oturumlari TEK atomik sorgu ile kapat (foreach N+1 yerine - DRY + performans)
                await _userSessionDal.InvalidateAllForCustomerAsync(customerId);

                // F3: DENETIM IZI REDAKSIYONU - EN SONA BIRAKILDI, SIRA BILINCLIDIR.
                // Yukaridaki her `UpdateAsync` AuditInterceptor uzerinden YENI bir audit satiri
                // uretir ve o satirin `old` degerleri TAM DA SILINEN PII'yi tasir. Redaksiyon once
                // kosulsaydi silme isleminin KENDI izi redakte edilmemis kalirdi - FAZ 1'de olculen
                // zararin ta kendisi. Sona alindigi icin kendi urettigi satirlari da kapsar.
                // Ekseni ENTITY'dir (musteriye ait varliklar); ticari kayit (Order/Invoice/Payment)
                // KAPSAM DISI - olculdu ki PII tasimiyorlar ve yasal saklama altindalar.
                await DenetimIziniRedakteEtAsync(customerId, addresses.Select(a => a.id), devices.Select(d => d.id));

                // F12: guvenlik defterine iz. Eskiden YALNIZ AuthManager ikizi yaziyordu; yani
                // frontend'in cagirmadigi yol iz birakiyor, UI'in hedefleyecegi yol BIRAKMIYORDU.
                await _securityEvents.LogAsync("AccountDeleted", "Warning", customerId, null, null,
                    "Kullanıcı hesabını sildi (KVKK anonimleştirme + denetim izi redaksiyonu)");
                return true;
            });

            // Hesap durumu cache'ini düşür - silinen hesabın token'ı bir sonraki istekte
            // TokenBlacklistMiddleware tarafından reddedilsin (TTL beklenmesin).
            // TRANSACTION'IN DISINDA: cache geri alinabilir bir kaynak degildir; rollback olan bir
            // silmede anahtari dusurmus olmak yalnizca bir DB okumasina mal olur, tersi (commit
            // olmus silmede anahtarin ayakta kalmasi) silinen hesabin 60 sn daha erisimi demektir.
            _cache.Remove(CacheKeys.CustomerActive(customerId));

            return (HttpStatusCode.OK, new SuccessResult(Messages.AccountDeleted));
        }

        // F3'un uygulamasi. SATIR SILINMEZ: id / action / entity_id / created_at / user_id ve ALAN
        // ADLARI korunur; yalnizca kisisel/sir DEGERLER `DenetimGizlilik.Isaret` ile degistirilir.
        // Boylece "su tarihte su alan degisti" izi ayakta kalir, "neydi/ne oldu" gider.
        //
        // KENDI KENDINI BESLEMEZ: `AuditInterceptor` `AuditLog` tipini ACIKCA disliyor
        // (`_ignored` + `entry.Entity is AuditLog`), yani bu guncellemeler YENI audit satiri
        // URETMEZ. Aksi halde redaksiyonun kendisi sonsuz bir PII'li satir kaynagi olurdu.
        private async Task DenetimIziniRedakteEtAsync(int customerId, IEnumerable<int> adresIdleri, IEnumerable<int> cihazIdleri)
        {
            var adres = adresIdleri.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
            var cihaz = cihazIdleri.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
            var musteri = customerId.ToString(CultureInfo.InvariantCulture);

            // Oturum id'leri anonimlestirmeden ETKILENMEZ (`user_sessions.customer_id` korunur),
            // bu yuzden burada okunmasi guvenli.
            var oturumlar = await _userSessionDal.GetListNoTrackingAsync(s => s.customer_id == customerId);
            var oturum = oturumlar.Select(s => s.id.ToString(CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);

            // Tablo listesi SORGUYA yerel bir dizi olarak verilir: `IReadOnlyCollection.Contains`
            // EF ifade agacinda guvenilir sekilde `IN`e cevrilmez ve `StringComparer.Ordinal`
            // semantigi SQL collation'iyla ortusmez (CLAUDE.md bolum 6c'nin SQL tarafi kurali).
            var tablolar = DenetimGizlilik.RedaksiyonTablolari.ToArray();
            var kayitlar = await _auditLogDal.GetListAsync(a =>
                tablolar.Contains(a.table_name) && a.changes != null);

            foreach (var k in kayitlar)
            {
                var bizeAit = k.table_name switch
                {
                    "Customer" => k.entity_id == musteri,
                    "Address" => adres.Contains(k.entity_id),
                    "UserSession" => oturum.Contains(k.entity_id),
                    "CustomerDevice" => cihaz.Contains(k.entity_id),
                    _ => false
                };
                if (!bizeAit) continue;

                var yeni = DenetimRedaksiyonu.Redakte(k.changes);
                if (yeni == k.changes) continue;
                k.changes = yeni;
                await _auditLogDal.UpdateAsync(k);
            }
        }
    }
}
