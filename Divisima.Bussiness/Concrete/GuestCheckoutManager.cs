using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Security.Hashing;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
using Divisima.Core.Utilities.Text;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Guest;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Misafir checkout iş kuralları. Misafir müşteri+adres oluşturur, sonra normal PlaceOrder'a devreder.
    // PlaceOrder kendi transaction'ını yönetir; misafir kayıtları öncesinde ayrı yazılır (nested transaction yok).
    public class GuestCheckoutManager : IGuestCheckoutService
    {
        private readonly ICustomerDal _customerDal;
        private readonly IAddressDal _addressDal;
        private readonly IOrderService _orderService;
        // A3: misafirin hesabini sonradan sahiplenebilmesi icin dogrulama maili tetiklenir.
        // YENI bir kod yolu ACILMADI - var olan ANONIM ucun ta kendisi cagriliyor.
        private readonly IAuthService _authService;
        private readonly ILogger<GuestCheckoutManager> _logger;
        // GUVENLIK-FIX-4: cop misafir siparisi guard'i icin - acik siparis sayimi ve esik.
        private readonly IOrderDal _orderDal;
        private readonly IConfiguration _configuration;

        public const string EsikAnahtari = "GuestCheckout:MaxOpenOrdersPerMailbox";
        public const int VarsayilanEsik = 3;

        // "ACIK" = iptal edilebilen, yani operatorun HALA ugrastigi durumlar. Elle yazilmaz,
        // DURUM MAKINESINDEN turetilir: `Cancelled`a hala gecebilen her durum aciktir.
        // `from == to` no-op oldugu icin `Cancelled` ACIKCA disarida birakilir (kendisine
        // gecis "gecerli" sayilir ama terminaldir). Sonuc: Pending, Confirmed, Preparing.
        public static readonly byte[] AcikDurumlar = Enum.GetValues(typeof(OrderStatusEnum))
            .Cast<OrderStatusEnum>()
            .Where(d => d != OrderStatusEnum.Cancelled
                        && OrderStatusMachine.IsValidTransition(d, OrderStatusEnum.Cancelled))
            .Select(d => (byte)d)
            .ToArray();

        public GuestCheckoutManager(ICustomerDal customerDal, IAddressDal addressDal, IOrderService orderService,
            IAuthService authService, ILogger<GuestCheckoutManager> logger, IOrderDal orderDal,
            IConfiguration configuration)
        {
            _customerDal = customerDal;
            _addressDal = addressDal;
            _orderService = orderService;
            _authService = authService;
            _logger = logger;
            _orderDal = orderDal;
            _configuration = configuration;
        }

        public async Task<(HttpStatusCode, Result)> PlaceGuestOrder(GuestCheckoutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.guest_email) || !dto.guest_email.Contains("@"))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.InvalidEmail));
            if (string.IsNullOrWhiteSpace(dto.guest_name))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ProfileNameRequired));
            if (dto.items == null || dto.items.Count == 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderEmptyCart));

            // A3 HIBRIT: misafir YALNIZ kapida odeme. Gerekce GuestCheckoutDto.payment_method'un
            // basinda. SESSIZCE COD'A DUSURME YOK - musteri kart sectiyse bunu ACIKCA ogrenmeli;
            // aksi halde "kartla odedim" sanip kapida nakit istenmesiyle karsilasirdi.
            const byte KapidaOdeme = 1;
            if (dto.payment_method != KapidaOdeme)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.GuestOnlyCashOnDelivery));

            var email = dto.guest_email.Trim().ToLowerInvariant();

            // Açıklayıcı yorum: E-posta zaten kayıtlıysa misafir checkout'a izin verme - giriş yapsın (hesap ele geçirme önleme)
            var existing = await _customerDal.GetAsync(c => c.email == email);
            if (existing != null)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.GuestEmailExists));

            // ══ GUVENLIK-FIX-4 - COP MISAFIR SIPARISI GUARD'I ═══════════════════════════════
            //
            // SIRA KRITIK: guard BURADA - musteri satiri / adres / dogrulama maili / siparis /
            // stok rezervasyonunun HICBIRI daha yazilmadi. Reddedilen istek HICBIR yan etki
            // birakmaz. Guard'i asagi almak, tam da engellemeye calistigi cop kaydi ONCE
            // yazip sonra reddetmek olurdu.
            //
            // 409 KONTROLUNDEN SONRA - BILINCLI: "bu e-posta kayitli" daha OZEL bir cevaptir
            // ve o dalin semantigi DEGISMEZ (kabul edilen risk, GUVENLIK DALGASI 2 / #1).
            //
            // ══ SPEC DUZELTMESI - OLCUMLE ═══════════════════════════════════════════════════
            // Ilk tasarim "Pending + odenmemis, SAKLANAN e-posta basina" idi. Ikisi de olcumle
            // curudu:
            //   (1) Misafir COD siparisi `Pending` DOGMUYOR - `Confirmed(1)` dogar
            //       (is_online_payment_done=0). Tum veritabaninda "Pending + odenmemis misafir
            //       COD siparisi" = 0 SATIR. Yani o yuklem HIC ATESLEMEZDI.
            //   (2) SAKLANAN e-posta basina acik siparis sayisi YAPISAL OLARAK en fazla 1'dir -
            //       ikinci siparis zaten yukaridaki 409'a takilir (olculdu: 5 e-postanin
            //       hepsinde n=1). Yani o gruplama anahtari da esigi HIC dolduramazdi.
            // GERCEK VEKTOR OLCULDU: `+etiket` varyanti 409'u ASIYOR ve AYNI fiziksel kutuya
            // yigiliyor (kurban@x -> 201, kurban+a@x -> 201), buyuk harf varyanti ise ZATEN
            // 409 aliyor (Dalga 1 kanoniklestirmesi tutuyor). Bu yuzden sayac ekseni KANONIK
            // POSTA KUTUSU'dur. Kanoniklestirme YALNIZ SAYACTA - hesap kimligi, musteri satiri
            // ve 409 semantigi DEGISMEZ (bkz. PostaKutusu dosyasinin basi).
            //
            // "ACIK" TANIMI DURUM MAKINESINDEN TURETILIR, ELLE YAZILMAZ: iptal edilebilen -
            // yani operatorun hala ugrastigi - her durum aciktir. `Shipped` DISARIDA cunku
            // yalniz `Delivered`a gidebilir (mal fiziksel olarak cikmistir, yeni siparisi
            // engellemek o maruziyeti geri almaz); `Delivered`/`Cancelled` terminaldir.
            // Makine degisirse bu kume KENDILIGINDEN degisir.
            var esik = _configuration.GetValue<int?>(EsikAnahtari) ?? VarsayilanEsik;
            if (esik < 1) esik = VarsayilanEsik;

            var kanonikKutu = PostaKutusu.Kanonik(email);
            var at = kanonikKutu.IndexOf('@');
            if (at > 0)
            {
                // SQL tarafi KABA SUZGEC (indeksten yararlanan sabit onek/sonek), kesin karar
                // C#'ta ORDINAL karsilastirmayla verilir - collation'a bagli yanlis pozitif
                // riski boylece sayacin DISINDA kalir.
                var yerelArti = kanonikKutu.Substring(0, at) + "+";
                var alanSonu = kanonikKutu.Substring(at);
                var adaylar = await _customerDal.GetListNoTrackingAsync(
                    c => c.email == kanonikKutu || (c.email.StartsWith(yerelArti) && c.email.EndsWith(alanSonu)));

                var kimlikler = adaylar
                    .Where(c => string.Equals(PostaKutusu.Kanonik(c.email), kanonikKutu, StringComparison.Ordinal))
                    .Select(c => c.id)
                    .ToList();

                if (kimlikler.Count > 0)
                {
                    var acikSiparisler = await _orderDal.GetListNoTrackingAsync(
                        o => kimlikler.Contains(o.customer_id)
                             && !o.is_online_payment_done
                             && AcikDurumlar.Contains(o.status));

                    if (acikSiparisler.Count >= esik)
                    {
                        _logger.LogWarning("MISAFIR SIPARIS GUARD'I: kanonik kutuda {Sayi} acik "
                            + "odenmemis siparis var (esik {Esik}) - yeni misafir siparisi reddedildi.",
                            acikSiparisler.Count, esik);
                        return (HttpStatusCode.TooManyRequests, new ErrorResult(Messages.GuestTooManyOpenOrders));
                    }
                }
            }

            // Açıklayıcı yorum: Misafir müşteri oluştur - rastgele güçlü şifre (müşteri bilmez; sonradan şifre-sıfırlama ile talep edebilir)
            var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            HashingHelper.CreatePasswordHash(randomPassword, out var hash, out var salt);
            var guest = new Customer
            {
                name = InputSanitizer.Sanitize(dto.guest_name.Trim()),  // stored XSS savunması
                user_type = 2,   // misafir de Customer
                email = email,
                phone = dto.guest_phone ?? "",
                password_hash = hash,
                password_salt = salt,
                is_active = true,
                email_verified = false,
                created_at = DateTime.Now,
                notify_email = true,
                notify_sms = false,
                notify_push = false
            };
            await _customerDal.AddAsync(guest);

            // Açıklayıcı yorum: Teslimat adresi oluştur
            var address = new Address
            {
                customer_id = guest.id,
                title = "Teslimat",
                full_name = InputSanitizer.Sanitize(dto.guest_name.Trim()),  // stored XSS savunması
                phone = dto.guest_phone ?? "",
                city = dto.city ?? "",
                district = dto.district ?? "",
                full_address = dto.full_address ?? "",
                zip_code = dto.zip_code,
                is_default = true,
                is_active = true,
                created_at = DateTime.Now
            };
            await _addressDal.AddAsync(address);

            // ══ A3 HIBRIT - MISAFIR HESABINI SAHIPLENEBILSIN DIYE DOGRULAMA MAILI ═══════════
            //
            // OLCULEN SORUN: misafir siparisini TAKIP EDEMIYORDU. Siparis onay mailindeki
            // takip baglantisi uye paneline gidiyor, misafirin oturumu yok; Login ise
            // email_verified sarti ariyor ve misafir DOGRULANMAMIS olarak yaziliyor.
            //
            // YENI UC ACILMADI (kullanici karari). Olculdu: bugun var olan ANONIM zincir bunu
            // zaten cozuyor -> resend-verification (var+dogrulanmamis dalinda YENI jeton uretir)
            // -> #/dogrula -> forgot-password -> sifre belirle -> my-orders. Eksik olan tek sey
            // MISAFIRE BUNUN SOYLENMESIYDI. Burada o zincirin ILK adimi tetikleniyor.
            // "Siparis no + e-posta ile sorgulama" ucu REDDEDILDI: yeni bir ANONIM sorgu yuzeyi
            // acar (enumeration + ayri rate-limit tasarimi gerektirir).
            //
            // BEST-EFFORT: mail tetiklenemezse SIPARIS DUSMEZ. Musterinin siparisi, hesabini
            // sahiplenme kolayligindan daha onemli - ustelik ayni maili kullanici Giris
            // ekranindan kendisi de isteyebilir. Sessiz de degil: hata loglanir.
            try { await _authService.ResendVerification(email); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MISAFIR DOGRULAMA MAILI TETIKLENEMEDI - siparis akisi "
                    + "etkilenmedi. customer_id={CustomerId}", guest.id);
            }

            // Açıklayıcı yorum: Normal sipariş akışına devret (stok/kupon/transaction hepsi PlaceOrder'da)
            var orderDto = new OrderCreateRequestDto
            {
                customer_id = guest.id,
                address_id = address.id,
                coupon_code = dto.coupon_code,
                request_id = dto.request_id,
                payment_method = dto.payment_method,   // A3: yalnizca COD gecebilir (yukarida dogrulandi)
                items = dto.items
            };
            // ══ MANTIK-FIX-3 / K4 - BASARISIZ SIPARISTE TELAFI SILME ═══════════════════════
            //
            // OLCULEN ZARAR (canli, uctan uca): gecersiz kupon -> PlaceOrder 400 doner, ama
            // musteri (:173) ve adres (:190) ZATEN YAZILMIS olur. Ayni misafir KUPONSUZ tekrar
            // dediginde bu kez 409 "Bu e-posta kayitli" alir - yani TEK BIR YANLIS KUPON KODU
            // o e-postayi misafir checkout'a KALICI KAPATIYOR ve musteri giris de yapamiyor
            // (parola rastgele uretildi, kendisi bilmiyor). Olculen ONCE-durum:
            //   1) gecersiz kupon -> 400, DB: musteri 1 / adres 1 / siparis 0
            //   2) ayni e-posta, kuponsuz -> 409
            //
            // NEDEN TRANSACTION DEGIL, TELAFI: yazimlari PlaceOrder ile AYNI transaction'a
            // almak IKI ayri sekilde ENGELLI (olculdu):
            //   (a) UnitOfWork._transaction TEK ALAN ve BeginTransactionAsync onu KOSULSUZ
            //       eziyor - ic ice transaction ACILAMAZ; PlaceOrder kendi transaction'ini
            //       yonetiyor (dosyanin basindaki notun soyledigi sey).
            //   (b) PlaceOrder hatalarinin cogu ISTISNA DEGIL DONUS DEGERI (11 nokta) -
            //       ExecuteInTransactionAsync yalniz ISTISNADA geri aliyor, dolayisiyla
            //       sarmalamak bile bu dali KURTARMAZDI.
            //
            // KAPSAM DAR: yalniz BU akisin BU cagrida yazdigi IKI satir siliniyor ve
            // ID'LER ELDE (guest / address nesneleri) - E-POSTAYLA ARAMA YAPILMIYOR
            // (yaris + yanlis hedef riski). Sira FK'ya saygili: ONCE adres, SONRA musteri.
            // 409 dali ve kupon dogrulama noktalari DEGISTIRILMEDI.
            //
            // ── BILINCLI SINIRLAR (rapora ve muhre girer) ──────────────────────────────
            //  1. TELAFI ATOMIK DEGIL. Telafi adimi kendisi duserse satir KALIR; o durumda
            //     musteriye PlaceOrder'in hatasi doner (telafi hatasi DEGIL) ve olay ADIYLA
            //     loglanir. Kalici kapanis GUVENLIK-AV-1 girdisidir.
            //  2. ISTISNA YOLU KAPSAM DISI. Merkez tarifi "donus-degerli hata dahil, throw
            //     beklenmez" diyor; PlaceOrder ISTISNA firlatirsa telafi KOSMAZ ve davranis
            //     K4 ONCESIYLE AYNI kalir (regresyon degil, kapatilmamis yol).
            //  3. DOGRULAMA MAILI YAN KAYDI (outbox mesaji) SILINMIYOR. Musteri satirindaki
            //     jeton musteriyle birlikte gidiyor ama outbox satirinin KIMLIGI ELDE DEGIL
            //     (AuthManager.ResendVerification yaziyor, geriye id dondurmuyor) ve onu
            //     e-postayla aramak "id'ler elde" kuralini delerdi. Sonuc: silinen bir hesap
            //     icin OLU JETONLU bir dogrulama maili gidebilir - kafa karistirici, ama
            //     e-postanin KALICI kilitlenmesinden kiyasla cok daha hafif.
            var (siparisDurum, siparisSonuc) = await _orderService.PlaceOrder(orderDto);
            if (siparisSonuc == null || !siparisSonuc.Success)
                await MisafirKayitlariniTelafiSilAsync(guest, address);
            return (siparisDurum, siparisSonuc);
        }

        // K4: yalniz PlaceOrder BASARISIZ dondugunde cagrilir. Nesneler cagiranin elinde -
        // arama yok. Sira FK'ya saygili (adres -> musteri). Telafi kendisi duserse GURULTULU
        // loglanir ve musteriye ASIL hata doner (bkz. yukaridaki BILINCLI SINIRLAR / 1).
        private async Task MisafirKayitlariniTelafiSilAsync(Customer guest, Address address)
        {
            try
            {
                await _addressDal.DeleteAsync(address);
                await _customerDal.DeleteAsync(guest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MISAFIR TELAFI SILME BASARISIZ - siparis olusmadi ama "
                    + "misafir kaydi KALDI; bu e-posta misafir checkout'ta 409 alacak. "
                    + "customer_id={CustomerId} address_id={AddressId}", guest.id, address.id);
            }
        }
    }
}
