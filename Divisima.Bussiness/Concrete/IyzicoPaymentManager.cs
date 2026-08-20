using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Locking;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Sanitization;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Payment;
using Divisima.Entity.Entities;
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
        private readonly ICouponUsageDal _couponUsageDal;
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly ILoyaltyService _loyaltyService;
        private readonly IReferralService _referralService;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderConfirmationService _orderConfirmation;
        // Commit sonrasi yan etkiler artik SESSIZ dusmuyor - patlayan adim adiyla loglanir (S7).
        private readonly ILogger<IyzicoPaymentManager> _logger;

        public IyzicoPaymentManager(IPaymentDal paymentDal, IOrderDal orderDal,
            IIyzicoClient iyzico, IStockService stockService, ICustomerDal customerDal,
            IFraudCheckService fraudCheck, IDistributedLock distributedLock,
            ICouponDal couponDal, ICouponUsageDal couponUsageDal,
IOrderStatusHistoryService statusHistory,
ILoyaltyService loyaltyService,
IReferralService referralService, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork,
            IOrderConfirmationService orderConfirmation, ILogger<IyzicoPaymentManager> logger)
        {
            _logger = logger;
            _paymentDal = paymentDal;
            _orderDal = orderDal;
            _iyzico = iyzico;
            _stockService = stockService;
            _customerDal = customerDal;
            _fraudCheck = fraudCheck;
            _distributedLock = distributedLock;
            _couponDal = couponDal;
            _couponUsageDal = couponUsageDal;
            _statusHistory = statusHistory;
            _loyaltyService = loyaltyService;
            _referralService = referralService;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
            _orderConfirmation = orderConfirmation;
        }

        public async Task<(HttpStatusCode, Result)> Initialize(PaymentInitRequestDto dto, int authenticatedCustomerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == dto.order_id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorDataResult<PaymentInitResponseDto>(Messages.OrderNotFound));

            // Açıklayıcı yorum: SAHİPLİK KONTROLÜ - kullanıcı yalnızca KENDİ siparişini ödeyebilir (IDOR engeli)
            if (order.customer_id != authenticatedCustomerId)
                return (HttpStatusCode.Forbidden, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentNotYourOrder));

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

            var customer = await _customerDal.GetAsync(c => c.id == order.customer_id);
            var conversationId = Guid.NewGuid().ToString("N");

            // Açıklayıcı yorum: Checkout Form başlat - TUTAR SUNUCUDAN (order.total_price), client'tan değil
            var init = await _iyzico.InitializeCheckoutFormAsync(new IyzicoCheckoutInitRequest
            {
                ConversationId = conversationId,
                Price = amountDue,   // KALAN tutar (cuzdan dususu sonrasi)
                CallbackUrl = dto.callback_url ?? "",
                CustomerId = order.customer_id,
                BuyerName = customer?.name ?? "Musteri",
                BuyerEmail = customer?.email ?? ""
            });
            if (!init.Success)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<PaymentInitResponseDto>(Messages.PaymentInitFailed));

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

        public async Task<(HttpStatusCode, Result)> HandleCallback(PaymentCallbackRequestDto dto)
        {
            // Açıklayıcı yorum: 1) İMZA DOĞRULA - sahte callback'i en baştan ele
            if (!_iyzico.VerifyCallbackSignature(dto.token, dto.signature))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.PaymentSignatureInvalid));

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
            if (payment.created_at.AddMinutes(30) < DateTime.Now)
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
                        payment.paid_at = DateTime.Now;
                        await _paymentDal.UpdateAsync(payment);

                        order.status = (byte)OrderStatusEnum.Confirmed;
                        // Açıklayıcı yorum: Ödeme başarılı - rezervasyonu gerçek stok düşümüne çevir
                        await _stockService.ConfirmReservation(order.id);
                        order.is_online_payment_done = true;
                        order.payment_id = result.PaymentId;
                        await _orderDal.UpdateAsync(order);

                        // Açıklayıcı yorum: Kupon kullanımını KAYDET - ödeme başarılı olduğunda (kupon gerçekten tüketildi).
                        // used_count artırılır (limit denetimi anlamlı olur) + CouponUsage kaydı (kim/hangi sipariş).
                        if (!string.IsNullOrWhiteSpace(order.coupon_code))
                        {
                            var coupon = await _couponDal.GetByCodeAsync(order.coupon_code);
                            if (coupon != null)
                            {
                                // Açıklayıcı yorum: CouponUsage kaydı transaction içinde (insert - çakışma yok, denetim izi).
                                // used_count artışı post-commit retry ile yapılır (ödemeyi concurrency çakışması bozmasın).
                                await _couponUsageDal.AddAsync(new CouponUsage
                                {
                                    coupon_id = coupon.id,
                                    customer_id = order.customer_id,
                                    order_id = order.id,
                                    discount_applied = order.discount_amount,
                                    created_at = DateTime.Now
                                });
                            }
                        }

                        // Açıklayıcı yorum: Zaman çizelgesine "Onaylandı" kaydı (ödeme başarılı - transaction içinde)
                        await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Ödeme onaylandı");
                    }
                    else
                    {
                        // Açıklayıcı yorum: Başarısız/tutar uyuşmuyor/fraud - sipariş iptal + stok iade
                        payment.payment_status = (byte)PaymentStatusEnum.Failed;
                        payment.paid_price = result.PaidPrice;
                        payment.fraud_status = result.FraudStatus;
                        await _paymentDal.UpdateAsync(payment);

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
                                customer_id = order.customer_id, amount = order.store_credit_used,
                                type = (byte)LedgerEntryTypeEnum.Earn, reason = "Ödeme başarısız - kredi iadesi",
                                order_id = order.id, created_at = DateTime.Now
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
            if (odemeGecerli)
            {
                // FATURA - ODEME DOGRULANDIKTAN SONRA (S7). Onceden dogrulamadan ONCE cagriliyordu:
                // fraud reddi ile IPTAL edilen siparise de fatura kesiliyor ve saglayiciya "Sent"
                // olarak gidiyordu. Artik yalniz ONAYLANMIS siparise kesilir. Dort onay yolu (COD,
                // tam magaza kredisi, havale onayi, bu online callback) ayni cagriyi kullanir.
                await YanEtkiUygulaAsync(order.id, "fatura",
                    () => _orderConfirmation.ApplyConfirmedSideEffectsAsync(order.id));

                // Sadakat puani (kendi transaction'ini acar - bu yuzden commit SONRASI).
                await YanEtkiUygulaAsync(order.id, "sadakat puani",
                    () => _loyaltyService.EarnFromOrder(order.customer_id, order.total_price, order.id));

                // Referans odulu (davet edilen musterinin ilk siparisinde iki tarafa kredi).
                await YanEtkiUygulaAsync(order.id, "referans odulu",
                    () => _referralService.RewardOnFirstOrder(order.customer_id, order.id));

                // Kupon used_count artisi optimistic-concurrency retry ile.
                if (!string.IsNullOrWhiteSpace(order.coupon_code))
                    await YanEtkiUygulaAsync(order.id, "kupon sayaci",
                        () => IncrementCouponUsageWithRetry(order.coupon_code));

                return (HttpStatusCode.OK, new SuccessResult(Messages.PaymentSuccess));
            }

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
        // Açıklayıcı yorum: Kupon used_count'u optimistic concurrency ile güvenli artır.
        // Cafixo DAL her UpdateAsync'te SaveChanges yaptığından, çakışmada taze yükleyip yeniden deneriz.
        private async Task IncrementCouponUsageWithRetry(string couponCode)
        {
            const int maxRetry = 5;
            for (int attempt = 0; attempt < maxRetry; attempt++)
            {
                try
                {
                    var coupon = await _couponDal.GetByCodeAsync(couponCode);
                    if (coupon == null) return;
                    coupon.used_count += 1;
                    await _couponDal.UpdateAsync(coupon);
                    return; // başarılı
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
                {
                    // Açıklayıcı yorum: Başka bir ödeme aynı kuponu güncelledi - taze değerle yeniden dene
                    if (attempt == maxRetry - 1) return; // son deneme de başarısız - sessiz geç (soft limit)
                }
            }
        }

    }
}
