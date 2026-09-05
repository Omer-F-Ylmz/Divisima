using System.Linq;
using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Integrations.Notifications;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Locking;
using Divisima.Core.Utilities.Notifications;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Shipping;
using Divisima.Core.Utilities.Validation;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Invoice;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;
using Divisima.Entity.Specifications;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Sipariş iş kuralları - sistemin kalbi. PlaceOrder frontend checkout akışının backend'i.
    // Cafixo tarzı: byte status/payment_type, nav property yok (kompozisyon çoklu DAL çağrısıyla).
    public class OrderManager : IOrderService
    {
        private const decimal CodMaxAmount = 5000m;   // kapida odeme ust limiti (dolandiricilik riski)
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IPaymentDal _paymentDal;
        private readonly IIyzicoClient _iyzico;
        private readonly IRefundService _refundService;
        private readonly ILoyaltyService _loyaltyService;
        private readonly IAddressDal _addressDal;
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IOrderSnapshotDal _orderSnapshotDal;
        private readonly IOrderSnapshotItemDal _orderSnapshotItemDal;
        private readonly IProductDal _productDal;
        private readonly ICustomerDal _customerDal;
        private readonly ICouponDal _couponDal;
        private readonly IStockService _stockService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInvoiceService _invoiceService;
        // K2: gorunur fatura KAYITTAN beslenir. Capraz Dal kullanimi mevcut manager desenidir
        // (yeni katman/abstraction ACILMADI).
        private readonly IInvoiceDal _invoiceDal;
        private readonly IInvoiceItemDal _invoiceItemDal;
        private readonly IOrderNotificationService _orderNotificationService;
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly IMapper _mapper;
        private readonly IDistributedLock _distributedLock;
        private readonly IOrderConfirmationService _orderConfirmation;

        // DALGA-2-FIX (B10): siparis onaylandiginda yan etki olayi OUTBOX'a yazilir.
        // Onceden bu manager onay yollarinda dogrudan `ApplyConfirmedSideEffectsAsync` cagiriyordu
        // ve o metot YALNIZ faturayi kesiyordu; sadakat / referans odulu / kupon defteri kart
        // yolundaki outbox mesajina bagliydi. Sonuc: kapida odeme, havale ve admin onayinda
        // musteri puan KAZANMIYOR, davet eden referans kredisini ALAMIYOR, kupon defteri BOS
        // kaliyordu (olculdu - Dalga 2 raporu B10).
        private readonly Divisima.Bussiness.Outbox.IOutboxService _outboxService;

        // Açıklayıcı yorum: Frontend sabitleri (FREE_SHIP=2000, kargo 49.9)
        private const decimal FreeShipThreshold = 2000m;
        // LAUNCH-FIX A1(b): IOrderPlacedEventPublisher bagimliligi KALDIRILDI. Olay artik outbox'a
        // yaziliyor ve publisher'i OutboxProcessor cagiriyor. Enjekte edilmis ama kullanilmayan bir
        // publisher birakmak, ileride birinin ayni satiri istek hattinda TEKRAR cagirmasina davetiye
        // olurdu - duzeltilen kusur tam olarak oydu. Derleyici de kaldirmanin guvenli oldugunu
        // dogruluyor (Sprint 8 madde 11'deki "build kanittir" kalibi).

        private const decimal ShippingCost = 49.9m;

        // GF-5 / K2 (D4): sahiplik ihlali izi. Kapsam gerekcesi ISecurityEventService'te.
        private readonly ISecurityEventService _securityEvents;

        // GF-6 / K1 (D1): `request_id` replay guard'i. Misafir yolu ve uye yolu AYNI servisi
        // FARKLI sahiplik ekseniyle cagirir - kural TEK YERDE (SiparisReplayGuardi.cs).
        private readonly ISiparisReplayGuardi _replayGuardi;

        public OrderManager(
            IOrderDal orderDal, IOrderItemDal orderItemDal,
            IOrderSnapshotDal orderSnapshotDal, IOrderSnapshotItemDal orderSnapshotItemDal,
            IProductDal productDal, ICustomerDal customerDal, ICouponDal couponDal,
            IStockService stockService, IUnitOfWork unitOfWork,
            IInvoiceService invoiceService, IInvoiceDal invoiceDal, IInvoiceItemDal invoiceItemDal,
            IOrderNotificationService orderNotificationService,
            IOrderStatusHistoryService statusHistory, IMapper mapper,
            IStoreCreditTransactionDal creditTxDal, IAddressDal addressDal,
            IPaymentDal paymentDal, IIyzicoClient iyzico, IRefundService refundService, ILoyaltyService loyaltyService,
            IDistributedLock distributedLock,
            IOrderConfirmationService orderConfirmation,
            Divisima.Bussiness.Outbox.IOutboxService outboxService,
            // GF-5 / K2 (D4): sahiplik ihlali IZ birakir. Yalniz YAZAR - 404 sozlesmesi,
            // mesajlar ve donus kodlari DEGISMEDI.
            ISecurityEventService securityEvents,
            // GF-6 / K1 (D1): `request_id` replay guard'i - misafir yoluyla ORTAK kaynak.
            ISiparisReplayGuardi replayGuardi)
        {
            _replayGuardi = replayGuardi;
            _securityEvents = securityEvents;
            _outboxService = outboxService;
            _creditTxDal = creditTxDal;
            _addressDal = addressDal;
            _paymentDal = paymentDal;
            _iyzico = iyzico;
            _refundService = refundService;
            _loyaltyService = loyaltyService;
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _orderSnapshotDal = orderSnapshotDal;
            _orderSnapshotItemDal = orderSnapshotItemDal;
            _productDal = productDal;
            _customerDal = customerDal;
            _couponDal = couponDal;
            _stockService = stockService;
            _invoiceService = invoiceService;
            _invoiceDal = invoiceDal;
            _invoiceItemDal = invoiceItemDal;
            _orderNotificationService = orderNotificationService;
            _statusHistory = statusHistory;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _distributedLock = distributedLock;
            _orderConfirmation = orderConfirmation;
        }

        // Açıklayıcı yorum: Sipariş oluştur. Adım adım - herhangi bir adımda hata olursa sipariş oluşmaz.
        public async Task<(HttpStatusCode, Result)> PlaceOrder(OrderCreateRequestDto dto)
        {
            // Açıklayıcı yorum: 1) Sepet boş olamaz
            if (dto.items == null || dto.items.Count == 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderEmptyCart));

            // ══ GF-6 / K1 (D1) - UYE REPLAY'I ARTIK SAHIPLIK SORUYOR ═══════════════════════════
            //
            // OLCULEN ONCE-DURUM (AV-3 / T1-B1, LAUNCH BLOKER): bu blok yalnizca
            // `o.request_id == dto.request_id` soruyordu ve eslesen siparisin `id` +
            // `order_number` alanlarini ISTEYEN KIM OLURSA OLSUN 200 ile doneriyordu.
            // `orders.request_id` tekil indeksi GLOBAL oldugu icin, BASKASININ anahtarini
            // gonderen bir uye o siparisin numarasini OGRENIYORDU. Misafir yolu ayni kapiyi
            // GF-1/K1'de KAZANMISTI (sahiplik = e-posta); uye yoluna TASINMAMISTI.
            //
            // KURAL KOPYALANMADI, ORTAK SERVISE TASINDI (`SiparisReplayGuardi`): "ayni kuralin
            // ikinci kopyasi" bu depoda YEDI kez bedeli odenmis bir ailedir. Buradaki eksen
            // `customer_id` - controller onu TOKEN'dan set eder.
            //
            // UC DAL (D1):
            //   baskasinin rid'i           -> 400 SIZINTISIZ (order_number YOK)
            //   ayni musteri + ayni sepet  -> 200 replayed:true (mevcut siparis)
            //   ayni musteri + baska sepet -> 400
            // MFIX-B / K3 KORUNDU: replay dali hala AYNI dar DTO'yu doner.
            var replaySonucu = await _replayGuardi.DegerlendirAsync(
                dto.request_id, ReplaySahiplik.MusteriIdIle(dto.customer_id), dto.items, dto.coupon_code);
            if (replaySonucu != null) return replaySonucu.Value;

            // Açıklayıcı yorum: ADRES SAHİPLİK KONTROLÜ (IDOR engeli) - address_id müşteriye AİT olmalı.
            // Aksi halde başkasının kayıtlı adresine sipariş verilebilir / adres bilgisi sızabilirdi.
            //
            // ══ GF-6 / K2 (D2) - ADRES ARTIK ZORUNLU ═══════════════════════════════════════
            // ESKI HAL `if (dto.address_id.HasValue)` IDI: adres GONDERILMEZSE bu blogun TAMAMI
            // atlaniyor ve siparis ADRESSIZ yaziliyordu (AV-3 / T1-B2). Kapi ONCE validator'da
            // (400 + `OrderAddressRequired`), BURADA da savunma amacli tekrar sorulur - bu
            // manager'i validator'dan GECMEYEN bir cagiran da (misafir akisi) kullaniyor.
            // Iki kapi AYNI mesaji verir; "ayni kuralin ikinci kopyasi" degil, AYNI SABIT.
            if (!dto.address_id.HasValue || dto.address_id.Value <= 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderAddressRequired));

            Address? siparisAdresi;
            {
                // GF-1 / K4 (B-1): SAHIPLIK IHLALI 404, 403 DEGIL - `SecureControllerBase`teki
                // tek sozlesme. Bu dal "yok" ile "senin degil"i ZATEN tek yanitta birlestiriyordu
                // (dogru desen), yalniz DURUM KODU sozlesmeye aykiriydi: 403, adresin VAR
                // oldugunu ima ediyordu. Mesaj DEGISMEDI - zaten varlik sizdirmiyor.
                var addr = await _addressDal.GetAsync(a => a.id == dto.address_id.Value);
                if (addr == null || addr.customer_id != dto.customer_id)
                {
                    // GF-5 / K2 (D4): IZ - 404 sozlesmesi ve mesaj DEGISMEDI.
                    // OLAY YALNIZ SAHIPLIK IHLALINDE YAZILIR, "adres YOK" dalinda DEGIL:
                    // ikisi ayni yaniti doner (bilincli, varlik sizdirmamak icin) ama ayni sey
                    // DEGILDIR - var olmayan bir id'yi yoklamak yazim hatasi olabilir, BASKASININ
                    // adresini yoklamak olamaz. Olayi ikisine birden yazmak, SIEM tarafinda
                    // gurultuyu sinyalden ayirt edilemez kilardi.
                    if (addr != null)
                        await _securityEvents.SahiplikIhlaliAsync("address", dto.address_id.Value, dto.customer_id);
                    return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderInvalidAddress));
                }
                // GF-6 / K2 (D2): snapshot'in `shipping_address` alani BUGUNE KADAR `null`
                // yaziliyordu. Adres nesnesi ZATEN ELDE - ikinci bir okuma yapilmiyor.
                siparisAdresi = addr;
            }

            // Açıklayıcı yorum: 2) Tüm kalemler için ürün + stok kontrolü (overselling engeli)
            decimal subtotal = 0m;
            var lineData = new List<(Product product, string size, int qty, decimal unitPrice)>();

            // N+1 ÖNLEME: tüm ürünleri TEK sorguda çek (her kalem için ayrı GetAsync yerine -> 20 kalem = 20 sorgu idi).
            // Sepet büyüdükçe checkout gecikmesi lineer artmasın diye toplu çekip dict'ten O(1) lookup.
            var productIds = dto.items.Select(i => i.product_id).Distinct().ToList();
            var productMap = (await _productDal.GetListAsync(p => productIds.Contains(p.id) && p.is_active))
                .ToDictionary(p => p.id);

            foreach (var item in dto.items)
            {
                // Açıklayıcı yorum: KALEM DOĞRULAMA - negatif/sıfır/aşırı miktar fiyat manipülasyonu (subtotal düşürme) +
                // stok baypası (negatif miktarda CheckStock her zaman geçer) + negatif rezervasyonla stok artışı önler.
                if (item.quantity < 1 || item.quantity > 100)
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidQuantity));
                if (string.IsNullOrWhiteSpace(item.size))
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidSize));

                if (!productMap.TryGetValue(item.product_id, out var product))
                    return (HttpStatusCode.NotFound, new ErrorResult(Messages.ProductNotFound));

                var hasStock = await _stockService.CheckStock(item.product_id, item.size, item.quantity);
                if (!hasStock)
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.StockInsufficient));

                // Açıklayıcı yorum: Flash sale aktifse indirimli fiyat kullan (fiyat tutarsızlığı önlenir)
                var effectivePrice = PricingHelper.EffectivePrice(product.price, product.sale_price, product.sale_start, product.sale_end, DateTime.Now);
                subtotal += effectivePrice * item.quantity;
                lineData.Add((product, item.size, item.quantity, effectivePrice));
            }

            // GÜVENLİK (kupon limit RACE engeli): per_user_limit/usage_limit CHECK-THEN-ACT'ti (COUNT sorgusu sonra
            // order insert); EŞZAMANLI aynı-kupon siparişleri her ikisi de "0 önceki" sayıp limiti AŞABİLİYORDU.
            // Limitli kupon varsa dağıtık kilitle serileştir -> aynı koda eşzamanlı siparişler sıraya girer, COUNT doğru olur.
            // using -> metot dönünce (commit sonrası) otomatik bırakılır. Limitsiz kupon/kuponsuz siparişte kilit YOK (contention yok).
            Coupon _couponForLock = !string.IsNullOrWhiteSpace(dto.coupon_code) ? await _couponDal.GetByCodeAsync(dto.coupon_code) : null;
            bool _needsCouponLock = _couponForLock != null && (_couponForLock.per_user_limit > 0 || _couponForLock.usage_limit > 0);
            using var _couponLock = _needsCouponLock
                ? await _distributedLock.AcquireAsync($"coupon:{_couponForLock.code}", TimeSpan.FromSeconds(10))
                : null;
            if (_needsCouponLock && _couponLock == null)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.OrderProcessingConflict));

            // Açıklayıcı yorum: 3) Kupon indirimi + kargo bedava bayrağı (frontend couponDiscount, byte discount_type)
            decimal discount = 0m;
            bool freeShipping = false;
            string coupon_code = null;

            if (!string.IsNullOrWhiteSpace(dto.coupon_code))
            {
                var coupon = await _couponDal.GetByCodeAsync(dto.coupon_code);
                // Açıklayıcı yorum: KUPON DOĞRULAMA - Validate ucundaki TÜM kuralları burada da uygula.
                // Kritik: doğrudan /api/order/place isteği Validate'i baypas edebilir; süre/limit/ilk-sipariş/tavan
                // burada kontrol edilmezse süresi dolmuş kupon geçer, tek-kullanımlık kupon sınırsız kullanılır.
                //
                // MFIX-B / K2: SESSIZ YOK SAYMA KALKTI. Onceden gecersiz kupon "UYGULANMAZ (indirim 0)"
                // ile yutuluyordu; olculdu (once-durum): var olmayan kodla place -> HTTP 201
                // {"data":224,...}, siparis 224 indirim 0.00 / coupon_code NULL - musteri odeme
                // ekraninda indirimli tutar gorup FARKLI tutar oduyor ve sebebi HICBIR YERDE yazmiyordu.
                // Artik RET SEBEBI tasinir ve asagida 400 + O kuralin mesajiyla donulur. Mesajlar
                // Validate ucuyle AYNI sabitlerden gelir (tek kaynak).
                //
                // YAN ETKI - KAPSAM ACIK YAZILIYOR (denetimde olculdu):
                //   PlaceOrder ICINDE yan etki YOK: bu blok transaction'dan (BeginTransactionAsync)
                //   ve stok rezervasyonundan (ReserveStock) ONCE kosar; reddedilen istek SIPARIS
                //   satiri ya da REZERVASYON birakmaz (canli olculdu: iki sayac da degismedi).
                //   AMA UC DUZEYINDE BIRAKABILIR: /api/guest-checkout/place misafir MUSTERI + ADRES
                //   satirini ve dogrulama e-postasi outbox mesajini PlaceOrder'a DEVRETMEDEN ONCE
                //   yazar (GuestCheckoutManager). Yani buradaki ret, o yolda yetim bir musteri
                //   birakir ve ayni e-posta ikinci denemede 409 alir. Bu sinif PRE-EXISTING'dir
                //   (stok yetersizliginde de olusur) ama K2 ulasilabilir ret sebeplerini artirdigi
                //   icin SIKLIGI artar. Kapsam disi - bkz. rapor/SUPHELI.
                //
                // SIRA: null -> expire -> min_amount. Uc kosul eskiden TEK bir && ifadesindeydi
                // (hepsi saglanmaliydi), yani ayrik siralari esdeger; Validate ucundaki oncelikle
                // (once yok, sonra suresi dolmus) AYNI olsun diye bu sira secildi.
                if (coupon == null)
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CouponInvalid));

                string kuponRet =
                    (coupon.expire_date.HasValue && coupon.expire_date.Value < DateTime.Now) ? Messages.CouponExpired
                    : subtotal < coupon.min_amount ? Messages.CouponMinAmountNotMet
                    : null;

                // Açıklayıcı yorum: İlk-sipariş kuponu - tamamlanmış (Pending/Cancelled dışı) siparişi olan müşteri kullanamaz
                if (kuponRet == null && coupon.first_order_only)
                {
                    // PERFORMANS (H51): EXISTS - satirlari cekmeden "hic tamamlanmis siparisi var mi" sorar.
                    var hasCompleted = await _orderDal.AnyAsync(o =>
                        o.customer_id == dto.customer_id && PaidOrderSpec.PaidStatuses.Contains(o.status));   // H52: merkezi kural
                    if (hasCompleted) kuponRet = Messages.CouponFirstOrderOnly;
                }

                // Açıklayıcı yorum: KULLANICI-BAŞI LİMİT - bu müşteri bu kuponu kaç kez kullandı (iptal olmayan siparişlerde).
                // Aksi halde tek-kullanımlık promo kuponu bir kullanıcı tarafından global limite kadar defalarca kullanılırdı.
                if (kuponRet == null && coupon.per_user_limit > 0)
                {
                    // KISI-BASI LIMIT FIX (H51): sayim yalniz "!= Cancelled" idi -> ODENMEMIS (Pending) siparis de
                    // musterinin kupon hakkini TUKETIYORDU. Odemesi yarida kalan/basarisiz olan musteri, per_user_limit=1
                    // ise kuponunu KALICI olarak kaybediyordu. Global limitte (H50) duzeltilen kuralin KARDES kopyasiydi.
                    // Ayni kural: odenmis siparisler + hala TAZE bekleyen odeme (devam eden checkout).
                    // GF-6 / F1: ESKI KURALDA KALDI - gerekce CouponManager'in global limit
                    // sitesinde (olculmus geri tepme: COD yolunda limit UYGULANAMAZ hale geliyordu).
                    // Onizleme ile enforcement AYNI ifadeyi kullanmaya devam eder (H52).
                    var userPendingGrace = DateTime.Now.AddMinutes(-PaidOrderSpec.PendingGraceMinutes);
                    var usedByUser = await _orderDal.CountAsync(o =>
                        o.customer_id == dto.customer_id && o.coupon_code == coupon.code &&
                        (PaidOrderSpec.PaidStatuses.Contains(o.status)
                         || (o.status == (byte)OrderStatusEnum.Pending && o.created_at >= userPendingGrace)));
                    if (usedByUser >= coupon.per_user_limit) kuponRet = Messages.CouponPerUserLimitReached;
                }

                // Açıklayıcı yorum: GLOBAL KULLANIM LİMİTİ - used_count yerine SİPARİŞ SAYISI ile denetlenir (per_user_limit gibi).
                // KRİTİK 2 hata: (1) used_count YALNIZCA kart-ödeme yolunda artıyordu -> store-credit/COD ile aynı kupon
                // global limiti aşacak şekilde SINIRSIZ kullanılabiliyordu (usage_limit baypası). (2) İptal edilince used_count
                // düşmüyordu -> iptal edilen siparişler limiti KALICI şişiriyor, kupon gerçek kullanım olmadan tükeniyordu.
                // İptal-olmayan siparişleri global sayarak: TÜM ödeme yöntemleri sayılır + iptaller otomatik düşülür (ikisi de çözülür).
                if (kuponRet == null && coupon.usage_limit > 0)
                {
                    // KAMPANYA SABOTAJI FIX (H50): sayim YALNIZ "!= Cancelled" idi -> ODENMEMIS (Pending)
                    // siparisler de limiti tuketiyordu. Saldirgan usage_limit kadar siparis acip HIC ODEMEZ ->
                    // kupon herkese KALICI olarak kapanir (kampanya sabotaji). Ayni dosyadaki kisi-basi kontrol
                    // zaten "!= Pending && != Cancelled" diyordu - tutarsizdi.
                    // Yeni kural: ODENMIS siparisler (PaidOrderSpec) + hala TAZE bekleyen odemeler (devam eden
                    // checkout'lar sayilmali ki limit asilmasin). Bayat Pending'ler artik limiti tutmaz.
                    var pendingGrace = DateTime.Now.AddMinutes(-PaidOrderSpec.PendingGraceMinutes);
                    // PERFORMANS (H51): COUNT(*) - populer kuponda 50.000 siparisi belleğe cekmek yerine tek sayi.
                    // GF-6 / F1: ESKI KURALDA KALDI - gerekce CouponManager'in global limit sitesinde.
                    var globalUses = await _orderDal.CountAsync(o =>
                        o.coupon_code == coupon.code &&
                        (PaidOrderSpec.PaidStatuses.Contains(o.status)
                         || (o.status == (byte)OrderStatusEnum.Pending && o.created_at >= pendingGrace)));
                    if (globalUses >= coupon.usage_limit) kuponRet = Messages.CouponUsageLimitReached;
                }

                // MFIX-B / K2: gecersiz kupon ARTIK SESSIZCE YOK SAYILMAZ - 400 + sebep.
                // TOCTOU KABULU (merkez karari): onizleme ile siparis arasinda kupon gecersizlesirse
                // checkout 400 ile kirilir. Alternatifi (sessizce kuponsuz devam) musteriye
                // BEKLEMEDIGI TUTARI odetiyordu; gorunur hata daha durust.
                if (kuponRet != null)
                    return (HttpStatusCode.BadRequest, new ErrorResult(kuponRet));

                coupon_code = coupon.code;
                switch ((DiscountTypeEnum)coupon.discount_type)
                {
                    case DiscountTypeEnum.Percentage:
                        discount = MoneyHelper.Percentage(subtotal, coupon.value);
                        // Açıklayıcı yorum: Yüzde indirim TAVANI (max_discount_amount) - yoksa büyük sepette sınırsız indirim
                        if (coupon.max_discount_amount.HasValue && discount > coupon.max_discount_amount.Value)
                            discount = coupon.max_discount_amount.Value;
                        break;
                    case DiscountTypeEnum.Fixed:
                        discount = Math.Min(coupon.value, subtotal);
                        break;
                    case DiscountTypeEnum.FreeShipping:
                        freeShipping = true;
                        break;
                }
            }

            // Açıklayıcı yorum: 4) Kargo (frontend shipCost)
            decimal shipping = (freeShipping || subtotal >= FreeShipThreshold) ? 0m : ShippingCost;
            decimal total = subtotal - discount + shipping;

            // Açıklayıcı yorum: CÜZDAN (mağaza kredisi) uygula - checkout'ta kullanılabilir. Clamp: istenen <= mevcut <= toplam.
            // Asıl düşüm transaction içinde ATOMIK yapılır (yarış güvenli); buradaki okuma yalnız clamp içindir.
            decimal creditRequested = dto.use_store_credit > 0 ? dto.use_store_credit : 0m;
            decimal creditToApply = 0m;
            if (creditRequested > 0)
            {
                var buyer = await _customerDal.GetAsync(c => c.id == dto.customer_id);
                creditToApply = Math.Min(Math.Min(creditRequested, buyer?.store_credit ?? 0m), total);
            }

            // Açıklayıcı yorum: KAPIDA ÖDEME (COD) - limit kontrolü. Kalan (cüzdan sonrası) tutar limiti aşarsa reddet.
            bool isCod = dto.payment_method == 1;
            bool isBankTransfer = dto.payment_method == 2;  // Havale/EFT - Pending kalir, admin manuel onaylar
            if (isCod && (total - creditToApply) > CodMaxAmount)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CodLimitExceeded));

            // Açıklayıcı yorum: 5) Sipariş kaydı (byte status = Pending(0), payment_type: COD=1 / Online=0)
            var order = new Order
            {
                customer_id = dto.customer_id,
                order_number = GenerateOrderNumber(),
                request_id = dto.request_id,
                status = (byte)OrderStatusEnum.Pending,
                subtotal = subtotal,
                discount_amount = discount,
                shipping_cost = shipping,
                total_price = total,
                store_credit_used = creditToApply,
                coupon_code = coupon_code,
                address_id = dto.address_id,
                payment_type = (byte)(isCod ? 1 : (isBankTransfer ? 2 : 0)),
                is_online_payment_done = false,
                created_at = DateTime.Now
            };
            // Açıklayıcı yorum: 5-7) Atomik transaction - order + kalemler + rezervasyon + snapshot hep birlikte.
            // AddAsync anında SaveChanges yaptığı için, kısmi siparişi önlemek üzere transaction ŞART.
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _orderDal.AddAsync(order);

                // Açıklayıcı yorum: 6) Kalemleri ekle + stok rezerve et
                foreach (var (product, size, qty, unitPrice) in lineData)
                {
                    await _orderItemDal.AddAsync(new OrderItem
                    {
                        order_id = order.id,
                        product_id = product.id,
                        size = size,
                        quantity = qty,
                        unit_price = unitPrice,
                        seller_id = product.seller_id, // marketplace: kalemi satıcıya bağla (satıcı satış/gelir sorgusu)
                        created_at = DateTime.Now
                    });

                    // Açıklayıcı yorum: Rezervasyon SONUCUNU kontrol et - yetmez/çakışırsa tüm siparişi geri al
                    var (reserveCode, reserveResult) = await _stockService.ReserveStock(product.id, size, qty, order.id);
                    if (reserveCode != HttpStatusCode.OK)
                    {
                        await _unitOfWork.RollbackAsync();
                        return (reserveCode, reserveResult);
                    }
                }

                // Açıklayıcı yorum: 7) Snapshot al (sipariş anını dondur)
                await CreateSnapshotAsync(order, lineData, siparisAdresi);

                // Açıklayıcı yorum: 7b) İlk durum kaydı - zaman çizelgesi başlangıcı (transaction içinde, atomik)
                await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Pending, "Sipariş oluşturuldu");

                // Açıklayıcı yorum: 7c) CÜZDAN DÜŞÜMÜ - ATOMIK (yarış güvenli; başka işlem krediyi harcamış olabilir).
                // Yetmezse tüm siparişi geri al (all-or-nothing).
                if (creditToApply > 0)
                {
                    var creditAffected = await _customerDal.TryDecrementStoreCreditAsync(dto.customer_id, creditToApply);
                    if (creditAffected == 0)
                    {
                        await _unitOfWork.RollbackAsync();
                        return (HttpStatusCode.Conflict, new ErrorResult(Messages.CreditInsufficient));
                    }
                    await _creditTxDal.AddAsync(new StoreCreditTransaction
                    {
                        customer_id = dto.customer_id,
                        amount = creditToApply,
                        type = (byte)LedgerEntryTypeEnum.Redeem,
                        reason = "Sipariş ödemesi (mağaza kredisi)",
                        order_id = order.id,
                        created_at = DateTime.Now
                    });
                }

                // Açıklayıcı yorum: 7d) CÜZDAN SİPARİŞİ TAM KAPATTIYSA online ödeme gerekmez - hemen onayla + rezervasyonu
                // gerçek stok düşümüne çevir (aksi halde 0 tutarlı Iyzico çağrısı yapılamaz).
                if (total - creditToApply <= 0)
                {
                    // GF-6 / K5: durum yazimi TEK KAPIDAN. Bu noktada siparis Pending DOGDU,
                    // yani gecis YAPISAL OLARAK gecerlidir; yine de sessiz gecistirme YOK -
                    // makine degisirse burasi GURULTULU duser, sessizce YANLIS YAZMAZ.
                    if (!DurumYaz(order, OrderStatusEnum.Confirmed))
                    {
                        await _unitOfWork.RollbackAsync();
                        return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderPlaceFailed));
                    }
                    order.is_online_payment_done = true;
                    await _orderDal.UpdateAsync(order);
                    await _stockService.ConfirmReservation(order.id);
                    await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Mağaza kredisi ile ödendi");
                }
                else if (isCod)
                {
                    // Açıklayıcı yorum: KAPIDA ÖDEME - online ödeme beklenmez; sipariş onaylanır, ödeme teslimatta alınır.
                    // Stok hemen satışa çevrilir (rezervasyon -> gerçek düşüm). is_online_payment_done=false kalır (nakit).
                    // GF-6 / K5: durum yazimi TEK KAPIDAN (gerekce DurumYaz'in basinda).
                    if (!DurumYaz(order, OrderStatusEnum.Confirmed))
                    {
                        await _unitOfWork.RollbackAsync();
                        return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderPlaceFailed));
                    }
                    await _orderDal.UpdateAsync(order);
                    await _stockService.ConfirmReservation(order.id);
                    await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Kapıda ödeme - sipariş onaylandı");
                }

                // DALGA-2-FIX (B10): iki dal da (magaza kredisiyle tam odeme + kapida odeme) siparisi
                // Confirmed yapiyor. Olay COMMIT'TEN ONCE, AYNI TRANSACTION ICINDE yaziliyor -
                // Sprint 8 madde 3'un kaliginin aynisi: "siparis onaylandi ama olay kaybedildi"
                // durumu OLUSAMAZ.
                if (order.status == (byte)OrderStatusEnum.Confirmed)
                    await SiparisOnaylandiOlayiYazAsync(order);

                // ══ LAUNCH-FIX A1(b) - SIPARIS OLAYI ARTIK OUTBOX'TAN GECIYOR ═══════════════
                //
                // OLCULEN ONCE-DURUM: _eventPublisher.PublishAsync(...) COMMIT'TEN SONRA ve
                // TRY BLOGUNUN DISINDA cagriliyordu. Publisher handler'lari duz bir
                // "foreach { await handler }" ile kosuyor, try/catch YOK. Handler'lardan biri
                // OrderPlacedEmailHandler ve o da SmtpMailService'i cagiriyor - bu servis
                // hatayi BILINCLI OLARAK FIRLATIR (yutmaz). Sonuc: gercek bir SMTP sunucusunda
                // ilk gecici hatada siparis COMMIT OLMUS oldugu halde uc HTTP 500 doner;
                // musteri "siparis olusmadi" sanip tekrar dener.
                //
                // COZUM MEVCUT ALTYAPI - YENI KANAL ICAT EDILMEDI: OutboxProcessor'da
                // case "OrderPlaced" ZATEN VARDI ve ayni publisher'i cagiriyordu; bugune kadar
                // o dala mesaj YAZAN kimse yoktu (olculdu: uretimde tek yazici yok, yalniz
                // ClaimBeforeSendTests kurgusu). Mesaj COMMIT'TEN ONCE, AYNI TRANSACTION'da
                // yaziliyor - "siparis var ama olay yok" durumu olusamaz.
                //
                // KAZANC: mail/SignalR hatasi siparis yanitini ETKILEMEZ; hata SESSIZ de kalmaz -
                // 5 kez yeniden denenir, tukenirse status=Failed + LogError + siparis zaman
                // cizelgesine KRITIK notu (OutboxProcessor.KaliciHataylaBirakAsync).
                //
                // BEDEL - DURUST KAYIT: teslimat artik AT-LEAST-ONCE ve tek bir mesaj UC handler'i
                // birden tasiyor. Son handler (SignalR bildirimi) patlarsa mesaj yeniden denenir
                // ve onay maili IKINCI KEZ gidebilir. Kabul edildi: alternatifi (handler basina
                // ayri mesaj) publisher sozlesmesini bolerdi ve bir siparis onay mailinin
                // tekrarlanmasi, hic gitmemesinden iyidir.
                await _outboxService.WriteAsync("OrderPlaced", new Divisima.Bussiness.Events.OrderPlacedEvent
                {
                    order_id = order.id,
                    customer_id = order.customer_id,
                    order_number = order.order_number,
                    total = order.total_price
                });

                await _unitOfWork.CommitAsync();

                // FATURA SENKRON KALIR - bu cagri KALDIRILMADI, gerekcesi OLCUMDUR.
                // Ilk denemede kaldirilmisti (fatura da outbox'a birakilmisti) ve IKI mevcut pin
                // bunu YAKALADI: AuthorizationIdorTests'in fatura testleri siparisin hemen
                // ardindan faturayi okuyor ve "Sequence contains no elements" ile kirildi.
                // Yani kart DISI yollarda fatura BUGUNE KADAR ANINDA kesiliyordu; onu ~1 dakikaya
                // yaymak ISTENMEYEN bir davranis degisikligi olurdu (B10'un kusuru fatura DEGIL,
                // eksik olan diger uc yan etkiydi).
                // CAKISMA YOK: isleyicinin 1. adimi ayni faturayi kesmeye calisinca
                // InvoiceManager'in "bu siparis icin fatura zaten var" kontrolu NO-OP dondurur
                // (olculdu ve pinlendi). YAN KAZANC: fatura artik kart disi yollarda da
                // YENIDEN DENENEBILIR - bu cagri patlarsa outbox onu tamamlar; onceden bu yolda
                // fatura best-effort'tu ve HIC yeniden denenmiyordu.
                if (order.status == (byte)OrderStatusEnum.Confirmed)
                    await _orderConfirmation.ApplyConfirmedSideEffectsAsync(order.id);
            }
            catch (Exception)
            {
                // Açıklayıcı yorum: Beklenmeyen hata - kısmi sipariş kalmasın (all-or-nothing)
                await _unitOfWork.RollbackAsync();
                // Concurrency: eşzamanlı AYNI request_id -> unique index ihlali fırlatmış olabilir. Kazanan siparişi
                // bul ve dön (graceful idempotency - race loser hata yerine mevcut siparişi alır, çift sipariş olmaz).
                if (!string.IsNullOrWhiteSpace(dto.request_id))
                {
                    // GF-1 / K1: YARISI KAYBEDEN DAL DA `replayed = true` doner. Eskiden bu
                    // dal `Success=TRUE` donduğu icin misafir akisinin telafisi ATESLEMIYOR ve
                    // kaybeden istegin YAZDIGI musteri+adres YETIM kaliyordu - on-kontrol
                    // bu dali KAPATMAZ (on-kontrol gecer, yaris SONRA kaybedilir), o yuzden
                    // bayrak BURADA da sart.
                    //
                    // GF-6 / K1 (D1): bu dal da ARTIK AYNI GUARD'DAN gecer - on-kontrolle IKI
                    // FARKLI kural olusmasin diye. Kazanan siparis BASKASININ ise 400 SIZINTISIZ
                    // doner; `order_number` yaris dalindan da SIZMAZ. Guard `null` donerse
                    // (kazanan bulunamadi) asagidaki 500'e dusulur - eski davranisla AYNI.
                    var yarisSonucu = await _replayGuardi.DegerlendirAsync(
                        dto.request_id, ReplaySahiplik.MusteriIdIle(dto.customer_id), dto.items, dto.coupon_code);
                    if (yarisSonucu != null) return yarisSonucu.Value;
                }
                return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderPlaceFailed));
            }

            // 8) Event publish: LAUNCH-FIX A1(b) ile BURADAN KALDIRILDI ve transaction'in ICINE,
            //    outbox'a tasindi (gerekce ve olculen zarar yukarida, yazim satirinin basinda).
            //    Buraya YENI bir cagri EKLENMEZ: bu noktada atilan her istisna, COMMIT OLMUS bir
            //    siparis icin 500 dondurur.
            return (HttpStatusCode.Created, new SuccessDataResult<OrderPlaceResponseDto>(
                new OrderPlaceResponseDto { id = order.id, order_number = order.order_number },
                Messages.OrderPlaced));
        }

        // Açıklayıcı yorum: Snapshot + snapshot kalemleri (Cafixo OrderSnapshot zinciri, iki timestamp)
        // ══ GF-6 / K2 (D2) - SNAPSHOT ARTIK ADRESI DE DONDURUYOR ═══════════════════════════
        //
        // OLCULEN ONCE-DURUM: `shipping_address` SABIT `null` yaziliyordu. Snapshot'in isi
        // "siparis anini dondurmak"tir; adres defterindeki satir sonradan DEGISTIRILEBILIR ya
        // da KVKK silmede anonimlestirilebilir (`Address.phone` icin bu ZATEN yapiliyor) -
        // yani siparisin GITTIGI adres, siparis anindan sonra HICBIR YERDE saklanmiyordu.
        //
        // KIRPMA URETIM NOKTASINDA: kolon `nvarchar(500)`; bilesenlerin toplam ust siniri
        // (ad 150 + telefon 20 + acik adres 500 + ilce 60 + sehir 60 + posta 20) bunu ASABILIR
        // ve EF insert-time HTTP 500 uretirdi - SD-7 ailesinin ta kendisi. Deger BURADA,
        // yazilmadan ONCE kirpilir; cagirana tasima YOK.
        private const int SnapshotAdresEnUzun = 500;

        private static string? SnapshotAdresMetni(Address? adres)
        {
            if (adres == null) return null;
            var parcalar = new[]
            {
                (adres.full_name ?? "").Trim(),
                (adres.phone ?? "").Trim(),
                (adres.full_address ?? "").Trim(),
                string.Join(" ", new[] { (adres.district ?? "").Trim(), (adres.city ?? "").Trim() }
                    .Where(p => p.Length > 0)),
                (adres.zip_code ?? "").Trim()
            }.Where(p => p.Length > 0);

            var metin = string.Join(" · ", parcalar);
            if (metin.Length == 0) return null;
            return metin.Length <= SnapshotAdresEnUzun ? metin : metin.Substring(0, SnapshotAdresEnUzun);
        }

        private async Task CreateSnapshotAsync(Order order,
            List<(Product product, string size, int qty, decimal unitPrice)> lineData,
            Address? siparisAdresi)
        {
            var customer = await _customerDal.GetAsync(c => c.id == order.customer_id);
            var snapshot = new OrderSnapshot
            {
                order_id = order.id,
                customer_id = order.customer_id,
                customer_full_name = customer != null ? customer.name : "",
                shipping_address = SnapshotAdresMetni(siparisAdresi),
                status = order.status,
                subtotal = order.subtotal,
                discount_amount = order.discount_amount,
                shipping_cost = order.shipping_cost,
                total_price = order.total_price,
                coupon_code = order.coupon_code,
                snapshot_created_at = DateTime.Now,
                order_created_at = order.created_at
            };
            await _orderSnapshotDal.AddAsync(snapshot);

            foreach (var (product, size, qty, unitPrice) in lineData)
            {
                await _orderSnapshotItemDal.AddAsync(new OrderSnapshotItem
                {
                    order_snapshot_id = snapshot.id,
                    product_id = product.id,
                    product_name = product.name,
                    brand = product.brand,
                    product_price = unitPrice,
                    size = size,
                    quantity = qty,
                    created_at = DateTime.Now
                });
            }
        }

        // ══ GF-6 / K5 (D5) - DURUM YAZIMININ TEK KAPISI ════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM (AV-3 / T4-F5): `OrderStatusMachine` VARDI ama YALNIZ BIR yol
        // (`ChangeOrderStatus`) ondan geciyordu. Diger BES yazim yeri durumu DOGRUDAN atiyor
        // ve gecerliligi KENDI ELLE YAZILMIS on kosuluyla soruyordu - yani makinenin ELLE
        // KOPYALARI olusmustu ("ayni kuralin ikinci kopyasi" ailesi). Ornekler:
        //   `ConfirmManualPayment` -> "status != Pending" (Pending->Confirmed kuralinin kopyasi)
        //   `CancelItem`           -> "status != Confirmed && != Preparing" (->Cancelled kopyasi)
        // Kopyalar makine degistiginde SESSIZCE ayrisir.
        //
        // ARTIK: durum YALNIZ buradan yazilir ve gecis GECERSIZSE YAZILMAZ (false doner).
        // `IsValidTransition` `from == to` icin true dondurur (idempotent no-op) - bu davranis
        // makinenin kendi sozlesmesidir ve DEGISTIRILMEDI.
        private static bool DurumYaz(Order order, OrderStatusEnum hedef)
        {
            if (!OrderStatusMachine.IsValidTransition((OrderStatusEnum)order.status, hedef))
                return false;
            order.status = (byte)hedef;
            return true;
        }

        private string GenerateOrderNumber()
        {
            return "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
        }



        // Açıklayıcı yorum: FATURA (HTML) - müşteri görüntüler/yazdırır (tarayıcı PDF'e çevirir; PDF lib gerekmez).
        // Sahiplik kontrollü (IDOR yok). KDV %20 dahil varsayımıyla matrah/KDV ayrıştırılır.
        // MANTIK-FIX-2R / K2 - GORUNUR FATURA ARTIK KAYITTAN BESLENIR.
        //
        // ONCEKI DAVRANIS (olculdu): bu metot faturayi `orders` + `order_items` uzerinden
        // YENIDEN HESAPLIYOR ve hazir HTML donduruyordu; `invoices` / `invoice_items`
        // tablolarina HIC DOKUNMUYORDU. Somut zararlar:
        //   - matrah sabit `total_price / 1.20m` ile ayristiriliyordu ve etiket sabit
        //     "KDV (%20)" idi -> karisik oranli sepette YANLIS ORAN BEYANI (canli: 12 fatura;
        //     ornek fatura 55 -> gercek agirlikli oran 0,1416 iken ekran %20 yaziyordu),
        //   - IPTAL EDILMIS siparis icin TAM GORUNUMLU fatura ciziliyordu (olculdu: siparis
        //     268 iptal + faturasi iptal, ekranda "iptal" gecisi 0, "Genel Toplam 549,70 TL"),
        //   - FATURASI OLMAYAN siparis icin de belge uretiliyordu (canli: 143 siparisin 47'si),
        //   - para SUNUCUDA bicimleniyordu (kultur sunucuya kilitleniyordu).
        //
        // ARTIK: kayit okunur, YAPILANDIRILMIS HAM DEGER donulur. Sunucu SAYI BICIMLEMEZ -
        // bicimleme istemcide dvsLocale ile yapilir, RequestLocalization ACILMAZ.
        // Sahiplik sozlesmesi AYNEN korunur (ihlal de "bulunamadi" doner - varlik sizdirilmaz).
        public async Task<(HttpStatusCode, Result)> GetInvoiceView(int orderId, int customerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            if (order.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));   // TEK SOZLESME: sahiplik ihlali de "bulunamadi" (varlik sizdirilmaz)

            var view = new InvoiceViewResponseDto
            {
                order_number = order.order_number,
                order_status = order.status,
                order_is_cancelled = order.status == (byte)OrderStatusEnum.Cancelled,
                // ODEME OZETI SIPARIS VERISINDEN (D2): fatura BRUTTUR, kredi bir ODEME ARACIDIR
                // ve `invoices` onu KAYDETMEZ. Bu yuzden kirilim tek dogru kaynagindan gelir.
                payment = new InvoiceViewPaymentDto
                {
                    order_total = order.total_price,
                    store_credit_used = order.store_credit_used,
                    remaining = order.total_price - order.store_credit_used
                }
            };

            var invoice = await _invoiceDal.GetAsync(i => i.order_id == order.id);
            if (invoice == null)
            {
                // BOS DURUM: belge UYDURULMAZ. Eski uc burada da fatura ciziyordu.
                view.has_invoice = false;
                return (HttpStatusCode.OK, new SuccessDataResult<InvoiceViewResponseDto>(view));
            }

            view.has_invoice = true;
            view.invoice_number = invoice.invoice_number;
            view.invoice_created_at = invoice.created_at;
            view.invoice_status = invoice.status;
            view.invoice_is_cancelled = invoice.status == (byte)InvoiceStatusEnum.Cancelled;
            view.subtotal = invoice.subtotal;
            view.tax_amount = invoice.tax_amount;
            view.total = invoice.total;

            var lines = await _invoiceItemDal.GetListAsync(x => x.invoice_id == invoice.id);
            foreach (var l in lines.OrderBy(x => x.id))
            {
                var kargoMu = l.product_id == null;
                view.items.Add(new InvoiceViewLineDto
                {
                    is_shipping = kargoMu,
                    // KARGO satirinda ad GONDERILMEZ (E4): etiket ekranda SOZLUKTEN cizilir.
                    // Bos birakmak bunu istemci adabi olmaktan cikarip YAPISAL kilar.
                    product_name = kargoMu ? null : l.product_name,
                    quantity = l.quantity,
                    unit_price = l.unit_price,
                    line_subtotal = l.line_subtotal,
                    vat_rate = l.vat_rate,
                    vat_amount = l.vat_amount,
                    line_total = l.line_total
                });
            }

            // KDV KIRILIMI: baslik tax_rate AGIRLIKLI ORTALAMADIR ve ekrana oran olarak
            // cikarsa var olmayan bir oran (or. %14,16) beyan edilirdi. Bu yuzden kalemler
            // KENDI oranlarina gore gruplanir ve ekran oran BAZINDA gosterir.
            view.vat_breakdown = lines
                .GroupBy(x => x.vat_rate)
                .OrderBy(g => g.Key)
                .Select(g => new InvoiceViewVatGroupDto
                {
                    vat_rate = g.Key,
                    base_amount = g.Sum(x => x.line_subtotal),
                    vat_amount = g.Sum(x => x.vat_amount),
                    gross_amount = g.Sum(x => x.line_total)
                })
                // B1 (MK-4b denetim bulgusu): HICBIR SEYE KATKI VERMEYEN grup KIRILIMDA GORUNMEZ.
                // BEDAVA kargoda K1 yine bir kalem yazar (D1 sozlesmesi: her faturada TAM 1 kargo
                // kalemi) ama tutari 0,00'dir. O kalem kosulsuz TaxRate ile damgalandigi icin,
                // urunleri %10 olan bir sipariste kirilima "KDV %20 (Matrah 0,00) - 0,00" satiri
                // girerdi: VAR OLMAYAN bir oran BEYAN EDILIRDI - K2'nin acildigi kusurun TAM AYNI
                // SINIFI. Suzgec KAYITTA degil GORUNTULEMEDE: fatura kalemi (D1) AYNEN durur.
                .Where(g => g.base_amount != 0m || g.vat_amount != 0m || g.gross_amount != 0m)
                .ToList();

            return (HttpStatusCode.OK, new SuccessDataResult<InvoiceViewResponseDto>(view));
        }

        // Açıklayıcı yorum: MANUEL ÖDEME ONAYI (admin) - Havale/EFT parası hesaba geçince sipariş onaylanır.
        // Rezervasyon gerçek stok düşümüne çevrilir (online ödemede callback'in yaptığını admin manuel yapar).
        // Sadece Pending + Havale(2) siparişlerde geçerli; idempotent (zaten onaylıysa tekrar işlemez).
        public async Task<(HttpStatusCode, Result)> ConfirmManualPayment(int orderId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            if (order.payment_type != GirdiSinirlari.OdemeHavale)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ManualPaymentOnlyBankTransfer));
            // ══ GF-6 / K5 (D5) - BU KONTROL BILINCLI OLARAK KALDI (MAKINE ONU IFADE EDEMEZ) ══
            //
            // "status != Pending" burada durum MAKINESININ kopyasi DEGIL, IDEMPOTENSI kapisidir.
            // Olculdu: `OrderStatusMachine.IsValidTransition(Confirmed, Confirmed)` TRUE doner
            // (`from == to` no-op, makinenin kendi sozlesmesi). Yani bu satir kaldirilip yerine
            // yalniz `DurumYaz` konulsaydi, ZATEN ONAYLANMIS bir havale siparisi IKINCI KEZ
            // onaylanir; `ConfirmReservation` + zaman cizelgesi + `PaymentConfirmed` olayi
            // TEKRAR kosar ve musteri sadakat puanini/faturayi IKI KEZ alirdi. Bu, D5'in
            // kaldirmak istedigi "elle kopya" degil, makinenin YANITLAYAMADIGI ayri bir sorudur.
            // Yazimin KENDISI yine de tek kapidan gecer (asagidaki `DurumYaz`).
            if (order.status != (byte)OrderStatusEnum.Pending || order.is_online_payment_done)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderAlreadyProcessed));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (!DurumYaz(order, OrderStatusEnum.Confirmed))
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidStatusTransition));
                }
                order.is_online_payment_done = true;   // ödeme alındı (havale onaylandı)
                await _orderDal.UpdateAsync(order);
                await _stockService.ConfirmReservation(order.id);
                await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Havale/EFT ödemesi onaylandı");
                // DALGA-2-FIX (B10): olay TRANSACTION ICINDE - havale onayi da dort yan etkinin
                // TAMAMINI tetikler. Onceden commit sonrasi YALNIZ fatura kesiliyordu.
                await SiparisOnaylandiOlayiYazAsync(order);
                await _unitOfWork.CommitAsync();
                // Fatura SENKRON kalir (gerekcesi PlaceOrder'daki ayni cagrida yazili - olculdu).
                await _orderConfirmation.ApplyConfirmedSideEffectsAsync(order.id);
                return (HttpStatusCode.OK, new SuccessResult(Messages.ManualPaymentConfirmed));
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        // Açıklayıcı yorum: Sipariş durumu değiştir (admin). byte status cast.
        // DALGA B / B2: siparis durumunun MUSTERIYE gosterilecek Turkce adi. Zaman cizelgesi notu
        // musteriye gorunuyor (order/timeline) ve ham enum adi basiyordu. Tanimsiz bir deger gelirse
        // ham ada duser - UYDURMA etiket yazilmaz.
        private static readonly Dictionary<byte, string> SiparisDurumAdlari = new()
        {
            [(byte)OrderStatusEnum.Pending] = "Onay bekliyor",
            [(byte)OrderStatusEnum.Confirmed] = "Onaylandı",
            [(byte)OrderStatusEnum.Preparing] = "Hazırlanıyor",
            [(byte)OrderStatusEnum.Shipped] = "Kargoda",
            [(byte)OrderStatusEnum.Delivered] = "Teslim edildi",
            [(byte)OrderStatusEnum.Cancelled] = "İptal edildi"
        };
        private static string SiparisDurumAdi(byte s) => SiparisDurumAdlari.TryGetValue(s, out var v) ? v : ((OrderStatusEnum)s).ToString();

        public async Task<(HttpStatusCode, Result)> ChangeOrderStatus(OrderStatusChangeRequestDto dto)
        {
            var order = await _orderDal.GetAsync(o => o.id == dto.id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            var previousStatus = order.status;

            // Açıklayıcı yorum: GEÇERLİ ENUM + GEÇİŞ VALİDASYONU. Admin bile keyfi/geçersiz geçiş yapamaz
            // (Cancelled->Shipped, Delivered->Pending gibi tutarsızlıklar veya tanımsız status=99 engellenir).
            if (!Enum.IsDefined(typeof(OrderStatusEnum), (int)dto.order_status))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidStatusTransition));
            if (!OrderStatusMachine.IsValidTransition((OrderStatusEnum)previousStatus, (OrderStatusEnum)dto.order_status))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidStatusTransition));

            // DALGA-2-FIX (B10): DURUM YAZIMI + ZAMAN CIZELGESI + ONAY OLAYI ARTIK ATOMIK.
            // Onceden bu yolda HIC transaction yoktu: `UpdateAsync` aninda SaveChanges yapiyor,
            // zaman cizelgesi AYRI bir yazma oluyordu. Onay olayini "aynen madde 3'un kalibi"
            // ile yazmak icin bir transaction sinirina ihtiyac var - yoksa "durum Confirmed
            // yazildi ama olay yazilamadi" penceresi acik kalirdi ve dort yan etki yine
            // sessizce kaybolabilirdi (bu dalganin duzelttigi kusurun yeni bir bicimi).
            // KAPSAM BILEREK DAR: transaction YALNIZ bu uc yazmayi sariyor. Iptal dalinin
            // stok/iade/fatura isleri `HandleStatusSideEffects` icinde, COMMIT SONRASINDA ve
            // eskisi gibi best-effort kaliyor - o davranisa DOKUNULMADI.
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // GF-6 / K5: yazim TEK KAPIDAN. Yukaridaki :798 on kontrolu ayni makineyi
                // sorar; burasi yazimin KENDISINI baglar - iki soru ARASINDA kod yok.
                if (!DurumYaz(order, (OrderStatusEnum)dto.order_status))
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderInvalidStatusTransition));
                }
                // Açıklayıcı yorum: Teslim edildiğinde teslim zamanını kaydet (iade penceresi buradan sayılır, sipariş tarihinden değil).
                if (order.status == (byte)OrderStatusEnum.Delivered && !order.delivered_at.HasValue)
                    order.delivered_at = DateTime.Now;
                await _orderDal.UpdateAsync(order);

                // Açıklayıcı yorum: Durum değişimini zaman çizelgesine kaydet (yalnızca gerçek değişimde)
                // DALGA B / B2: not MUSTERIYE gorunur (order/timeline -> Hesabim > Siparislerim) ve
                // eskiden ham ENUM ADI basiyordu. CANLI olculdu: musterinin cizelgesinde
                // "Durum guncellendi: Preparing" yaziyordu - Turkce vitrinde Ingilizce bir sabit.
                // Bu bir GORUNTU dizgesidir (CLAUDE.md bolum 6c), Turkce dogru olandir; durumun
                // makine-okunur hali zaten ayni satirin `status` byte'indadir.
                if (order.status != previousStatus)
                    await _statusHistory.RecordAsync(order.id, order.status, $"Durum güncellendi: {SiparisDurumAdi(order.status)}");

                // DALGA-2-FIX (B10): admin bir siparisi Confirmed'a tasidiginda da dort yan etki
                // uygulanir. `!= previousStatus` sarti MUKERRER mesaji engeller: zaten Confirmed
                // olan bir siparise ayni durum tekrar yazilirsa olay URETILMEZ.
                if (order.status == (byte)OrderStatusEnum.Confirmed && previousStatus != (byte)OrderStatusEnum.Confirmed)
                    await SiparisOnaylandiOlayiYazAsync(order);

                // ══ GF-6 / F1 (K4-DAR) - KAPIDA ODEMEDE SADAKAT TESLIMATTA KAZANILIR ═══════
                //
                // COD'da para TESLIMATTA alinir; puan `Confirmed`da verilirse musteri hicbir sey
                // odemeden puan kazanir (AV-3 / T1-B4'un sadakat yuzu). Cozum, uygulayiciyi
                // BOLMEK degil - `PaymentConfirmedSideEffects` TEK PARCA kalir ve `Confirmed`
                // dalinda sadakat adimini ATLAR (gerekce orada). Teslimatta AYNI olay YENIDEN
                // yazilir; dort adimin dordu de IDEMPOTENT oldugu icin yalnizca ATLANMIS olan
                // sadakat adimi fiilen kosar (fatura/referans/kupon satiri NO-OP doner).
                //
                // `!= previousStatus` sarti MUKERRER olayi engeller (Confirmed dalindaki emsalin
                // aynisi). Online/havale siparislerinde de olay yazilir ama sadakat ZATEN
                // kazanilmis oldugu icin `EarnFromOrder`in H28 ledger kontrolu NO-OP doner.
                if (order.status == (byte)OrderStatusEnum.Delivered && previousStatus != (byte)OrderStatusEnum.Delivered)
                    await SiparisOnaylandiOlayiYazAsync(order);

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            // Açıklayıcı yorum: Durum değimine göre yan etkiler (fatura + bildirim). Best-effort - başarısızlık
            // sipariş güncellemesini geri almaz (bildirim/fatura ikincil). Hata loglanır, akış devam eder.
            await HandleStatusSideEffects(order, previousStatus);

            return (HttpStatusCode.OK, new SuccessResult(Messages.OrderStatusChanged));
        }

        // ══ DALGA-2-FIX (B10) - SIPARIS ONAY OLAYININ TEK YAZICISI ═════════════════════════════
        //
        // Dort yan etki (fatura, sadakat, referans odulu, kupon defteri+sayaci) TEK uygulayicida
        // yasar: PaymentConfirmedSideEffects. Oraya tek giris outbox mesajidir. Bu yardimci, bu
        // manager'daki UC onay yolunun (kapida odeme / magaza-kredisiyle tam odeme, havale admin
        // onayi, admin durum degisikligi) hepsinin AYNI mesaji AYNI bicimde yazmasini saglar.
        //
        // NEDEN TEK SATIRLIK BIR YARDIMCI: uc cagri yerine olay govdesini KOPYALAMAK, bu dalganin
        // duzelttigi kusurun (ayni mantigin yollara dagilmasi) yeni bir ornegi olurdu. Bir alan
        // eklendiginde (or. discount_amount) tek yerde eklenir.
        //
        // CAGRI KURALI: HER ZAMAN acik bir transaction ICINDE ve commit'ten ONCE cagrilir.
        private async Task SiparisOnaylandiOlayiYazAsync(Order order)
        {
            await _outboxService.WriteAsync("PaymentConfirmed", new Divisima.Bussiness.Events.PaymentConfirmedEvent
            {
                order_id = order.id,
                customer_id = order.customer_id,
                total_price = order.total_price,
                coupon_code = order.coupon_code,
                discount_amount = order.discount_amount
            });
        }

        // Açıklayıcı yorum: Sipariş durumu değişince tetiklenen yan etkiler - fatura üretimi + müşteri bildirimi.
        private async Task HandleStatusSideEffects(Order order, byte previousStatus)
        {
            var newStatus = order.status;
            if (newStatus == previousStatus) return;

            try
            {
                // Açıklayıcı yorum: Onaylanınca fatura üret (idempotent - InvoiceManager tekrar üretmez)
                // DALGA-2-FIX (B10): bu cagri KALIR ve fatura SENKRON kesilmeye devam eder; diger
                // uc yan etki (sadakat, referans odulu, kupon defteri) icin ONAY OLAYI yukarida,
                // durum yazimiyla AYNI transaction'da uretiliyor. Gerekce PlaceOrder'daki ayni
                // cagrinin uzerinde: faturayi asenkrona tasimak ISTENMEYEN bir davranis degisikligi
                // olurdu ve iki mevcut pin bunu olcerek yakaladi.
                if (newStatus == (byte)OrderStatusEnum.Confirmed)
                    await _orderConfirmation.ApplyConfirmedSideEffectsAsync(order.id);

                // Açıklayıcı yorum: İPTAL - stok geri kazanılır (hayalet stok kaybı önlenir).
                // Ödeme öncesi (Pending) iptal: rezervasyon serbest (reserved düşer, fiziksel stok zaten düşmedi).
                // Ödeme sonrası (Confirmed+) iptal: fiziksel stok düşmüştü -> geri yükle (IncreaseStock aynı zamanda
                // "stok gelince haber ver" bildirimini de tetikler).
                if (newStatus == (byte)OrderStatusEnum.Cancelled)
                {
                    if (previousStatus == (byte)OrderStatusEnum.Pending)
                    {
                        await _stockService.ReleaseReservation(order.id);
                    }
                    else
                    {
                        // ÇİFT-STOK FIX (H44): SADECE henüz iptal edilmemiş kalemlerin stoğu geri yüklenir.
                        // CancelItem ile tek tek iptal edilen kalemlerin stoğu ZATEN IncreaseStock ile iade edilmişti;
                        // filtresiz döngü onları TEKRAR iade ederdi -> hayalet stok (fiziksel olmayan mal) -> overselling.
                        // (H44'te aynı çift-sayım para tarafında Math.Min ile kapatılmıştı; stok tarafı eksikti.)
                        var cancelledItems = await _orderItemDal.GetListAsync(i => i.order_id == order.id && !i.is_cancelled);
                        foreach (var it in cancelledItems)
                            await _stockService.IncreaseStock(it.product_id, it.size, it.quantity, order.id);
                    }

                    // FİNANSAL (MERKEZİ İADE - DRY): iptal edilen sipariş ÖDENDİYSE parayı ödeme kaynağına göre iade et.
                    // Ödenen tutar = kart çekildiyse (is_online_payment_done) total; COD-ödenmemişse yalnız cüzdan payı
                    // (nakit henüz ödenmedi). RefundManager kart->Iyzico, cüzdan->store credit ayrımını yapar.
                    // ÇİFT-İADE FIX (H44): store_credit_used, CancelItem'da kalem iptallerinde DÜŞMEZ (total_price düşer).
                    // Bu yüzden cüzdan iadesini KALAN total_price ile SINIRLA; yoksa "kalemleri tek-tek iptal et + sonra
                    // tüm-siparişi iptal et" akışı zaten-iade-edilmiş kalemleri store_credit_used üzerinden TEKRAR iade ederdi.
                    var walletPortion = Math.Min(order.store_credit_used, order.total_price);
                    var paidAmount = order.is_online_payment_done ? order.total_price : walletPortion;
                    // SESSIZ IADE HATASI FIX (H53): bu cagrinin SONUCU HIC KONTROL EDILMIYORDU ve blok
                    // "yan etki hatasi ana akisi bozmaz" diyen bir catch icinde. Ama PARA ikincil yan etki DEGILDIR:
                    // Iyzico iadesi basarisiz olursa siparis Cancelled, stok geri, puan geri alinir ama MUSTERI
                    // PARASIZ KALIR ve hicbir yerde iz kalmaz. (ReturnManager ayni cagriyi kontrol edip rollback
                    // yapiyor - asimetri bug'in kendisiydi.) Durum degisikligi bu noktada zaten commit edildigi icin
                    // geri alinamaz; en azindan GORUNUR yapiyoruz: siparis zaman cizelgesine kritik not dusuyoruz ki
                    // operasyon manuel mutabakat yapabilsin.
                    var cancelRefund = await _refundService.RefundToSourceAsync(order, paidAmount, "Sipariş iptali - iade");
                    if (!cancelRefund.Success)
                        await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Cancelled,
                            $"KRİTİK: para iadesi BAŞARISIZ (tutar {paidAmount:N2}) - manuel müdahale gerekli");

                    // FARMING ENGELİ: iptal edilen siparişte ödemede KAZANILAN loyalty puanını GERİ AL. Aksi halde müşteri
                    // sipariş ver -> puan kazan -> iptal et -> refund AL + puanı krediye çevir ile sınırsız bedava kredi üretirdi.
                    await _loyaltyService.ReverseForOrder(order.customer_id, order.id);

                    // FATURA İPTALİ: sipariş iptal edildiğinde faturası da iptal edilmeliydi (InvoiceStatusEnum.Cancelled
                    // tanımlıydı ama hiçbir kod yazmıyordu) - iptal edilen sipariş muhasebe raporunda ciroda kalıyordu.
                    // Sipariş durumu bu noktada zaten kalıcı (yukarıda UpdateAsync ile kaydedildi), fatura onu görebilir.
                    await _orderConfirmation.ApplyCancelledSideEffectsAsync(order.id);
                }

                // Açıklayıcı yorum: Kargoya verilince / teslim edilince müşteriye bildir (MERKEZİ servis - DRY).
                // Ayni cagri ShipmentManager'dan da yapilir (kargo-kaynakli gecislerde bildirim atlanmaz).
                if (newStatus == (byte)OrderStatusEnum.Shipped || newStatus == (byte)OrderStatusEnum.Delivered)
                    await _orderNotificationService.NotifyStatusChangeAsync(order, (OrderStatusEnum)newStatus);
            }
            catch (Exception)
            {
                // Açıklayıcı yorum: Yan etki hatası ana akışı bozmaz (fatura/bildirim ikincil). Gerçekte ILogger ile loglanır.
            }
        }

        // Açıklayıcı yorum: Sipariş detayı. Kompozisyon - sipariş + kalemleri ayrı DAL çağrılarıyla (nav yok).
        public async Task<(HttpStatusCode, Result)> GetById(int id, int customerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<OrderDetailResponseDto>(Messages.OrderNotFound));

            // Açıklayıcı yorum: IDOR koruması - müşteri SADECE kendi siparişini görebilir.
            // Sahibi değilse "bulunamadı" döndür (varlık bilgisini sızdırma - Forbidden yerine NotFound).
            if (order.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorDataResult<OrderDetailResponseDto>(Messages.OrderNotFound));

            var data = _mapper.Map<OrderDetailResponseDto>(order);

            // Açıklayıcı yorum: Kalemleri ayrı DAL ile getir + ürün adını doldur (kompozisyon serviste)
            var items = await _orderItemDal.GetListAsync(i => i.order_id == order.id);
            // N+1 DUZELTMESI: kalem basina tekil urun sorgusu yerine, tum urunleri TEK sorguda getir.
            // 20 kalemli siparis: 20 sorgu -> 2 sorgu (kalemler + urunler).
            var productIds = items.Select(i => i.product_id).Distinct().ToList();
            var products = await _productDal.GetListAsync(p => productIds.Contains(p.id));
            var productNames = products.ToDictionary(p => p.id, p => p.name);
            var itemDtos = new List<OrderItemResponseDto>();
            foreach (var it in items)
            {
                itemDtos.Add(new OrderItemResponseDto
                {
                    product_id = it.product_id,
                    product_name = productNames.TryGetValue(it.product_id, out var pname) ? pname : "",
                    size = it.size,
                    quantity = it.quantity,
                    unit_price = it.unit_price,
                    line_total = it.unit_price * it.quantity,
                    is_cancelled = it.is_cancelled   // H44: iptal edilen kalem işaretlenir (toplam mutabakatı)
                });
            }
            data.items = itemDtos;

            return (HttpStatusCode.OK, new SuccessDataResult<OrderDetailResponseDto>(data, Messages.OrderListed));
        }

        // Açıklayıcı yorum: Müşterinin siparişleri (yeniden eskiye).
        public async Task<(HttpStatusCode, Result)> GetByCustomer(int customerId)
        {
            var orders = await _orderDal.GetListAsync(o => o.customer_id == customerId);
            var data = _mapper.Map<List<OrderListResponseDto>>(orders.OrderByDescending(o => o.created_at).ToList());
            return (HttpStatusCode.OK, new SuccessDataResult<List<OrderListResponseDto>>(data, Messages.OrderListed));
        }
        // Açıklayıcı yorum: Admin - tüm siparişleri filtreyle listele (sayfalı). Ciro/yönetim paneli için.
        public async Task<(HttpStatusCode, Result)> GetAllForAdmin(Divisima.Entity.Dtos.Order.AdminOrderFilterDto filter)
        {
            var page = filter.page < 1 ? 1 : filter.page;
            var size = filter.page_size is < 1 or > 100 ? 20 : filter.page_size;

            // SINIRSIZ YUKLEME FIX (H51): eslesen TUM siparisler bellege cekilip LINQ ile sayfalaniyordu
            // (all.Skip().Take()). 100 bin siparisli bir magazada HER admin sayfa goruntusu 100 bin kaydi
            // yukluyordu -> bellek sicramasi + yavaslik + GC baskisi. Projede zaten DB tarafinda sayfalayan
            // GetPagedAsync var (COUNT + OFFSET/FETCH tek sorguda, clamp'li). Ona gecildi.
            var paging = new Divisima.Core.Utilities.Dtos.PagingRequestDto { page = page, size = size };
            var pagedOrders = await _orderDal.GetPagedAsync(
                paging,
                o => (filter.status == null || o.status == filter.status.Value)
                     && (filter.start_date == null || o.created_at >= filter.start_date.Value)
                     && (filter.end_date == null || o.created_at <= filter.end_date.Value),
                o => o.created_at,
                descending: true);

            var total = pagedOrders.TotalCount;
            var items = pagedOrders.Items
                .Select(o => new Divisima.Entity.Dtos.Order.AdminOrderListItemDto
                {
                    id = o.id,
                    order_number = o.order_number,
                    customer_id = o.customer_id,
                    status = o.status,
                    status_name = ((OrderStatusEnum)o.status).ToString(),
                    total_price = o.total_price,
                    payment_type = o.payment_type,
                    coupon_code = o.coupon_code,
                    created_at = o.created_at
                })
                .ToList();

            // DALGA B / B2: repository tipi (PagedResult<T>) ARTIK HTTP yanitina KONMUYOR.
            // Gerekce ve olculen zarar AdminOrderPagingListResponseDto'nun basinda; ozetle
            // o tip PascalCase serilesip { items, totalCount, ... } uretiyordu, deponun
            // diger sayfali uclari ise { items, total_count, ... } donuyor - ayni API'de
            // iki konvansiyon vardi ve admin siparis listesi bu yuzden CANLIDA BOSTU.
            var paged = new Divisima.Entity.Dtos.Order.AdminOrderPagingListResponseDto
            {
                items = items,
                total_count = total,
                page = page,
                size = size,
                total_pages = size > 0 ? (int)Math.Ceiling(total / (double)size) : 0
            };
            return (HttpStatusCode.OK, new SuccessDataResult<Divisima.Entity.Dtos.Order.AdminOrderPagingListResponseDto>(paged));
        }

        // Açıklayıcı yorum: KISMİ İPTAL - siparişten tek kalemi iptal et.
        // Yalnız ödenmiş+kargolanmamış (Confirmed/Preparing) siparişlerde: stok iade + kalem iptal +
        // tutar düş + iptal edilen kalem için INLINE mağaza kredisi (iade) + son kalemse tüm sipariş iptal.
        public async Task<(HttpStatusCode, Result)> CancelItem(int orderId, int orderItemId, int customerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // Açıklayıcı yorum: IDOR koruması - sipariş bu müşteriye mi ait
            if (order.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));   // TEK SOZLESME: sahiplik ihlali de "bulunamadi" (varlik sizdirilmaz)

            // Açıklayıcı yorum: Sadece ödenmiş ve henüz kargolanmamış siparişlerde kısmi iptal
            if (order.status != (byte)OrderStatusEnum.Confirmed && order.status != (byte)OrderStatusEnum.Preparing)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderItemNotCancellable));

            var item = await _orderItemDal.GetAsync(i => i.id == orderItemId && i.order_id == orderId && !i.is_cancelled);
            if (item == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderItemNotFound));

            var customer = await _customerDal.GetAsync(c => c.id == customerId);
            if (customer == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // FAZLA-IADE DUZELTMESI: musteri kupon indirimiyle DAHA AZ odedi; liste fiyatini iade etmek
            // fazla-iade olurdu (musteri iadeden kar eder). Kaleme dusen indirimi orantili hesapla.
            var grossItem = item.unit_price * item.quantity;                 // liste (indirim oncesi) tutar
            decimal itemDiscount = order.subtotal > 0
                ? MoneyHelper.Round(order.discount_amount * grossItem / order.subtotal) : 0m;
            var lineAmount = grossItem - itemDiscount;                        // kalem icin GERCEK odenen (iade edilecek)

            await _unitOfWork.BeginTransactionAsync();
            decimal refundedTotal = 0m;
            // Sipariş bu çağrıyla tümüyle iptale döndü mü - fatura iptali commit SONRASI tetiklenecek.
            bool orderFullyCancelled = false;
            try
            {
                // Açıklayıcı yorum: Stoğu iade et (IncreaseStock kendi transaction'ını açmaz - ambient'e katılır)
                await _stockService.IncreaseStock(item.product_id, item.size, item.quantity, order.id);

                // Açıklayıcı yorum: Kalemi iptal işaretle
                item.is_cancelled = true;
                await _orderItemDal.UpdateAsync(item);

                // Açıklayıcı yorum: Sipariş tutarlarını tutarlı düş (total = subtotal - discount + shipping korunur).
                // subtotal liste kadar, discount kaleme düşen indirim kadar, total gerçek-iade kadar düşer.
                order.subtotal = Math.Max(0m, order.subtotal - grossItem);
                order.discount_amount = Math.Max(0m, order.discount_amount - itemDiscount);
                order.total_price = Math.Max(0m, order.total_price - lineAmount);

                // İADE MODELİ (H44): yalnız ÖDENEN tutar iade edilir. Online(kart) siparişte kalem TAM ödenmiştir -> lineAmount.
                // COD/cüzdan siparişte COD-nakit HENÜZ ÖDENMEDİ -> yalnız ödenen store-credit payı iade edilir + store_credit_used
                // düşürülür. Yoksa: müşteri COD siparişe az store-credit uygulayıp kalemleri iptal ederek ödemediği nakit için
                // store-credit kazanırdı (BEDAVA PARA) + sonraki tüm-sipariş-iptali bayat store_credit_used ile çift-iade ederdi.
                // ATOMİK artış (tracked "customer.store_credit +=" eşzamanlı atomik harcamayı ezerdi = lost update).
                decimal itemRefund;
                if (order.is_online_payment_done)
                {
                    itemRefund = lineAmount;
                }
                else
                {
                    itemRefund = Math.Min(lineAmount, order.store_credit_used);
                    order.store_credit_used = Math.Max(0m, order.store_credit_used - itemRefund);
                }
                if (itemRefund > 0m)
                {
                    // SESSIZ PARA KAYBI FIX (H54): 0 satir etkilenirse bakiye artmadi -> defter yazma, iptal et.
                    var creditedItem = await _customerDal.IncrementStoreCreditAsync(customerId, itemRefund);
                    if (creditedItem == 0)
                    {
                        await _unitOfWork.RollbackAsync();
                        return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderItemCancelFailed));
                    }
                    await _creditTxDal.AddAsync(new StoreCreditTransaction
                    {
                        customer_id = customerId,
                        amount = itemRefund,
                        type = (byte)LedgerEntryTypeEnum.Earn,
                        reason = "Kısmi iptal iadesi",
                        order_id = order.id,
                        created_at = DateTime.Now
                    });
                    refundedTotal += itemRefund;
                }

                // Açıklayıcı yorum: Aktif (iptal edilmemiş) kalem kaldı mı - kalmadıysa tüm siparişi iptal et
                // PERFORMANS (H51): EXISTS - kalan kalem VAR MI (hepsini cekmeye gerek yok).
                var hasRemaining = await _orderItemDal.AnyAsync(i => i.order_id == orderId && !i.is_cancelled);
                if (!hasRemaining && DurumYaz(order, OrderStatusEnum.Cancelled))
                {
                    // GF-6 / K5: yazim TEK KAPIDAN gecer. DURUST SINIR (MK-6 ile olculdu):
                    // metodun basindaki "Confirmed veya Preparing" on kosulu KALDIRILMADI -
                    // o kosul yalnizca ->Cancelled gecisini degil, KALEM IPTALININ kendisinin
                    // hangi durumlarda mesru oldugunu da soyluyor ve makine bunu ifade edemez.
                    // Dolayisiyla bu satirin bugunku katkisi SAVUNMA DERINLIGIDIR: on kosul
                    // bir gun gevsetilirse durum yine de makinesiz YAZILAMAZ.
                    // Gecis reddedilirse siparis durumu DEGISMEZ - kalem iptalleri ve iadeleri
                    // ZATEN yazildi, sessizce yanlis durum YAZILMAZ.
                    orderFullyCancelled = true;
                    // TUTARLILIK FIX (H44): son kalem iptaliyle sipariş TÜMÜYLE iptal -> kalan tutarı (kargo) da iade et
                    // (tüm-sipariş iptali yolu total'in tamamını=kargo dahil iade eder; yoksa müşteri kargoyu kaybederdi).
                    // Yine yalnız ÖDENEN pay: online->kalan total_price, COD/cüzdan->kalan store_credit_used kadar (nakit ödenmedi).
                    decimal leftoverRefund = order.is_online_payment_done
                        ? order.total_price
                        : Math.Min(order.total_price, order.store_credit_used);
                    if (leftoverRefund > 0m)
                    {
                        // SESSIZ PARA KAYBI FIX (H54): kalan (kargo) iadesinde de ayni kontrol.
                        var creditedLeft = await _customerDal.IncrementStoreCreditAsync(customerId, leftoverRefund);
                        if (creditedLeft == 0)
                        {
                            await _unitOfWork.RollbackAsync();
                            return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderItemCancelFailed));
                        }
                        await _creditTxDal.AddAsync(new StoreCreditTransaction
                        {
                            customer_id = customerId,
                            amount = leftoverRefund,
                            type = (byte)LedgerEntryTypeEnum.Earn,
                            reason = "Tam iptal - kalan (kargo) iadesi",
                            order_id = order.id,
                            created_at = DateTime.Now
                        });
                        refundedTotal += leftoverRefund;
                    }
                    order.total_price = 0m;
                    // ══ DALGA-2-FIX (B12) - KARGO BEDELI DE SIFIRLANIR ═════════════════════════
                    // OLCULEN DURUM (Dalga 2, dev veritabani): tam iptal edilmis YEDI siparis
                    // `subtotal=0, discount=0, shipping=49.90, total=0` tasiyordu. Yani muhasebe
                    // kimligi `total = subtotal - indirim + kargo` KIRILIYORDU: kalem iptalleri
                    // subtotal'i ve indirimi dusuruyor, tam iptal total'i sifirliyor, ama kargo
                    // kolonu ILK DEGERINDE kaliyordu.
                    // GORUNEN ZARAR: musterinin siparis detayi ve fatura govdesi 0,00 TL'lik iptal
                    // sipariste "Kargo: 49,90 TL" yaziyordu - odenmeyecek bir kalem gibi.
                    //
                    // SIRA KRITIK: bu satir `leftoverRefund` HESAPLANDIKTAN SONRA. Iade tutari
                    // `order.total_price` uzerinden turetiliyor ve o deger kargoyu ICERIYOR;
                    // kargoyu once sifirlamak musteriye kargo bedelini IADE ETMEMEK olurdu.
                    // PARA YOLU DEGISMEDI - yalnizca defterlenen kolon duzeltildi (pin bunu da korur).
                    order.shipping_cost = 0m;
                    // FARMING ENGELİ: son kalem de iptal edilip sipariş tümüyle iptale döndüyse kazanılan loyalty puanını GERİ AL.
                    await _loyaltyService.ReverseForOrder(order.customer_id, order.id);
                }

                // DERLEME FIX (H44): Order entity'sinde "updated_at" alanı YOK -> ona atama CS1061'di (build patlardı).
                await _orderDal.UpdateAsync(order);

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderItemCancelFailed));
            }

            // FATURA İPTALİ: son kalem de iptal edilip sipariş tümüyle Cancelled'a döndüyse faturasını da iptal et.
            // TRANSACTION DIŞINDA (CommitAsync'ten SONRA): fatura iptali ayrı bir SaveChanges'tır ve sipariş
            // durumunun KALICI olmasına dayanır - transaction içinde çağrılsaydı CancelForOrder'ın okuduğu
            // sipariş henüz commit edilmemiş olurdu ve rollback halinde fatura yanlışlıkla iptalde kalırdı.
            if (orderFullyCancelled)
                await _orderConfirmation.ApplyCancelledSideEffectsAsync(order.id);

            var iptalMesaji = refundedTotal > 0m
                ? Messages.OrderItemCancelled
                : "Kalem iptal edildi. Ödeme alınmadığı için iade oluşmadı.";
            return (HttpStatusCode.OK, new SuccessDataResult<decimal>(refundedTotal, iptalMesaji));
        }

        // Açıklayıcı yorum: Tahmini teslim tarihi (sipariş tarihi + iş günü penceresi). Teslim edilmişse gerçek tarih.
        public async Task<(HttpStatusCode, Result)> GetEstimatedDelivery(int orderId, int customerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null) return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            if (order.customer_id != customerId)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));   // TEK SOZLESME: sahiplik ihlali de "bulunamadi" (varlik sizdirilmaz)

            var (earliest, latest) = DeliveryEstimator.Estimate(order.created_at);
            var dto = new Divisima.Entity.Dtos.Order.EstimatedDeliveryDto
            {
                order_id = orderId,
                earliest = earliest,
                latest = latest
            };
            return (HttpStatusCode.OK, new SuccessDataResult<Divisima.Entity.Dtos.Order.EstimatedDeliveryDto>(dto));
        }

    }
}
