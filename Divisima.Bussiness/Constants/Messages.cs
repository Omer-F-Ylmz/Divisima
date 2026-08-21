namespace Divisima.Core.Utilities.Constants
{
    // Açıklayıcı yorum: İş mesajları sabitleri (Cafixo Messages kalıbı). Servislerde Messages.X olarak kullanılır.
    // NOT: Bu dosya Divisima modülleri için üretildi; gerçek Messages.cs'inle birleştirilebilir.
    public static class Messages
    {
        // ── Ürün ──
        public static string ManualPaymentOnlyBankTransfer = "Manuel onay yalnizca havale/EFT siparislerinde gecerli.";
        public static string OrderAlreadyProcessed = "Siparis zaten islenmis.";
        public static string ManualPaymentConfirmed = "Havale/EFT odemesi onaylandi, siparis hazirlaniyor.";
        public static string CouponInvalidValue = "Gecersiz kupon degeri (yuzde 0-100, tutarlar negatif olamaz).";
        public static string ProductInvalidSalePrice = "Indirimli fiyat normal fiyattan dusuk ve pozitif olmalidir.";
        public static string ProductInvalidPrice = "Fiyat pozitif olmalidir.";
        public static string WishlistMovedToCart = "Urun sepete tasindi.";
        public static string QuestionTooShort = "Soru en az 5 karakter olmalidir.";
        public static string QuestionAsked = "Sorunuz alindi, yanitlaninca gorunur olacak.";
        public static string ReviewAlreadyExists = "Bu urune zaten yorum yaptiniz.";
        public static string OrderInvalidAddress = "Gecersiz teslimat adresi.";
        public static string OrderInvalidStatusTransition = "Gecersiz siparis durumu gecisi.";
        public static string CodLimitExceeded = "Kapida odeme limiti asildi. Bu tutar icin online odeme kullanin.";
        public static string ImportEmpty = "Ice-aktarilacak veri bos veya gecersiz (baslik + en az 1 satir gerekli).";
        public static string ProductAdded = "Ürün başarıyla eklendi.";
        public static string ProductUpdated = "Ürün başarıyla güncellendi.";
        public static string ProductDeleted = "Ürün başarıyla silindi.";
        public static string ProductListed = "Ürünler başarıyla listelendi.";
        public static string ProductNotFound = "Ürün bulunamadı.";
        public static string ProductAlreadyExists = "Bu isim ve markada bir ürün zaten mevcut.";
        public static string ProductStatusChanged = "Ürün durumu güncellendi.";

        // ── Kategori ──
        public static string CategoryAdded = "Kategori başarıyla eklendi.";
        public static string CategoryUpdated = "Kategori başarıyla güncellendi.";
        public static string CategoryDeleted = "Kategori başarıyla silindi.";
        public static string CategoryListed = "Kategoriler başarıyla listelendi.";
        public static string CategoryNotFound = "Kategori bulunamadı.";
        public static string CategoryAlreadyExists = "Bu slug ile bir kategori zaten mevcut.";
        public static string CategoryStatusChanged = "Kategori durumu güncellendi.";

        // ── Koleksiyon ──
        public static string CollectionAdded = "Koleksiyon başarıyla eklendi.";
        public static string CollectionUpdated = "Koleksiyon başarıyla güncellendi.";
        public static string CollectionDeleted = "Koleksiyon başarıyla silindi.";
        public static string CollectionListed = "Koleksiyonlar başarıyla listelendi.";
        public static string CollectionNotFound = "Koleksiyon bulunamadı.";
        public static string CollectionAlreadyExists = "Bu slug ile bir koleksiyon zaten mevcut.";
        public static string CollectionStatusChanged = "Koleksiyon durumu güncellendi.";
        public static string CollectionCuratorRequired = "Stil elçisi koleksiyonunda küratör adı zorunludur.";

        // ── Kupon ──
        public static string CouponAdded = "Kupon başarıyla eklendi.";
        public static string CouponUpdated = "Kupon başarıyla güncellendi.";
        public static string CouponDeleted = "Kupon başarıyla silindi.";
        public static string CouponListed = "Kuponlar başarıyla listelendi.";
        public static string CouponNotFound = "Kupon bulunamadı.";
        public static string CouponAlreadyExists = "Bu kod ile bir kupon zaten mevcut.";
        public static string CouponStatusChanged = "Kupon durumu güncellendi.";
        public static string CouponValid = "Kupon geçerli.";
        public static string CouponInvalid = "Geçersiz kupon kodu.";
        public static string CouponMinAmountNotMet = "Kupon için minimum sepet tutarına ulaşılmadı.";
        public static string CouponExpired = "Bu kuponun süresi dolmuş.";
        public static string CouponUsageLimitReached = "Bu kupon kullanım limitine ulaşmış.";

        // ── Sipariş ──
        public static string OrderPlaced = "Siparişiniz başarıyla oluşturuldu.";
        public static string OrderListed = "Siparişler başarıyla listelendi.";
        public static string OrderNotFound = "Sipariş bulunamadı.";
        public static string OrderEmptyCart = "Sepetiniz boş, sipariş oluşturulamaz.";
        public static string OrderStatusChanged = "Sipariş durumu güncellendi.";
        public static string OrderAlreadyPlaced = "Bu sipariş zaten oluşturulmuş.";
        public static string OrderFailed = "Sipariş oluşturulurken bir hata oluştu, işlem geri alındı.";

        // ── Stok ──
        public static string StockDecreased = "Stok başarıyla düşüldü.";
        public static string StockIncreased = "Stok başarıyla artırıldı.";
        public static string StockInsufficient = "Yetersiz stok. İstenen adet mevcut değil.";
        public static string StockNotFound = "Stok kaydı bulunamadı.";
        public static string StockConcurrencyConflict = "Stok güncelleme çakışması, lütfen tekrar deneyin.";

        // ── Kimlik doğrulama ──
        public static string RegisterSuccess = "Kaydınız başarıyla oluşturuldu.";
        public static string LoginSuccess = "Giriş başarılı.";
        public static string LoginFailed = "E-posta veya şifre hatalı.";
        public static string EmailAlreadyExists = "Bu e-posta adresi zaten kayıtlı.";
        public static string AccountInactive = "Hesabınız aktif değil.";
        public static string AccountLocked = "Çok fazla başarısız deneme. Hesabınız geçici olarak kilitlendi, 15 dakika sonra tekrar deneyin.";
        // ── Satıcı (marketplace) ──
        public static string SellerRegisterSuccess = "Satıcı başvurunuz alındı. Hesabınız admin onayından sonra satışa açılacaktır.";
        public static string SellerPendingApproval = "Hesabınız henüz onaylanmadı. Ürün ve satış işlemleri onay sonrası aktifleşir.";
        public static string SellerSuspended = "Satıcı hesabınız askıya alınmış. Lütfen destek ile iletişime geçin.";
        public static string SellerNotFound = "Satıcı bulunamadı.";
        public static string SellerDashboardListed = "Satıcı paneli verileri getirildi.";
        public static string RefreshTokenInvalid = "Geçersiz refresh token.";
        public static string RefreshTokenExpired = "Oturum süresi doldu, tekrar giriş yapın.";
        public static string TokenRefreshed = "Token yenilendi.";

        // ── Yorum ──
        public static string ReviewAdded = "Yorumunuz alındı, onay sonrası yayınlanacak.";
        public static string ReviewApproved = "Yorum onaylandı.";
        public static string ReviewRejected = "Yorum reddedildi.";
        public static string ReviewNotFound = "Yorum bulunamadı.";
        public static string ReviewListed = "Yorumlar listelendi.";
        public static string ReviewInvalidRating = "Puan 1 ile 5 arasında olmalıdır.";

        // ── İçerik ──
        public static string ContentListed = "İçerik listelendi.";
        public static string ContentNotFound = "İçerik bulunamadı.";
        public static string ContentUpdated = "İçerik güncellendi.";

        // ── Sepet ──
        public static string CartItemAdded = "Ürün sepete eklendi.";
        public static string CartItemRemoved = "Ürün sepetten çıkarıldı.";
        public static string CartItemNotFound = "Sepet kalemi bulunamadı.";
        public static string CartUpdated = "Sepet güncellendi.";
        public static string CartListed = "Sepet getirildi.";
        public static string CartInvalidQuantity = "Adet en az 1 olmalıdır.";
        public static string OrderInvalidQuantity = "Sipariş adedi 1-100 arasında olmalıdır.";
        public static string OrderInvalidSize = "Geçerli bir beden seçilmelidir.";

        // ── Yorum ──
        public static string ReviewStatusChanged = "Yorum durumu güncellendi.";

        // ── Auth ──

        // ── İçerik ──
        public static string ContentAdded = "İçerik başarıyla eklendi.";

        // ── Ödeme ──
        public static string PaymentInitiated = "Ödeme başlatıldı.";
        public static string PaymentInitFailed = "Ödeme başlatılamadı.";
        // SPRINT 8 MADDE 8: init hatasinin AYIRT EDILEBILIR dali. Musteriye "ne yapabilirsin"
        // sorusunun yaniti verilir; saglayicinin ham hata metni YANSITILMAZ.
        public static string PaymentBuyerEmailNotAccepted = "Ödeme sağlayıcısı hesabındaki e-posta adresini kabul etmiyor. Hesap Bilgilerim sayfasından gerçek bir e-posta adresi girip tekrar deneyebilirsin.";
        public static string PaymentSuccess = "Ödeme başarılı, siparişiniz onaylandı.";
        public static string PaymentFailed = "Ödeme başarısız, sipariş iptal edildi.";
        public static string PaymentNotFound = "Ödeme kaydı bulunamadı.";
        public static string PaymentAlreadyDone = "Bu siparişin ödemesi zaten yapılmış.";
        public static string PaymentAlreadyProcessed = "Bu ödeme zaten işlenmiş.";
        public static string PaymentProcessingError = "Ödeme işlenirken hata oluştu.";

        // ── Adres ──
        public static string AddressAdded = "Adres eklendi.";
        public static string AddressUpdated = "Adres güncellendi.";
        public static string AddressDeleted = "Adres silindi.";
        public static string AddressListed = "Adresler listelendi.";
        public static string AddressNotFound = "Adres bulunamadı.";

        // ── Sepet ──
        public static string CartCleared = "Sepet temizlendi.";
        public static string CartNotFound = "Sepet bulunamadı.";

        // ── Favoriler ──
        public static string WishlistAdded = "Favorilere eklendi.";
        public static string WishlistRemoved = "Favorilerden çıkarıldı.";
        public static string WishlistListed = "Favoriler listelendi.";

        // ── E-posta doğrulama ──
        public static string EmailVerified = "E-posta adresiniz doğrulandı.";
        public static string EmailAlreadyVerified = "E-posta zaten doğrulanmış.";
        public static string EmailVerificationInvalid = "Geçersiz doğrulama bağlantısı.";
        public static string EmailVerificationSent = "Doğrulama e-postası gönderildi.";
        public static string EmailNotVerified = "Giriş için e-posta adresinizi doğrulamanız gerekiyor. Gelen kutunuzu kontrol edin.";

        public static string SearchCompleted = "Arama tamamlandı.";

        public static string AccessDenied = "Bu işlem için yetkiniz yok.";

        public static string PaymentSignatureInvalid = "Ödeme imzası doğrulanamadı (geçersiz istek).";
        public static string PaymentAmountMismatch = "Ödenen tutar sipariş tutarıyla uyuşmuyor, işlem reddedildi.";
        public static string PaymentFraudReject = "Ödeme güvenlik kontrolünde reddedildi.";

        public static string PaymentTooManyAttempts = "Çok fazla ödeme denemesi. Lütfen birkaç dakika sonra tekrar deneyin.";

        public static string PaymentNotYourOrder = "Bu sipariş size ait değil.";
        public static string PaymentOrderNotPayable = "Bu sipariş ödeme için uygun durumda değil.";
        public static string PaymentInvalidAmount = "Geçersiz sipariş tutarı.";
        public static string PaymentPendingExists = "Bu sipariş için zaten bekleyen bir ödeme var.";
        public static string PaymentTokenExpired = "Ödeme oturumu zaman aşımına uğradı, lütfen tekrar başlatın.";
        public static string PaymentCurrencyMismatch = "Para birimi uyuşmuyor, ödeme reddedildi.";
        public static string PaymentProcessingBusy = "Bu sipariş için ödeme işleniyor, lütfen bekleyin.";
        public static string OrderProcessingConflict = "Sipariş işleniyor, lütfen birazdan tekrar deneyin.";

        // ── Şifre sıfırlama & çıkış ──
        public static string PasswordResetMailSent = "Eğer bu e-posta kayıtlıysa, sıfırlama bağlantısı gönderildi.";
        public static string PasswordResetInvalid = "Geçersiz sıfırlama bağlantısı.";
        public static string PasswordResetExpired = "Sıfırlama bağlantısının süresi doldu.";
        public static string PasswordResetSuccess = "Şifreniz güncellendi. Lütfen tekrar giriş yapın.";
        public static string LogoutSuccess = "Çıkış yapıldı.";

        public static string PaymentInvalidCallbackUrl = "Geçersiz callback adresi.";

        public static string AccountDeleted = "Hesabınız silindi. Kişisel verileriniz anonimleştirildi.";
        public static string DataExported = "Verileriniz dışa aktarıldı.";

        // ── İade/değişim ──
        public static string ReturnNotYourOrder = "Bu sipariş size ait değil.";
        public static string ReturnOrderNotDelivered = "Yalnızca teslim edilmiş siparişler iade edilebilir.";
        public static string ReturnWindowExpired = "İade süresi (14 gün) doldu.";
        public static string ReturnInvalidItem = "Geçersiz iade kalemi veya adet.";
        public static string ReturnAlreadyRequested = "Bu ürün için zaten bekleyen bir iade talebi var.";
        public static string ReturnCreated = "İade talebiniz alındı.";
        public static string ReturnNotFound = "İade talebi bulunamadı.";
        public static string ReturnAlreadyProcessed = "Bu iade talebi zaten işlenmiş.";
        public static string ReturnRejected = "İade talebi reddedildi.";
        public static string ReturnApproved = "İade onaylandı, ödeme iadesi yapıldı.";
        public static string ReturnRefundFailed = "Ödeme iadesi başarısız oldu.";
        public static string ReturnProcessingError = "İade işlenirken hata oluştu.";

        // ── Fatura ──
        public static string InvoiceCreated = "Fatura oluşturuldu.";
        public static string InvoiceNotFound = "Fatura bulunamadı.";
        public static string InvoiceNotYours = "Bu fatura size ait değil.";

        // ── Fatura ──
        public static string InvoiceAlreadyExists = "Bu sipariş için fatura zaten oluşturulmuş.";
        // SPRINT 8 MADDE 2: fatura yalniz onaylanmis (ve sonrasi) siparisler icin kesilir.
        // Pending -> para henuz alinmadi; Cancelled -> siparis yok hukmunde. Ikisine de fatura
        // kesmek mali bir yanlis beyandir (ciroyu sisirir, musteriye odenmemis borc gonderir).
        public static string InvoiceOrderNotBillable = "Bu siparişin durumu faturalamaya uygun değil (iptal edilmiş ya da ödemesi tamamlanmamış).";
        public static string InvoiceGenerated = "Fatura oluşturuldu.";

        // ── Fatura iptali (sipariş iptal edilince) ──
        public static string InvoiceCancelled = "Fatura iptal edildi.";
        public static string InvoiceAlreadyCancelled = "Fatura zaten iptal edilmiş.";
        // e-Fatura sağlayıcısı iptali reddetti -> fatura YEREL olarak da iptal işaretlenmez
        // (kayıtta iptal / GİB'de geçerli uyumsuzluğu oluşmasın).
        public static string InvoiceProviderCancelFailed = "Fatura e-Fatura sağlayıcısında iptal edilemedi; işlem geri alındı.";
        public static string InvoiceCancelNotNeeded = "İptal edilecek fatura bulunmuyor.";
        public static string InvoiceCancelOrderNotCancelled = "Sipariş iptal edilmediği için fatura iptal edilemez.";

        // ── Cihaz/push ──
        public static string DeviceTokenRequired = "Cihaz token gerekli.";
        public static string DeviceRegistered = "Cihaz kaydedildi.";
        public static string DeviceNotFound = "Cihaz bulunamadı.";
        public static string DeviceUnregistered = "Cihaz kaydı silindi.";

        // ── Kargo ──
        public static string ShipmentTrackingRequired = "Kargo takip numarası gerekli.";
        public static string ShipmentAlreadyExists = "Bu sipariş için zaten kargo kaydı var.";
        public static string ShipmentCreated = "Kargo oluşturuldu.";
        public static string ShipmentNotFound = "Kargo kaydı bulunamadı.";
        public static string ShipmentNotYours = "Bu kargo size ait değil.";

        // ── Stok rezervasyon ──
        public static string StockReserved = "Stok rezerve edildi.";
        public static string StockReservationConfirmed = "Rezervasyon onaylandı (stok düşüldü).";
        public static string StockReservationReleased = "Rezervasyon serbest bırakıldı.";

        public static string StockAdjusted = "Stok güncellendi.";
        public static string StockAdjustInvalid = "Geçersiz stok değeri.";
        public static string StockAdjustBelowReserved = "Stok, rezerve edilmiş miktarın altına indirilemez.";

        // ── Admin müşteri yönetimi ──
        public static string CustomerNotFound = "Müşteri bulunamadı.";
        public static string UserTypeUpdated = "Kullanıcı tipi güncellendi.";
        public static string InvalidUserType = "Geçersiz kullanıcı tipi (yalnızca Admin veya Müşteri).";
        public static string CannotDemoteLastAdmin = "Son admin müşteriye indirilemez (en az bir admin kalmalı).";
        public static string CustomerSuspended = "Müşteri askıya alındı.";
        public static string CustomerActivated = "Müşteri aktifleştirildi.";

        // ── Ürün görsel ──
        public static string ImageUploaded = "Görsel yüklendi.";
        public static string ImageTypeInvalid = "Yalnızca JPEG, PNG veya WEBP yüklenebilir.";
        public static string ImageTooLarge = "Görsel en fazla 5 MB olabilir.";
        public static string ImageEmpty = "Boş dosya yüklenemez.";
        public static string ImageNotFound = "Görsel bulunamadı.";
        public static string ImageDeleted = "Görsel silindi.";
        public static string ImagePrimarySet = "Birincil görsel belirlendi.";
        public static string OrderPlaceFailed = "Sipariş oluşturulamadı, lütfen tekrar deneyin.";
        public static string InvalidEmail = "Geçersiz e-posta adresi.";
        public static string StockNotificationSubscribed = "Stok geldiğinde size haber vereceğiz.";
        // SPRINT 8 MADDE 10 - abonelik yonetimi mesajlari.
        public static string StockNotificationNotFound = "Bildirim aboneliği bulunamadı.";
        public static string PriceDropNotFound = "Fiyat uyarısı aboneliği bulunamadı.";
        public static string NotificationUnsubscribed = "Bildirim aboneliğin kaldırıldı.";
        public static string StockNotificationAlreadySubscribed = "Bu ürün için zaten bildirim talebiniz var.";
        public static string RecentlyViewedRecorded = "Görüntüleme kaydedildi.";
        public static string ProfileNameRequired = "Ad boş olamaz.";
        public static string ProfileUpdated = "Profil güncellendi.";
        public static string PasswordTooShort = "Şifre en az 6 karakter olmalıdır.";
        public static string CurrentPasswordWrong = "Mevcut şifre hatalı.";
        public static string PasswordChanged = "Şifreniz değiştirildi.";
        public static string NotificationPreferencesUpdated = "Bildirim tercihleriniz güncellendi.";
        public static string CreditInvalidAmount = "Geçersiz tutar.";
        public static string CreditOperationFailed = "Kredi işlemi başarısız.";
        public static string CreditAdded = "Kredi eklendi.";
        public static string CreditInsufficient = "Yetersiz kredi bakiyesi.";
        public static string CreditUsed = "Kredi kullanıldı.";
        public static string LoyaltyNoPoints = "Kazanılacak puan yok.";
        public static string LoyaltyOperationFailed = "Puan işlemi başarısız.";
        public static string LoyaltyEarned = "Puan kazandınız.";
        public static string LoyaltyMinRedeem = "En az {0} puan gerekli.";
        public static string LoyaltyInsufficient = "Yetersiz puan bakiyesi.";
        public static string LoyaltyRedeemed = "Puan krediye çevrildi.";
        public static string GiftCardInvalidAmount = "Geçersiz tutar.";
        public static string GiftCardNotFound = "Hediye kartı bulunamadı.";
        public static string GiftCardCreated = "Hediye kartı oluşturuldu.";
        public static string GiftCardEmpty = "Hediye kartı bakiyesi tükenmiş.";
        public static string GiftCardRedeemFailed = "Hediye kartı bozdurma başarısız.";
        public static string GiftCardRedeemed = "Hediye kartı mağaza kredinize eklendi.";
        public static string PriceDropSubscribed = "Fiyat düşünce size haber vereceğiz.";
        public static string PriceDropAlreadySubscribed = "Bu ürün için zaten takiptesiniz.";
        public static string ReviewProfanity = "Yorumunuz uygunsuz ifade içeriyor.";
        public static string ReviewAlreadyVoted = "Bu yoruma zaten oy verdiniz.";
        public static string ReviewVoted = "Oyunuz kaydedildi.";
        public static string QuestionEmpty = "Soru boş olamaz.";
        public static string QuestionProfanity = "Sorunuz uygunsuz ifade içeriyor.";
        public static string QuestionSubmitted = "Sorunuz alındı, cevaplanınca yayınlanacak.";
        public static string QuestionNotFound = "Soru bulunamadı.";
        public static string QuestionAnswered = "Soru cevaplandı ve yayınlandı.";
        public static string AnswerEmpty = "Cevap boş olamaz.";
        public static string CouponFirstOrderOnly = "Bu kupon sadece ilk siparişte geçerlidir.";
        public static string CartSavedForLater = "Ürün favorilere taşındı.";
        public static string WishlistItemNotFound = "Favorilerde bulunamadı.";
        public static string CartMovedToCart = "Ürün sepete taşındı.";
        public static string OrderItemNotCancellable = "Bu sipariş durumunda kalem iptali yapılamaz.";
        public static string OrderItemNotFound = "Sipariş kalemi bulunamadı.";
        public static string OrderItemCancelFailed = "Kalem iptali başarısız.";
        public static string OrderItemCancelled = "Kalem iptal edildi, tutar mağaza kredinize eklendi.";
        public static string TwoFactorRequired = "Giris dogrulama kodu e-postaniza gonderildi.";
        public static string TwoFactorInvalid = "Gecersiz dogrulama kodu.";
        public static string TwoFactorExpired = "Dogrulama kodunun suresi doldu, tekrar giris yapin.";
        public static string InvalidVerificationToken = "Gecersiz dogrulama baglantisi.";
        public static string InvalidResetToken = "Gecersiz veya suresi dolmus sifirlama baglantisi.";
        public static string OrderAccessDenied = "Bu siparişe erişim yetkiniz yok.";
        public static string AttributesUpdated = "Ürün özellikleri güncellendi.";
        public static string SizeGuideInvalid = "Beden etiketi gerekli.";
        public static string SizeGuideUpdated = "Beden tablosu güncellendi.";
        public static string SizeGuideNotFound = "Bu kategori için beden tablosu yok.";
        public static string SizeGuideNoMeasurements = "Öneri için en az bir ölçü girin.";
        public static string SizeGuideRecommended = "Önerilen beden bulundu.";
        public static string CompareInvalidCount = "2-4 ürün karşılaştırabilirsiniz.";
        public static string CompareNotEnoughProducts = "Karşılaştırma için yeterli geçerli ürün yok.";
        public static string GuestEmailExists = "Bu e-posta kayıtlı. Lütfen giriş yapın.";
    }
}
