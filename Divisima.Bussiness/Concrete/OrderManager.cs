using System.Net;
using System.Linq;
using Divisima.Core.Integrations.Iyzico;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Bussiness.Events;
using Divisima.Bussiness.Outbox;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Locking;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Shipping;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Notifications;
using Divisima.Core.Utilities.Notifications;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Order;
using Divisima.Entity.Entities;

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
        private readonly IOrderNotificationService _orderNotificationService;
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly IMapper _mapper;
        private readonly IDistributedLock _distributedLock;
        private readonly IOrderConfirmationService _orderConfirmation;
        

        // Açıklayıcı yorum: Frontend sabitleri (FREE_SHIP=2000, kargo 49.9)
        private const decimal FreeShipThreshold = 2000m;
        private readonly IOrderPlacedEventPublisher _eventPublisher;

        private const decimal ShippingCost = 49.9m;

        public OrderManager(
            IOrderDal orderDal, IOrderItemDal orderItemDal,
            IOrderSnapshotDal orderSnapshotDal, IOrderSnapshotItemDal orderSnapshotItemDal,
            IProductDal productDal, ICustomerDal customerDal, ICouponDal couponDal,
            IStockService stockService, IUnitOfWork unitOfWork,
            IInvoiceService invoiceService, IOrderNotificationService orderNotificationService,
            IOrderStatusHistoryService statusHistory, IMapper mapper,
            IStoreCreditTransactionDal creditTxDal, IAddressDal addressDal,
            IPaymentDal paymentDal, IIyzicoClient iyzico, IRefundService refundService, ILoyaltyService loyaltyService,
            IDistributedLock distributedLock, IOrderPlacedEventPublisher eventPublisher,
            IOrderConfirmationService orderConfirmation)
        {
            _eventPublisher = eventPublisher;
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

            // Açıklayıcı yorum: Idempotency - aynı request_id ikinci kez sipariş üretmez (WebOrder kalıbı)
            if (!string.IsNullOrWhiteSpace(dto.request_id))
            {
                var duplicate = await _orderDal.GetAsync(o => o.request_id == dto.request_id);
                if (duplicate != null)
                    return (HttpStatusCode.OK, new SuccessDataResult<int>(duplicate.id, Messages.OrderAlreadyPlaced));
            }

            // Açıklayıcı yorum: ADRES SAHİPLİK KONTROLÜ (IDOR engeli) - address_id verildiyse müşteriye AİT olmalı.
            // Aksi halde başkasının kayıtlı adresine sipariş verilebilir / adres bilgisi sızabilirdi.
            if (dto.address_id.HasValue)
            {
                var addr = await _addressDal.GetAsync(a => a.id == dto.address_id.Value);
                if (addr == null || addr.customer_id != dto.customer_id)
                    return (HttpStatusCode.Forbidden, new ErrorResult(Messages.OrderInvalidAddress));
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
                // Geçersizse kupon UYGULANMAZ (indirim 0) - mevcut min_amount sessiz-yok-sayma davranışıyla tutarlı.
                bool couponValid = coupon != null
                    && subtotal >= coupon.min_amount
                    && !(coupon.expire_date.HasValue && coupon.expire_date.Value < DateTime.Now);

                // Açıklayıcı yorum: İlk-sipariş kuponu - tamamlanmış (Pending/Cancelled dışı) siparişi olan müşteri kullanamaz
                if (couponValid && coupon.first_order_only)
                {
                    // PERFORMANS (H51): EXISTS - satirlari cekmeden "hic tamamlanmis siparisi var mi" sorar.
                    var hasCompleted = await _orderDal.AnyAsync(o =>
                        o.customer_id == dto.customer_id && PaidOrderSpec.PaidStatuses.Contains(o.status));   // H52: merkezi kural
                    if (hasCompleted) couponValid = false;
                }

                // Açıklayıcı yorum: KULLANICI-BAŞI LİMİT - bu müşteri bu kuponu kaç kez kullandı (iptal olmayan siparişlerde).
                // Aksi halde tek-kullanımlık promo kuponu bir kullanıcı tarafından global limite kadar defalarca kullanılırdı.
                if (couponValid && coupon.per_user_limit > 0)
                {
                    // KISI-BASI LIMIT FIX (H51): sayim yalniz "!= Cancelled" idi -> ODENMEMIS (Pending) siparis de
                    // musterinin kupon hakkini TUKETIYORDU. Odemesi yarida kalan/basarisiz olan musteri, per_user_limit=1
                    // ise kuponunu KALICI olarak kaybediyordu. Global limitte (H50) duzeltilen kuralin KARDES kopyasiydi.
                    // Ayni kural: odenmis siparisler + hala TAZE bekleyen odeme (devam eden checkout).
                    var userPendingGrace = DateTime.Now.AddMinutes(-PaidOrderSpec.PendingGraceMinutes);
                    var usedByUser = await _orderDal.CountAsync(o =>
                        o.customer_id == dto.customer_id && o.coupon_code == coupon.code &&
                        (PaidOrderSpec.PaidStatuses.Contains(o.status)
                         || (o.status == (byte)OrderStatusEnum.Pending && o.created_at >= userPendingGrace)));
                    if (usedByUser >= coupon.per_user_limit) couponValid = false;
                }

                // Açıklayıcı yorum: GLOBAL KULLANIM LİMİTİ - used_count yerine SİPARİŞ SAYISI ile denetlenir (per_user_limit gibi).
                // KRİTİK 2 hata: (1) used_count YALNIZCA kart-ödeme yolunda artıyordu -> store-credit/COD ile aynı kupon
                // global limiti aşacak şekilde SINIRSIZ kullanılabiliyordu (usage_limit baypası). (2) İptal edilince used_count
                // düşmüyordu -> iptal edilen siparişler limiti KALICI şişiriyor, kupon gerçek kullanım olmadan tükeniyordu.
                // İptal-olmayan siparişleri global sayarak: TÜM ödeme yöntemleri sayılır + iptaller otomatik düşülür (ikisi de çözülür).
                if (couponValid && coupon.usage_limit > 0)
                {
                    // KAMPANYA SABOTAJI FIX (H50): sayim YALNIZ "!= Cancelled" idi -> ODENMEMIS (Pending)
                    // siparisler de limiti tuketiyordu. Saldirgan usage_limit kadar siparis acip HIC ODEMEZ ->
                    // kupon herkese KALICI olarak kapanir (kampanya sabotaji). Ayni dosyadaki kisi-basi kontrol
                    // zaten "!= Pending && != Cancelled" diyordu - tutarsizdi.
                    // Yeni kural: ODENMIS siparisler (PaidOrderSpec) + hala TAZE bekleyen odemeler (devam eden
                    // checkout'lar sayilmali ki limit asilmasin). Bayat Pending'ler artik limiti tutmaz.
                    var pendingGrace = DateTime.Now.AddMinutes(-PaidOrderSpec.PendingGraceMinutes);
                    // PERFORMANS (H51): COUNT(*) - populer kuponda 50.000 siparisi belleğe cekmek yerine tek sayi.
                    var globalUses = await _orderDal.CountAsync(o =>
                        o.coupon_code == coupon.code &&
                        (PaidOrderSpec.PaidStatuses.Contains(o.status)
                         || (o.status == (byte)OrderStatusEnum.Pending && o.created_at >= pendingGrace)));
                    if (globalUses >= coupon.usage_limit) couponValid = false;
                }

                if (couponValid)
                {
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
                await CreateSnapshotAsync(order, lineData);

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
                        customer_id = dto.customer_id, amount = creditToApply, type = (byte)LedgerEntryTypeEnum.Redeem,
                        reason = "Sipariş ödemesi (mağaza kredisi)", order_id = order.id, created_at = DateTime.Now
                    });
                }

                // Açıklayıcı yorum: 7d) CÜZDAN SİPARİŞİ TAM KAPATTIYSA online ödeme gerekmez - hemen onayla + rezervasyonu
                // gerçek stok düşümüne çevir (aksi halde 0 tutarlı Iyzico çağrısı yapılamaz).
                if (total - creditToApply <= 0)
                {
                    order.status = (byte)OrderStatusEnum.Confirmed;
                    order.is_online_payment_done = true;
                    await _orderDal.UpdateAsync(order);
                    await _stockService.ConfirmReservation(order.id);
                    await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Mağaza kredisi ile ödendi");
                }
                else if (isCod)
                {
                    // Açıklayıcı yorum: KAPIDA ÖDEME - online ödeme beklenmez; sipariş onaylanır, ödeme teslimatta alınır.
                    // Stok hemen satışa çevrilir (rezervasyon -> gerçek düşüm). is_online_payment_done=false kalır (nakit).
                    order.status = (byte)OrderStatusEnum.Confirmed;
                    await _orderDal.UpdateAsync(order);
                    await _stockService.ConfirmReservation(order.id);
                    await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Kapıda ödeme - sipariş onaylandı");
                }

                await _unitOfWork.CommitAsync();
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
                    var winner = await _orderDal.GetAsync(o => o.request_id == dto.request_id);
                    if (winner != null)
                        return (HttpStatusCode.OK, new SuccessDataResult<int>(winner.id, Messages.OrderAlreadyPlaced));
                }
                return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.OrderPlaceFailed));
            }

            // Açıklayıcı yorum: 8) Event publish (commit SONRASI - rollback olduysa yayınlanmaz)
            await _eventPublisher.PublishAsync(new OrderPlacedEvent
            {
                order_id = order.id,
                customer_id = order.customer_id,
                order_number = order.order_number,
                total = order.total_price
            });

            return (HttpStatusCode.Created, new SuccessDataResult<int>(order.id, Messages.OrderPlaced));
        }

        // Açıklayıcı yorum: Snapshot + snapshot kalemleri (Cafixo OrderSnapshot zinciri, iki timestamp)
        private async Task CreateSnapshotAsync(Order order,
            List<(Product product, string size, int qty, decimal unitPrice)> lineData)
        {
            var customer = await _customerDal.GetAsync(c => c.id == order.customer_id);
            var snapshot = new OrderSnapshot
            {
                order_id = order.id,
                customer_id = order.customer_id,
                customer_full_name = customer != null ? customer.name : "",
                shipping_address = null,
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

        private string GenerateOrderNumber()
        {
            return "DVS" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
        }



        // Açıklayıcı yorum: FATURA (HTML) - müşteri görüntüler/yazdırır (tarayıcı PDF'e çevirir; PDF lib gerekmez).
        // Sahiplik kontrollü (IDOR yok). KDV %20 dahil varsayımıyla matrah/KDV ayrıştırılır.
        public async Task<(HttpStatusCode, Result)> GetInvoiceHtml(int orderId, int customerId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            if (order.customer_id != customerId)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.OrderAccessDenied));

            var items = await _orderItemDal.GetListAsync(i => i.order_id == order.id && !i.is_cancelled);
            var productIds = items.Select(i => i.product_id).Distinct().ToList();
            var names = (await _productDal.GetListAsync(p => productIds.Contains(p.id))).ToDictionary(p => p.id, p => p.name);

            decimal matrah = Math.Round(order.total_price / 1.20m, 2);
            decimal kdv = order.total_price - matrah;

            var rows = new System.Text.StringBuilder();
            foreach (var it in items)
            {
                var pname = System.Net.WebUtility.HtmlEncode(names.TryGetValue(it.product_id, out var n) ? n : "Ürün");
                var size = System.Net.WebUtility.HtmlEncode(it.size ?? "");
                rows.Append($"<tr><td>{pname}</td><td>{size}</td><td>{it.quantity}</td><td>{it.unit_price:N2} TL</td><td>{(it.unit_price * it.quantity):N2} TL</td></tr>");
            }

            var html = $@"<!DOCTYPE html><html lang=""tr""><head><meta charset=""utf-8"">
<title>Fatura {System.Net.WebUtility.HtmlEncode(order.order_number)}</title>
<style>body{{font-family:Arial,sans-serif;max-width:800px;margin:20px auto;color:#222}}
h1{{font-size:20px}}table{{width:100%;border-collapse:collapse;margin:16px 0}}
th,td{{border:1px solid #ddd;padding:8px;text-align:left;font-size:13px}}th{{background:#f5f5f5}}
.tot{{text-align:right;margin-top:8px}}.tot div{{margin:2px 0}}</style></head><body>
<h1>DIVISIMA - Fatura</h1>
<p>Sipariş No: <b>{System.Net.WebUtility.HtmlEncode(order.order_number)}</b><br>
Tarih: {order.created_at:dd.MM.yyyy}</p>
<table><thead><tr><th>Ürün</th><th>Beden</th><th>Adet</th><th>Birim Fiyat</th><th>Tutar</th></tr></thead>
<tbody>{rows}</tbody></table>
<div class=""tot"">
<div>Ara Toplam: {order.subtotal:N2} TL</div>
{(order.discount_amount > 0 ? $"<div>İndirim: -{order.discount_amount:N2} TL</div>" : "")}
<div>Kargo: {order.shipping_cost:N2} TL</div>
<div>Matrah: {matrah:N2} TL</div>
<div>KDV (%20): {kdv:N2} TL</div>
<div style=""font-size:16px""><b>Genel Toplam: {order.total_price:N2} TL</b></div>
{(order.store_credit_used > 0 ? $"<div style=\"margin-top:8px;border-top:1px solid #eee;padding-top:6px\">Mağaza kredisi ile ödenen: {order.store_credit_used:N2} TL</div><div>Kalan (kart/havale): {(order.total_price - order.store_credit_used):N2} TL</div>" : "")}
</div>
<p style=""font-size:11px;color:#888;margin-top:24px"">Bu belge bilgilendirme amaçlıdır. Resmi e-fatura ayrıca düzenlenir.</p>
</body></html>";
            return (HttpStatusCode.OK, new SuccessDataResult<string>(html));
        }

        // Açıklayıcı yorum: MANUEL ÖDEME ONAYI (admin) - Havale/EFT parası hesaba geçince sipariş onaylanır.
        // Rezervasyon gerçek stok düşümüne çevrilir (online ödemede callback'in yaptığını admin manuel yapar).
        // Sadece Pending + Havale(2) siparişlerde geçerli; idempotent (zaten onaylıysa tekrar işlemez).
        public async Task<(HttpStatusCode, Result)> ConfirmManualPayment(int orderId)
        {
            var order = await _orderDal.GetAsync(o => o.id == orderId);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            if (order.payment_type != 2)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ManualPaymentOnlyBankTransfer));
            if (order.status != (byte)OrderStatusEnum.Pending || order.is_online_payment_done)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.OrderAlreadyProcessed));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                order.status = (byte)OrderStatusEnum.Confirmed;
                order.is_online_payment_done = true;   // ödeme alındı (havale onaylandı)
                await _orderDal.UpdateAsync(order);
                await _stockService.ConfirmReservation(order.id);
                await _statusHistory.RecordAsync(order.id, (byte)OrderStatusEnum.Confirmed, "Havale/EFT ödemesi onaylandı");
                await _unitOfWork.CommitAsync();
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

            order.status = (byte)dto.order_status;
            // Açıklayıcı yorum: Teslim edildiğinde teslim zamanını kaydet (iade penceresi buradan sayılır, sipariş tarihinden değil).
            if (order.status == (byte)OrderStatusEnum.Delivered && !order.delivered_at.HasValue)
                order.delivered_at = DateTime.Now;
            await _orderDal.UpdateAsync(order);

            // Açıklayıcı yorum: Durum değişimini zaman çizelgesine kaydet (yalnızca gerçek değişimde)
            if (order.status != previousStatus)
                await _statusHistory.RecordAsync(order.id, order.status, $"Durum güncellendi: {((OrderStatusEnum)order.status)}");

            // Açıklayıcı yorum: Durum değimine göre yan etkiler (fatura + bildirim). Best-effort - başarısızlık
            // sipariş güncellemesini geri almaz (bildirim/fatura ikincil). Hata loglanır, akış devam eder.
            await HandleStatusSideEffects(order, previousStatus);

            return (HttpStatusCode.OK, new SuccessResult(Messages.OrderStatusChanged));
        }

        // Açıklayıcı yorum: Sipariş durumu değişince tetiklenen yan etkiler - fatura üretimi + müşteri bildirimi.
        private async Task HandleStatusSideEffects(Order order, byte previousStatus)
        {
            var newStatus = order.status;
            if (newStatus == previousStatus) return;

            try
            {
                // Açıklayıcı yorum: Onaylanınca fatura üret (idempotent - InvoiceManager tekrar üretmez)
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

            // Açıklayıcı yorum: Sayfalı sonuç (toplam sayı + sayfa bilgisi)
            var paged = new Divisima.Core.Utilities.Dtos.PagedResult<Divisima.Entity.Dtos.Order.AdminOrderListItemDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                Size = size
            };
            return (HttpStatusCode.OK, new SuccessDataResult<Divisima.Core.Utilities.Dtos.PagedResult<Divisima.Entity.Dtos.Order.AdminOrderListItemDto>>(paged));
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
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.OrderAccessDenied));

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
                        customer_id = customerId, amount = itemRefund, type = (byte)LedgerEntryTypeEnum.Earn,
                        reason = "Kısmi iptal iadesi", order_id = order.id, created_at = DateTime.Now
                    });
                    refundedTotal += itemRefund;
                }

                // Açıklayıcı yorum: Aktif (iptal edilmemiş) kalem kaldı mı - kalmadıysa tüm siparişi iptal et
                // PERFORMANS (H51): EXISTS - kalan kalem VAR MI (hepsini cekmeye gerek yok).
                var hasRemaining = await _orderItemDal.AnyAsync(i => i.order_id == orderId && !i.is_cancelled);
                if (!hasRemaining)
                {
                    order.status = (byte)OrderStatusEnum.Cancelled;
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
                            customer_id = customerId, amount = leftoverRefund, type = (byte)LedgerEntryTypeEnum.Earn,
                            reason = "Tam iptal - kalan (kargo) iadesi", order_id = order.id, created_at = DateTime.Now
                        });
                        refundedTotal += leftoverRefund;
                    }
                    order.total_price = 0m;
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
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.OrderAccessDenied));

            var (earliest, latest) = DeliveryEstimator.Estimate(order.created_at);
            var dto = new Divisima.Entity.Dtos.Order.EstimatedDeliveryDto
            {
                order_id = orderId, earliest = earliest, latest = latest
            };
            return (HttpStatusCode.OK, new SuccessDataResult<Divisima.Entity.Dtos.Order.EstimatedDeliveryDto>(dto));
        }

    }
}
