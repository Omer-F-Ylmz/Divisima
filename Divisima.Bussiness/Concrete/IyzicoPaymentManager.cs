using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Events;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Locking;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Payment;
using Divisima.Entity.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: GÜVENLİ Iyzico ödeme akışı.
    // 1) Initialize -> Checkout Form (kart sunucuya gelmez). Payment kaydı (Pending) + token.
    // 2) Callback -> imza doğrula -> GERÇEK sonucu Iyzico'dan token ile çek (callback gövdesine GÜVENME)
    //    -> ödenen tutar == sipariş tutarı mı? -> fraud onaylı mı? -> ancak o zaman siparişi onayla.
    //    Aksi halde iptal + stok iade. Tüm adım idempotent + transaction'lı.
    public class IyzicoPaymentManager : IPaymentService
    {
        private readonly IPaymentDal _paymentDal;
        private readonly IOrderDal _orderDal;
        private readonly IIyzicoClient _iyzico;
        private readonly IStockService _stockService;
        private readonly ICustomerDal _customerDal;
        private readonly IFraudCheckService _fraudCheck;
        private readonly IDistributedLock _distributedLock;
        private readonly ICouponDal _couponDal;
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly ILoyaltyService _loyaltyService;
        private readonly IReferralService _referralService;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderConfirmationService _orderConfirmation;
        // SPRINT 8 MADDE 3: odeme onayi yan etkileri outbox uzerinden islenir.
        private readonly Divisima.Bussiness.Outbox.IOutboxService _outboxService;
        // Aciklayici yorum: Iyzico:CallbackUrl - DTO callback_url BOS geldiginde kullanilan OPERATOR girdisi (E2b).
        private readonly IConfiguration _config;
        // Commit sonrasi yan etkiler artik SESSIZ dusmuyor - patlayan adim adiyla loglanir (S7).
        private readonly ILogger<IyzicoPaymentManager> _logger;
        // GF-5 / K2: sahiplik ihlali ve webhook imza hatasi IZ birakir. Yalniz YAZAR - hicbir
        // karar bu servise bagli degildir, donus kodlari ve mesajlar DEGISMEDI.
        private readonly ISecurityEventService _securityEvents;

        public IyzicoPaymentManager(IPaymentDal paymentDal, IOrderDal orderDal,
            IIyzicoClient iyzico, IStockService stockService, ICustomerDal customerDal,
            IFraudCheckService fraudCheck, IDistributedLock distributedLock,
            ICouponDal couponDal,
IOrderStatusHistoryService statusHistory,
ILoyaltyService loyaltyService,
IReferralService referralService, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork,
            IOrderConfirmationService orderConfirmation, IConfiguration config, ILogger<IyzicoPaymentManager> logger,
            Divisima.Bussiness.Outbox.IOutboxService outboxService,
            ISecurityEventService securityEvents)
        {
            _securityEvents = securityEvents;
            _logger = logger;
            _paymentDal = paymentDal;
            _orderDal = orderDal;
            _iyzico = iyzico;
            _stockService = stockService;
            _customerDal = customerDal;
            _fraudCheck = fraudCheck;
            _distributedLock = distributedLock;
            _couponDal = couponDal;
            _statusHistory = statusHistory;
            _loyaltyService = loyaltyService;
            _referralService = referralService;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
            _orderConfirmation = orderConfirmation;
            _outboxService = outboxService;
            _config = config;
        }

        public async Task<(HttpStatusCode, Result)> Initialize(PaymentInitRequestDto dto, int authenticatedCustomerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == dto.order_id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<PaymentInitResponseDto>(Messages.OrderNotFound));

            // ══ GF-1 / K4 (B-1) - SAHIPLIK IHLALI 404, 403 DEGIL ══════════════════════════
            //
            // `ReturnManager`dakinin IKIZI - `SecureControllerBase`teki tek sozlesme (404,
            // varlik sizdirilmaz) burada da ihlal ediliyordu. Bu uc ODEME BASLATMA ucu, yani
            // sizinti "hangi siparis id'leri gercek + odenmemis" sorusunu ANONIM'e yakin bir
            // maliyetle yanitliyordu. Yanit ustteki "yok" daliyla BIREBIR ayni.
            if (order.customer_id != authenticatedCustomerId)
            {
                // GF-5 / K2 (D4): 404 SOZLESMESI DEGISMEDI - yalnizca IZ eklendi. Olay YAZILIR,
                // sonra AYNI yanit doner; hicbir karar bu cagriya bagli degildir.
                await _securityEvents.SahiplikIhlaliAsync("order", dto.order_id, authenticatedCustomerId);
                return (HttpStatusCode.NotFound, new ErrorDataResult<PaymentInitResponseDto>(Messages.OrderNotFound));
            }

            if (order.is_online_payment_done)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentAlreadyDone));

            // Açıklayıcı yorum: SİPARİŞ DURUMU KONTROLÜ - sadece Pending siparişe ödeme başlatılır
            // (iptal/teslim/kargo edilmiş siparişe ödeme engellenir)
            if (order.status != (byte)OrderStatusEnum.Pending)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentOrderNotPayable));

            // Açıklayıcı yorum: KALAN TUTAR = sipariş toplamı - uygulanan mağaza kredisi. Online sadece KALANI tahsil edilir.
            // Cüzdan siparişi tamamen karşıladıysa PlaceOrder onu zaten Confirmed+is_online_payment_done yapar (yukarıdaki guard yakalar).
            decimal amountDue = order.total_price - order.store_credit_used;
            if (amountDue <= 0)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentInvalidAmount));

            // Açıklayıcı yorum: TEKİL BEKLEYEN ÖDEME - aynı siparişe zaten bekleyen ödeme varsa yenisini açma
            var pending = await _paymentDal.GetAsync(p => p.order_id == order.id && p.payment_status == (byte)PaymentStatusEnum.Pending);
            if (pending != null)
                return (HttpStatusCode.Conflict, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentPendingExists));

            // Açıklayıcı yorum: FRAUD/HIZ KONTROLÜ - kısa sürede çok deneme = kart testi saldırısı, engelle.
            // TEK ATOMİK çağrı: sayacı artırır VE limit aşıldı mı döner (ayrı RecordAttempt YOK -> lost-update/TOCTOU yok).
            if (!await _fraudCheck.CanAttemptPaymentAsync(order.customer_id))
                return (HttpStatusCode.TooManyRequests, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentTooManyAttempts));

            // Açıklayıcı yorum: SSRF engeli - callback_url verildiyse yalnız güvenli public HTTPS olmalı
            if (!string.IsNullOrWhiteSpace(dto.callback_url) && !UrlValidator.IsSafePublicHttpsUrl(dto.callback_url))
                return (HttpStatusCode.BadRequest, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentInvalidCallbackUrl));

            // Aciklayici yorum: CALLBACK ADRESI COZUMU (E2b).
            // DTO DOLUYSA mevcut davranis AYNEN surer - yukaridaki SSRF guard onu zaten dogruladi.
            // DTO BOS ise operator girdisi olan Iyzico:CallbackUrl kullanilir. Gerekce: storefront bu adresi
            // BILMEZ ve bilmemeli, gercek Iyzico ise BOS callbackUrl kabul etmez (form baslatilamaz).
            // Config degeri DTO guardina TABI DEGIL: kullanici girdisi degil OPERATOR girdisidir. Dev ortaminda
            // http://localhost:5000/... olabilir; guard yalniz public HTTPS kabul ettigi icin ayni deger DTO
            // uzerinden GECEMEZDI. Ayar yoksa bos kalir - yani E2b oncesi davranis birebir korunur.
            var callbackUrl = !string.IsNullOrWhiteSpace(dto.callback_url)
                ? dto.callback_url
                : (_config["Iyzico:CallbackUrl"] ?? "");

            var customer = await _customerDal.GetAsync(c => c.id == order.customer_id);
            var conversationId = Guid.NewGuid().ToString("N");

            // Açıklayıcı yorum: Checkout Form başlat - TUTAR SUNUCUDAN (order.total_price), client'tan değil
            var init = await _iyzico.InitializeCheckoutFormAsync(new IyzicoCheckoutInitRequest
            {
                ConversationId = conversationId,
                Price = amountDue,   // KALAN tutar (cuzdan dususu sonrasi)
                CallbackUrl = callbackUrl,
                CustomerId = order.customer_id,
                BuyerName = customer?.name ?? "Musteri",
                BuyerEmail = customer?.email ?? ""
            });
            if (!init.Success)
            {
                // SPRINT 8 MADDE 8 - AYIRT EDILEBILIR MESAJ.
                // Onceden her init hatasi ayni "Odeme baslatilamadi." mesajini donuyordu; musteri
                // ne oldugunu ve ne yapabilecegini goremiyordu. E2b'de OLCULEN gercek vaka:
                // gercek Iyzico "@divisima.test" adresini "email hatali format ile gonderilmistir"
                // ile REDDEDIYOR; AYNI musteri example.com adresiyle 200 aliyor. Yani BIZIM kabul
                // ettigimiz bir e-posta ile uye olan musteri HIC kart odemesi yapamiyor.
                //
                // Saglayicinin ham hata metnini MUSTERIYE YANSITMIYORUZ (isyeri/yapilandirma
                // ayrintisi sizabilir) ve metin ESLESTIRMESI de yapmiyoruz (yabanci bir API'nin
                // dizgesine bagimli olmak kirilgandir). Bunun yerine sebebi KENDIMIZ tespit
                // ediyoruz: alici e-postasi teslim edilemez bir ust alan adiysa mesaj ONA gore
                // olur. Diger tum hatalar eski genel mesajda kalir - yanlis teshis vermeyiz.
                var aliciEposta = customer?.email ?? "";
                if (TeslimEdilemezEposta(aliciEposta))
                    return (HttpStatusCode.BadRequest,
                        new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentBuyerEmailNotAccepted));

                return (HttpStatusCode.BadRequest, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentInitFailed));
            }

            await _paymentDal.AddAsync(new Payment
            {
                order_id = order.id,
                payment_provider = "iyzico",
                payment_status = (byte)PaymentStatusEnum.Pending,
                amount = amountDue,                    // beklenen KALAN tutar (doğrulamada kullanılır)
                conversation_id = conversationId,
                token = init.Token,
                created_at = DateTime.Now
            });

            return (HttpStatusCode.OK, new SuccessDataResult<PaymentInitResponseDto>(new PaymentInitResponseDto
            {
                conversation_id = conversationId,
                checkout_form_content = init.CheckoutFormContent
            }, Messages.PaymentInitiated));
        }

        // E2: callback yonlendirmesi icin token -> siparis id. SALT OKUR, hicbir durum degistirmez.
        // Bulunamazsa 0 doner (cagiran yonlendirmeyi siparissiz yapar).
        public async Task<int> GetOrderIdByTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return 0;
            var payment = (await _paymentDal.GetListNoTrackingAsync(p => p.token == token)).FirstOrDefault();
            return payment?.order_id ?? 0;
        }

        public async Task<(HttpStatusCode, Result)> HandleCallback(PaymentCallbackRequestDto dto,
            PaymentNotificationChannel kanal = PaymentNotificationChannel.Strict)
        {
            // POLITIKA TEK YERDE TURER. Cagri yerleri yalnizca KANALI soyler; hangi savunmanin
            // gevsedigine burasi karar verir. Yeni bir politika eklenirse cagri yerleri DEGISMEZ.
            // Her iki gevseme de gerekcesiyle PaymentNotificationChannel enum'unda yazili.
            bool imzaZorunlu = kanal == PaymentNotificationChannel.Strict;
            bool tokenYasiSiniriUygula = kanal != PaymentNotificationChannel.ProviderWebhook;

            // 1) IMZA (E2b - OLCULEREK DEGISTI)
            // Iyzico CF callback POST edilen govdesinde YALNIZ "token" gonderiyor. Olculdu:
            // tarayici Network > callback > Payload > Form Data icinde TEK alan var, "signature"
            // alani YOK. Eski kod imzayi kosulsuz zorunlu tutuyordu, dolayisiyla GERCEK Iyzico ile
            // her gecerli odemenin callback'i "gecersiz imza" ile reddediliyordu (olculdu: callback
            // 4 ms'de 400 donuyor - retrieve HIC calismiyor; odeme satiri Pending, para Iyzico'da).
            //
            // Bu yolda OTORITE imza DEGIL: (i) token opak ve yalniz bize+Iyzico'ya ait,
            // (ii) sonuc SUNUCU-SUNUCU RetrievePaymentResultAsync ile Iyzico'dan cekiliyor,
            // (iii) token 30 dk zaman asimina tabi, (iv) tutar/para birimi/fraud dogrulaniyor,
            // (v) yalniz Pending odeme islenebiliyor. Sahte bir callback bunlarin hicbirini
            // atlayamaz - retrieve odenmemis bir token icin basari DONMEZ.
            //
            // GEVSEME SINIRLI: imza GELDIYSE yine dogrulanir (asagidaki kosul).
            //
            // SPRINT 8 MADDE 9 - WEBHOOK YOLU DA GEVSEDI (E2b'deki bu yorum ARTIK GECERSIZ:
            // "webhook imzaZorunlu=true ile cagrildigi icin oradaki zorunluluk AYNEN durur").
            // 22 Agustos 2026'da GERCEK Iyzico bildirimi olculdu: govdede "signature" alani YOK,
            // baslikta "X-Iyz-Signature" VAR ama BOS. Yani saglayici bu yolda da imza gondermiyor
            // ve kosulsuz zorunluluk her GERCEK bildirimi reddediyordu (canli kanit: siparis #33 -
            // para alindi, siparis Pending kaldi). Otorite artik ACIKCA retrieve zinciri.
            // AYRINTI VE GEREKCE: PaymentController.Webhook uzerindeki blok.
            //
            // imzaZorunlu HALA VAR ve varsayilani TRUE: gevseme iki cagri yerinde ACIKCA
            // yaziliyor, yeni bir cagiran yanlislikla gevsemis yola dusmuyor (fail-closed).
            if (imzaZorunlu || !string.IsNullOrWhiteSpace(dto.signature))
            {
                if (!_iyzico.VerifyCallbackSignature(dto.token, dto.signature))
                {
                    // GORUNURLUK: imza GELDIGI halde tutmuyorsa bu ya sahte bir istektir ya da
                    // saglayicinin imza BICIMI bizimkinden farklidir (or. panelde bir webhook
                    // anahtari acilir ve X-Iyz-Signature V3 bicimiyle dolmaya baslar). Ikinci
                    // durum SESSIZ kalirsa kesinti yine teshis edilemez halde geri gelir -
                    // bu yuzden ADIYLA loglanir.
                    // ══ GF-5 / K5 (SC-7 = SE-2) - JETON ARTIK MASKELI ═════════════════════════
                    //
                    // OLCULEN ONCE-DURUM: `dto.token` HAM basiliyordu. CLAUDE.md'nin
                    // "MASKELEME URETIM NOKTASINDA YAPILIR" kurali TAM OLARAK bunu yasakliyor
                    // ve ayni jeton `IyzicoClient.cs,206`da ZATEN maskeleniyordu - yani
                    // kural biliniyordu, bu dosyada uygulanmiyordu (GF-5 on olcumunde IKI ajan
                    // bagimsiz buldu). Sprint 8'de bir Iyzico odeme jetonunun depoya girmesi
                    // `secret-scan` job'ini KIRMISTI; bu satir ayni sinifin canli kalan orneginidir.
                    //
                    // TESHIS DEGERI KORUNUR: `KanitMaskesi` ilk 8 karakteri BIRAKIR, dolayisiyla
                    // deger hala `payments.token` ile eslestirilebilir - yukaridaki "saglayicinin
                    // imza BICIMI degismis olabilir" teshisi icin gereken sey budur.
                    _logger.LogWarning(
                        "ODEME IMZA DOGRULANAMADI. imzaZorunlu={Zorunlu} imzaGeldiMi={Geldi} token={Token}. " +
                        "Imza GELDIGI halde tutmuyorsa saglayicinin imza BICIMI degismis olabilir - " +
                        "Sprint 8 madde 9 notuna bak.",
                        imzaZorunlu, !string.IsNullOrWhiteSpace(dto.signature),
                        Divisima.Core.Utilities.Text.KanitMaskesi.Maskele(dto.token));

                    // ══ GF-5 / K2 - BOZUK IMZA ARTIK GUVENLIK OLAYI YAZIYOR ═══════════════════
                    //
                    // AV-2 / S-C matrisinde "webhook imza hatasi" TAM BOSLUKTU: log'a dusuyordu
                    // ama `security_events`e HICBIR SEY yazilmiyordu, yani SIEM tarafinda
                    // gorunmuyordu. DAVRANIS DEGISMEDI - 400 ve mesaj AYNEN duruyor (merkez
                    // karari D1: K7'nin kod yarisi DUSTU, imza VARSA ve bozuksa 400 STATUKO).
                    //
                    // `customer_id` NULL: bu uc ANONIM (saglayici cagiriyor) ve odeme satiri
                    // HENUZ okunmadi (`payment` okumasi ASAGIDA). `detail` kullanici girdisi TASIMAZ - yalniz iki
                    // BOOL; jeton buraya KONMAZ (KVKK + log forging + sir hijyeni).
                    await _securityEvents.LogAsync("PaymentSignatureInvalid", "Warning", null, null, null,
                        $"imzaZorunlu={imzaZorunlu} imzaGeldi={!string.IsNullOrWhiteSpace(dto.signature)}");
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PaymentSignatureInvalid));
                }
            }

            // NoTracking (S6): bu ilk okuma yalniz GUARD icin. Tracked okunursa DbContext bu satiri
            // izlemeye alir ve kilit sonrasindaki TAZE okuma identity resolution ile ayni bayat
            // nesneye duser (kok sebep buydu). Guncelleme yollarinda UpdateAsync detached nesneyi
            // zaten Attach+Modified yapiyor - davranis degismiyor.
            var payment = (await _paymentDal.GetListNoTrackingAsync(p => p.token == dto.token)).FirstOrDefault();
            if (payment == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.PaymentNotFound));

            // Açıklayıcı yorum: 2) IDEMPOTENCY - zaten işlenmişse tekrar işleme (replay engeli)
            if (payment.payment_status != (byte)PaymentStatusEnum.Pending)
                return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentAlreadyProcessed));

            // Açıklayıcı yorum: 2b) TOKEN ZAMAN AŞIMI - ödeme 30 dk içinde tamamlanmalı (eski token replay engeli)
            //
            // KANALA BAGLI (SUPHELI #15 - OLCULEREK DUZELTILDI). Bu guard TARAYICI yolu icin
            // dogrudur ve orada AYNEN durur: eski bir formun yeniden gonderilmesi gercek bir
            // senaryodur. WEBHOOK'ta ise UYGULANMAZ - saglayici bildirimi geciktirebilir ya da
            // saatler sonra yeniden deneyebilir; sinir orada da uygulaninca GECIKMIS ama GERCEK
            // bir bildirim, parasi ALINMIS bir odemeyi "Failed" diye defterliyordu (canli ornek:
            // siparis #33). Gevseyen TEK sey bu yas siniri: "yalniz Pending islenir" + retrieve
            // otoritesi + tutar + para birimi + fraud kontrolleri AYNEN duruyor.
            //
            // STOK TARAFI OLCULDU, sessiz bir tehlike YOK: rezervasyon bu arada expire olmus
            // olabilir, ama StockManager.ConfirmReservation bu durumu ZATEN ele aliyor -
            // stok varsa dogrudan dusuyor, yoksa hareket kaydina "UYARI: odeme alindi fakat
            // stok yok ... manuel iade/tedarik gerekli" notunu GURULTULU sekilde yaziyor.
            if (tokenYasiSiniriUygula && payment.created_at.AddMinutes(30) < DateTime.Now)
            {
                payment.payment_status = (byte)PaymentStatusEnum.Failed;
                await _paymentDal.UpdateAsync(payment);
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PaymentTokenExpired));
            }

            var order = await _orderDal.GetAsync(o => o.id == payment.order_id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // Açıklayıcı yorum: 3) DAĞITIK KİLİT - aynı siparişe eşzamanlı iki callback'ten yalnızca biri işler
            //    (race condition / çift onaylama engeli). Kilit alınamazsa kaynak meşgul, tekrar denenir.
            using var orderLock = await _distributedLock.AcquireAsync($"payment-order:{order.id}", TimeSpan.FromSeconds(30));
            if (orderLock == null)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.PaymentProcessingBusy));

            // Açıklayıcı yorum: Kilit sonrası durumu TEKRAR oku (kilit beklerken başka callback işlemiş olabilir).
            // BAYAT OKUMA FIX (S6): burada eskiden GetAsync (TRACKED) kullaniliyordu. Ayni DbContext
            // yukarida (satir ~143) ayni Payment satirini zaten izlemeye almisti; EF Core identity
            // resolution yuzunden ikinci sorgu DB'den taze degeri okusa bile AYNI bayat nesneyi
            // donduruyordu (olculdu: ilkOkuma=Pending, ikinciOkuma=Pending, dbGercek=Success,
            // referansAyni=True). Yani bu savunma satiri OLU idi: kilit sekiz callback'i duzgun
            // serilestirdigi halde (olculdu: kritik bolumde max esmanli=1) sekizi de basari dalini
            // calistiriyor, sadakat puani sekiz kez yaziliyordu. NoTracking okuma bunu duzeltir.
            payment = (await _paymentDal.GetListNoTrackingAsync(p => p.token == dto.token)).FirstOrDefault();
            if (payment == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.PaymentNotFound));
            if (payment.payment_status != (byte)PaymentStatusEnum.Pending)
                return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentAlreadyProcessed));
            // Açıklayıcı yorum: 4) GERÇEK SONUCU IYZICO'DAN ÇEK - callback gövdesine ASLA güvenme.
            // TRANSACTION SINIRI DISINDA (S7): asagidaki A bolgesi retry-guvenli bir transaction
            // icinde kosar; gecici bir DB kopmasinda tekrarlanabilir. Bu dis cagri ORAYA GIRMEZ -
            // aksi halde her retry saglayiciya yeni bir sorgu gonderirdi.
            var result = await _iyzico.RetrievePaymentResultAsync(dto.token);

            // Açıklayıcı yorum: 5) TUTAR DOĞRULAMA - ödenen tutar sipariş tutarını KARŞILAMALI (manipülasyon/eksik-ödeme engeli).
            //    KRİTİK: TAKSİTLİ ödemede Iyzico komisyonu ekler -> PaidPrice = amountDue + komisyon > payment.amount olur.
            //    Eskiden "== payment.amount" (tam eşleşme) idi -> GEÇERLİ taksit ödemeleri REDDEDİLİYORDU (installment_fee kodu ölüydü).
            //    Güvenlik: eksik ödeme (PaidPrice < amountDue) hâlâ reddedilir; fazlası yalnız MAKUL taksit komisyonu kadar (üst sınır=2x).
            //    PaidPrice güvenilir Iyzico callback'inden (HMAC doğrulanmış) gelir; fazla ödemenin saldırgan faydası yok.
            bool amountMatches = result.PaidPrice >= payment.amount && result.PaidPrice <= payment.amount * 2m;
            bool fraudOk = result.FraudStatus == "1";
            // Açıklayıcı yorum: PARA BİRİMİ KONTROLÜ - sipariş para birimi ile ödeme eşleşmeli (TRY siparişe USD engeli)
            bool currencyOk = string.Equals(result.Currency, order.currency ?? "TRY", StringComparison.OrdinalIgnoreCase);
            bool odemeGecerli = result.Success && amountMatches && fraudOk && currencyOk;

            // ══ A BOLGESI - GERCEK TRANSACTION (S7) ═════════════════════════════════════════
            // ONCEDEN burada BeginTransactionAsync() hemen ardindan CommitAsync() geliyordu: BOS bir
            // transaction acilip kapaniyor, sonraki TUM yazmalar ambient transaction OLMADAN her DAL
            // cagrisinda ayri SaveChanges ile kaliciliyor, alttaki CommitAsync() ve catch icindeki
            // RollbackAsync() no-op oluyordu. Yani yarida kalan bir callback KISMI DURUM birakiyordu
            // (or. cuzdana kredi yazilip defter kaydi yazilamadan cokme -> bakiye ile defter ayrisir).
            //
            // ExecuteInTransactionAsync SECILDI, manuel BeginTransaction DEGIL: Program.cs'te
            // EnableRetryOnFailure yorumda duruyor ve gerekcesi "transaction kullanan manager'lar
            // ExecuteInTransactionAsync'e tasinmali - manuel BeginTransaction retry stratejisi
            // tarafindan REDDEDILIR" diyor; IyzicoPayment o listedeydi. Bu tasima engeli kaldirir.
            // (Bayragi ACMA karari ayri - bu sprint kapsaminda degil.)
            //
            // ATOMIK DURUM GECISI transaction'a DAHIL: gecis artik KOSULLU bir kazanmadir. A bolgesi
            // patlarsa rollback gecisi de geri alir, odeme Pending'e doner ve yeniden giris TEMIZ olur.
            // Eszamanlilik ayrica saglamlasir: ikinci callback'in UPDATE'i satir kilidinde bloke olur,
            // yani tekillik artik yalniz uygulama kilidine bagli degil.
            // ══ GF-6 / K5 (D5) - T4/S-1: TERMINAL SIPARIS DIRILTILMEZ ══════════════════════════
            //
            // OLCULEN ONCE-DURUM (AV-3 / T4/S-1, LAUNCH BLOKER): odeme satirinin Pending->Success
            // CAS'i AYNI ODEMENIN iki kez islenmesini engelliyordu, ama SIPARISIN o arada
            // TERMINAL duruma gecmis olmasini HIC SORMUYORDU. Iptal edilmis (`Cancelled`) bir
            // siparis, gecikmis/yeniden gonderilmis bir basarili callback ile `Confirmed`a
            // DIRILIYOR; stok rezervasyonu gercek dusume ceriliyor, `PaymentConfirmed` olayi
            // yaziliyor ve musteri iptal ettigi siparis icin fatura + sadakat puani aliyordu.
            //
            // ARTIK: gecis makineden sorulur. Terminal ise DURUM YAZILMAZ; buna karsilik
            //   - `payments` satiri Success olarak KAYDEDILIR (para gercekten alindi, izi kalir),
            //   - `PaymentAfterTerminal` olayi **Critical** olarak yazilir.
            // PARA IADESI OTOMATIK DEGIL - BILINEN KALEM (elle iade, GF-7). Otomatik iade bu
            // dalgada ACILMADI: iade yolu `RefundManager` uzerinden gider ve o kapsam disidir.
            bool terminalAtlandi = false;
            bool kazandi;
            try
            {
                kazandi = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var gecis = await _paymentDal.TryTransitionStatusAsync(payment.id,
                        (byte)PaymentStatusEnum.Pending,
                        odemeGecerli ? (byte)PaymentStatusEnum.Success : (byte)PaymentStatusEnum.Failed);
                    if (gecis == 0)
                        return false;   // baska bir cagri kazandi - hicbir yan etki uygulanmaz

                    if (odemeGecerli)
                    {
                        payment.payment_status = (byte)PaymentStatusEnum.Success;
                        payment.paid_price = result.PaidPrice;
                        // Aciklayici yorum: TAKSIT - secilen taksit + komisyon. Komisyon = PaidPrice - amountDue (karta cekilen tutar).
                        // ONCEDEN taban order.total_price idi -> store_credit kullanildiginda (amountDue < total) komisyon YANLIS/0 cikiyordu.
                        payment.installment_count = (byte)result.Installment;
                        payment.installment_fee = result.PaidPrice > payment.amount ? result.PaidPrice - payment.amount : 0m;
                        order.installment_count = (byte)result.Installment;
                        payment.currency = result.Currency;
                        payment.fraud_status = result.FraudStatus;
                        payment.transaction_id = result.PaymentId;
                        // E2b: IADE bu kimlikle yapilir (paymentId ile DEGIL) - olculdu.
                        payment.item_transaction_id = result.ItemTransactionId;
                        payment.paid_at = DateTime.Now;
                        await _paymentDal.UpdateAsync(payment);

                        // GF-6 / K5 (D5): TERMINAL KAPISI - gerekce yukarida, `terminalAtlandi`nin
                        // taniminda. Odeme satiri YUKARIDA zaten kaydedildi; buradan sonrasi
                        // (durum, stok, olay) YALNIZ gecis gecerliyse kosar.
                        if (!OrderStatusMachine.IsValidTransition(
                                (OrderStatusEnum)order.status, OrderStatusEnum.Confirmed))
                        {
                            terminalAtlandi = true;
                            await _securityEvents.LogAsync("PaymentAfterTerminal", "Critical",
                                order.customer_id, null, null,
                                "ODEME BASARILI ama siparis TERMINAL durumda - siparis durumu YAZILMADI. "
                                + $"order={order.id} order_status={order.status} payment={payment.id}. "
                                + "IADE ELLE YAPILIR.");
                            return true;
                        }

                        order.status = (byte)OrderStatusEnum.Confirmed;
                        // Açıklayıcı yorum: Ödeme başarılı - rezervasyonu gerçek stok düşümüne çevir
                        await _stockService.ConfirmReservation(order.id);
                        order.is_online_payment_done = true;
                        order.payment_id = result.PaymentId;
                        await _orderDal.UpdateAsync(order);

                        // DALGA-2-FIX (B10): KUPON KULLANIM SATIRI BURADAN KALDIRILDI.
                        // Satiri yazan tek yer artik PaymentConfirmedSideEffects'in 4. adimi - yani
                        // TUM onay yollari icin AYNI yazici. Burada birakilsaydi kart yolu satiri
                        // yazar, kart disi yollar yazmazdi; bu dalganin duzeltmeye calistigi kusur
                        // tam olarak buydu. Gerekce ve bedeli o adimin basinda yazili.

                        // Açıklayıcı yorum: Zaman çizelgesine "Onaylandı" kaydı (ödeme başarılı - transaction içinde)
                        await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Ödeme onaylandı");

                        // SPRINT 8 MADDE 3 - YAN ETKILER OUTBOX'A TASINDI.
                        // Olay TRANSACTION ICINDE yazilir: "odeme Success oldu ama olay kaybedildi"
                        // durumu OLUSAMAZ. Onceden dort adim (fatura, sadakat, referans, kupon
                        // sayaci) commit SONRASI best-effort kosuyordu; patlarlarsa adiyla loglanip
                        // zaman cizelgesine not dusuluyor ama HIC YENIDEN DENENMIYORDU - gecici bir
                        // aksaklik yan etkiyi KALICI OLARAK kaybettiriyordu.
                        // Bedeli: eventual consistency (~1 dk, Cron.Minutely). Musteri siparis
                        // onayini ANINDA gorur; fatura/puan bir dakikaya kadar gecikebilir.
                        await _outboxService.WriteAsync("PaymentConfirmed", new PaymentConfirmedEvent
                        {
                            order_id = order.id,
                            customer_id = order.customer_id,
                            total_price = order.total_price,
                            coupon_code = order.coupon_code,
                            discount_amount = order.discount_amount
                        });
                    }
                    else
                    {
                        // Açıklayıcı yorum: Başarısız/tutar uyuşmuyor/fraud - sipariş iptal + stok iade
                        payment.payment_status = (byte)PaymentStatusEnum.Failed;
                        payment.paid_price = result.PaidPrice;
                        payment.fraud_status = result.FraudStatus;
                        await _paymentDal.UpdateAsync(payment);

                        // GF-6 / K5 (D5): TERMINAL KAPISI bu dalda da gecerlidir. Ozellikle
                        // `Delivered` bir siparis, gecikmis bir BASARISIZ callback ile
                        // `Cancelled`a CEKILEMEZ - mal fiziksel olarak teslim edilmistir;
                        // rezervasyon iadesi ve cuzdan iadesi de o durumda YANLIS olurdu.
                        if (!OrderStatusMachine.IsValidTransition(
                                (OrderStatusEnum)order.status, OrderStatusEnum.Cancelled))
                        {
                            terminalAtlandi = true;
                            await _securityEvents.LogAsync("PaymentAfterTerminal", "Critical",
                                order.customer_id, null, null,
                                "ODEME BASARISIZ ama siparis TERMINAL durumda - iptal YAZILMADI. "
                                + $"order={order.id} order_status={order.status} payment={payment.id}.");
                            return true;
                        }

                        order.status = (byte)OrderStatusEnum.Cancelled;
                        await _orderDal.UpdateAsync(order);

                        // Açıklayıcı yorum: Ödeme başarısız - stok DÜŞMEDİĞİ için (rezerve edilmişti) rezervasyonu
                        // serbest bırak - fiziksel stok hiç azalmamıştı (stok artırma değil, rezerve iadesi).
                        await _stockService.ReleaseReservation(order.id);

                        // CÜZDAN İADESİ - siparişte kullanılan mağaza kredisi geri verilir (sipariş iptal edildi).
                        // Bakiye artisi ile defter kaydi ARTIK GERCEKTEN atomik (S7): oncesinde ambient
                        // transaction olmadigi icin ikisi ayri ayri kaliciliyordu - arada bir cokme
                        // "bakiye artti ama defterde iz yok" ayrismasi birakirdi.
                        if (order.store_credit_used > 0)
                        {
                            await _customerDal.IncrementStoreCreditAsync(order.customer_id, order.store_credit_used);
                            await _creditTxDal.AddAsync(new StoreCreditTransaction
                            {
                                customer_id = order.customer_id,
                                amount = order.store_credit_used,
                                type = (byte)LedgerEntryTypeEnum.Earn,
                                reason = "Ödeme başarısız - kredi iadesi",
                                order_id = order.id,
                                created_at = DateTime.Now
                            });
                        }
                    }
                    return true;
                });
            }
            catch (Exception)
            {
                // Transaction KENDI icinde rollback etti (ExecuteInTransactionAsync catch -> Rollback -> throw).
                // Durum gecisi de geri alindi: odeme Pending kaldi, tekrar denenebilir.
                return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.PaymentProcessingError));
            }

            if (!kazandi)
                return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentAlreadyProcessed));

            // ══ B BOLGESI - COMMIT SONRASI YAN ETKILER ══════════════════════════════════════
            // Bunlar transaction'a ALINAMAZ: fatura dis saglayiciya HTTP yapar, loyalty/referral
            // kendi transaction'larini acar. Bu yuzden commit SONRASI ve "best-effort" kalirlar -
            // ama artik SESSIZ degiller: her adim ayri ayri sarilir, patlayan adim ADIYLA loglanir
            // ve siparis zaman cizelgesine operatorun gorecegi bir not dusulur. Onceden bunlar
            // ciplak "catch { }" idi: puan/referans/kupon sayaci sessizce dusuyordu.
            // ══ GF-6 / F2 (S-1) - PARA ALINDI AMA SIPARIS IPTALDI: UCUNCU DURUM ═══════════════
            //
            // OLCULEN ONCE-DURUM (L3 denetcisi buldu, ana akis kendi komutuyla dogruladi): bu
            // kontrol asagidaki `if (odemeGecerli)` blogunun ALTINDA duruyordu, dolayisiyla
            // `terminalAtlandi` bayragi BASARILI dalda hicbir yaniti degistirmiyordu. Musteri,
            // IPTAL KALAN bir siparis icin "Odeme basarili, siparisiniz onaylandi." mesajini ve
            // `status=success` sonuc ekranini goruyordu. Bu tutarsizlik GF-6'nin ACTIGI bir
            // yuzeydi: K5 oncesinde siparis `Confirmed`a DIRILIYORDU, yani 200 "tutarli"ydi.
            //
            // UCUNCU DURUM SECILDI (merkez karari, secenek a): ne "basarili" ne "basarisiz".
            // ODEME GERCEKTEN ALINDI - 400 donmek musteriye "paran cekilmedi" dedirtirdi ve
            // iade talebini geciktirirdi. Yanit 200 KALIR, MESAJ ayrisir; `PaymentController`
            // bu mesaji taniyip `status=review` ile yonlendirir (`success` DEGIL).
            //
            // SIRA KRITIK: bu kontrol `odemeGecerli` blogunun USTUNDE durmali - altinda
            // duran hali, duzeltilen kusurun ta kendisiydi.
            if (terminalAtlandi && odemeGecerli)
                return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentReceivedOrderCancelled));

            if (odemeGecerli)
            {
                // SPRINT 8 MADDE 3: DORT YAN ETKI BURADAN KALKTI.
                // Fatura / sadakat puani / referans odulu / kupon sayaci artik A bolgesindeki
                // transaction icinde yazilan "PaymentConfirmed" outbox mesajiyla, OutboxProcessor
                // tarafindan uygulaniyor (bkz. PaymentConfirmedSideEffects).
                // KAZANC: adimlar artik YENIDEN DENENIYOR - gecici bir aksaklik yan etkiyi kalici
                // olarak kaybettirmiyor. 5 denemede de basarisiz olursa mesaj Failed olur ve
                // GURULTULU kalir (zaman cizelgesine "KRITIK" notu + log).
                // BEDEL: eventual consistency (~1 dk). Musteri siparis onayini ANINDA gorur.
                return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentSuccess));
            }

            // GF-6 / K5 (D5): terminal kapisi atesledigi dalda siparis durumu DEGISMEDI, yani
            // ne fatura iptali ne de baska bir iptal yan etkisi kosmalidir - `Delivered` bir
            // siparisin faturasini iptal etmek, duzeltmeye calistigimiz hasarin AYNISI olurdu.
            if (terminalAtlandi)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PaymentFailed));

            // IPTAL YAN ETKILERI - siparis Cancelled olarak KALICI olduktan SONRA.
            // Bu dalda artik fatura KESILMIYOR (onay dalina tasindi), ama cagri yine de yapilir:
            // baska bir onay yolundan (COD/havale/magaza kredisi) fatura kesilmis bir siparis bu
            // callback ile iptal olabilir - o fatura acikta KALMAMALI. CancelForOrder idempotent.
            await YanEtkiUygulaAsync(order.id, "fatura iptali",
                () => _orderConfirmation.ApplyCancelledSideEffectsAsync(order.id));

            var msg = !amountMatches ? Messages.PaymentAmountMismatch
                    : !currencyOk ? Messages.PaymentCurrencyMismatch
                    : !fraudOk ? Messages.PaymentFraudReject
                    : Messages.PaymentFailed;
            return (HttpStatusCode.BadRequest, new ErrorResult(msg));
        }

        // COMMIT SONRASI YAN ETKI SARMALAYICI (S7).
        // Sozlesme: bu adimlar odemeyi BOZMAZ (para gercekten alindi, geri alinmaz) ama SESSIZ de
        // kalmaz. Patlayan adim adiyla loglanir VE siparis zaman cizelgesine not dusulur - operator
        // hangi yan etkinin eksik kaldigini panelde gorebilsin (OrderManager'daki "KRITIK: para
        // iadesi BASARISIZ" notuyla ayni kalip). Not yazmanin kendisi de patlarsa log tek kanal kalir.
        private async Task YanEtkiUygulaAsync(int orderId, string adim, Func<Task> islem)
        {
            try
            {
                await islem();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Odeme sonrasi yan etki BASARISIZ. adim={Adim} orderId={OrderId}", adim, orderId);
                try
                {
                    await _statusHistory.RecordAsync(orderId, (byte)OrderStatusEnum.Confirmed,
                        $"UYARI: '{adim}' adimi basarisiz - manuel kontrol gerekli");
                }
                catch (Exception izEx)
                {
                    _logger.LogError(izEx,
                        "Yan etki hatasi zaman cizelgesine yazilamadi. adim={Adim} orderId={OrderId}", adim, orderId);
                }
            }
        }
        // DALGA-2-FIX (B10): buradaki `SyncCouponUsageCountAsync` OLU METOTTU - Sprint 8 madde 3'te
        // sayac outbox isleyicisine tasinmis ama metot ve yorumu geride kalmisti (tarandi: hicbir
        // cagrisi yoktu). Ustelik yorumu "kullanim satiri A bolgesindeki transaction icinde
        // yazilir" diyordu; bu dalga satiri isleyiciye tasidigi icin o cumle ARTIK YANLIS olacakti
        // ve yalan soyleyen bir yorum olu koddan daha zararlidir. Sprint 8 madde 1'in gerekcesi
        // ZATEN yasadigi yerde duruyor: EfCouponDal.SyncUsedCountAsync uzerindeki blok.

        // SPRINT 8 MADDE 8 - TESLIM EDILEMEZ UST ALAN ADLARI.
        // RFC 2606 / RFC 6761 bu adlari TEST ve OZEL kullanim icin AYIRMISTIR; internette
        // hicbir zaman gercek bir posta kutusuna cozulemezler. ".local" ise mDNS icin ayrildi.
        // Yani bu adreslerdeki bir musteri e-postayi HIC alamaz - odeme saglayicisinin
        // reddetmesi de bu yuzden dogru davranistir.
        //
        // KAPSAM BILINCLI OLARAK DAR: burasi YALNIZ odeme init'i basarisiz olduktan SONRA,
        // musteriye DOGRU sebebi soylemek icin kullanilir. KAYIT validatorune DOKUNULMADI -
        // kayit kurallarini sikilastirmak ayri bir urun karari (rapora "SUPHELI" olarak yazildi):
        // gecerli ama alisilmadik adresleri reddetmek gercek musteriyi kapida cevirebilir.
        private static readonly string[] TeslimEdilemezUstAlanlar =
            { ".test", ".example", ".invalid", ".localhost", ".local" };

        private static bool TeslimEdilemezEposta(string eposta)
        {
            if (string.IsNullOrWhiteSpace(eposta)) return false;
            var e = eposta.Trim().ToLowerInvariant();
            var at = e.LastIndexOf('@');
            if (at < 0 || at == e.Length - 1) return false;
            var alan = e.Substring(at + 1);
            return TeslimEdilemezUstAlanlar.Any(son => alan.EndsWith(son, StringComparison.Ordinal));
        }
    }
}
