using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Stock;
using Divisima.Entity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// BEDEN NORMALIZASYONU (H48): tum stok islemleri ayni normalize edilmis bedeni kullanir.
namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Stok iş kuralları. Beden bazlı ProductStock üzerinden düş/artır + StockMovement kaydı.
    // Cafixo IngredientStock/StockMovement orkestrasyonunun Divisima (beden) karşılığı.
    public class StockManager : IStockService
    {
        private readonly IProductStockDal _productStockDal;
        private readonly IStockMovementDal _stockMovementDal;
        private readonly IStockReservationDal _stockReservationDal;
        private readonly IStockNotificationService _stockNotificationService;
        // SUPHELI #18: "odeme alindi ama stok yok" uyarisi hareket kaydinin YANINDA siparis zaman
        // cizelgesine de dusulur - operatorun panelde GORDUGU yer orasi (H53 kalibi).
        private readonly IOrderStatusHistoryService _statusHistory;
        private readonly ILogger<StockManager> _logger;

        // Açıklayıcı yorum: Rezervasyon süresi - sipariş verilip ödeme yapılmazsa bu süre sonunda stok serbest kalır
        private const int ReservationMinutes = 20;

        public StockManager(IProductStockDal productStockDal, IStockMovementDal stockMovementDal, IStockReservationDal stockReservationDal,
            IStockNotificationService stockNotificationService, IOrderStatusHistoryService statusHistory, ILogger<StockManager> logger)
        {
            _productStockDal = productStockDal;
            _stockMovementDal = stockMovementDal;
            _stockReservationDal = stockReservationDal;
            _stockNotificationService = stockNotificationService;
            _statusHistory = statusHistory;
            _logger = logger;
        }

        // Açıklayıcı yorum: Yeterli stok kontrolü (frontend addToCart stok guard)
        public async Task<bool> CheckStock(int productId, string size, int quantity)
        {
            size = (size ?? string.Empty).Trim();   // H48: beden normalizasyonu (bosluk kaynakli hayalet stok engeli)
            var stock = await _productStockDal.GetAsync(s => s.product_id == productId && s.size == size && s.is_active);
            // Açıklayıcı yorum: Müsait stok = fiziksel stok - rezerve (ödeme bekleyen) miktar
            return stock != null && (stock.stock_quantity - stock.reserved_quantity) >= quantity;
        }

        // Açıklayıcı yorum: Stok düş. Yetersizse hata döner (overselling engeli), yeterliyse düşer + Out hareketi.
        public async Task<(HttpStatusCode, Result)> DecreaseStock(int productId, string size, int quantity, int? referenceId)
        {
            size = (size ?? string.Empty).Trim();   // H48: beden normalizasyonu (bosluk kaynakli hayalet stok engeli)
            // Açıklayıcı yorum: Optimistic concurrency - eşzamanlı iki sipariş aynı stoğu düşerse
            // biri DbUpdateConcurrencyException alır; taze veriyle sınırlı sayıda yeniden dener.
            const int maxRetry = 3;
            for (int attempt = 0; attempt < maxRetry; attempt++)
            {
                var stock = await _productStockDal.GetAsync(s => s.product_id == productId && s.size == size && s.is_active);
                if (stock == null)
                    return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotFound));

                // Açıklayıcı yorum: Overselling engeli - stoktan fazla düşülemez (her denemede taze kontrol)
                if (stock.stock_quantity < quantity)
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.StockInsufficient));

                stock.stock_quantity -= quantity;
                stock.updated_at = DateTime.Now;

                try
                {
                    await _productStockDal.UpdateAsync(stock);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Açıklayıcı yorum: Başka bir işlem bu satırı değiştirdi - taze veriyle tekrar dene
                    if (attempt == maxRetry - 1)
                        return (HttpStatusCode.Conflict, new ErrorResult(Messages.StockConcurrencyConflict));
                    continue;
                }

                // Açıklayıcı yorum: Stok hareketi kaydı (Out)
                await _stockMovementDal.AddAsync(new StockMovement
                {
                    product_id = productId,
                    size = size,
                    movement_type = (byte)StockMovementType.Out,
                    quantity = quantity,
                    reference_id = referenceId,
                    note = "Sipariş - stok düşümü",
                    created_at = DateTime.Now
                });

                return (HttpStatusCode.OK, new SuccessResult(Messages.StockDecreased));
            }
            return (HttpStatusCode.Conflict, new ErrorResult(Messages.StockConcurrencyConflict));
        }

        // Açıklayıcı yorum: Stok artır (iade/iptal). In hareketi kaydeder.
        public async Task<(HttpStatusCode, Result)> IncreaseStock(int productId, string size, int quantity, int? referenceId)
        {
            size = (size ?? string.Empty).Trim();   // H48: beden normalizasyonu (bosluk kaynakli hayalet stok engeli)
            var stock = await _productStockDal.GetAsync(s => s.product_id == productId && s.size == size && s.is_active);
            if (stock == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotFound));

            // Açıklayıcı yorum: Bildirim için önceki müsait miktarı ölç (0 iken pozitife çıkarsa haber ver)
            var availableBefore = stock.stock_quantity - stock.reserved_quantity;

            // Concurrency DUZELTMESI: ATOMIK stok artisi (retry'siz += eszamanli stok isleminde exception firlatirdi).
            // availableBefore yukarida okundu (bildirim icin best-effort); asil artis atomik UPDATE ile.
            await _productStockDal.IncrementStockQuantityAsync(productId, size, quantity);

            await _stockMovementDal.AddAsync(new StockMovement
            {
                product_id = productId,
                size = size,
                movement_type = (byte)StockMovementType.In,
                quantity = quantity,
                reference_id = referenceId,
                note = "İade/iptal - stok girişi",
                created_at = DateTime.Now
            });

            // Açıklayıcı yorum: Stok 0'dan pozitife çıktıysa bekleyen abonelere "gelince haber ver" bildirimi
            var availableAfterIncrease = stock.stock_quantity - stock.reserved_quantity;
            if (availableBefore <= 0 && availableAfterIncrease > 0)
                await _stockNotificationService.NotifyBackInStock(productId, size);

            return (HttpStatusCode.OK, new SuccessResult(Messages.StockIncreased));
        }
        // Açıklayıcı yorum: REZERVE - sipariş verilince stok düşmez, rezerve edilir (ödeme penceresi).
        // Optimistic concurrency ile eşzamanlı rezervasyonlar güvenli. Müsait yoksa reddeder.
        public async Task<(HttpStatusCode, Result)> ReserveStock(int productId, string size, int quantity, int orderId)
        {
            size = (size ?? string.Empty).Trim();   // H48: beden normalizasyonu (bosluk kaynakli hayalet stok engeli)

            // ESKI DESEN (kaldirildi): oku -> bellekte artir -> row_version ile yaz -> catch -> 3 kez dene.
            // Ayni urun+bedene eszamanli siparislerde denemeler tukeniyordu ve 8 siparisin 7'si
            // 500 adet stok VARKEN "Stok guncelleme cakismasi" (409) aliyordu. Musteri bunu
            // "stok yok" diye okur - olcum D2 dalgasindaydi.
            //
            // YENI DESEN: tek atomik CAS. Kosul ve yazma ayni SQL UPDATE'inde oldugu icin
            // kontrol-yazma araligi yok, concurrency EXCEPTION uretilmez, cekisme satir
            // kilidiyle cozulur. Retry / 409 yolu tamamen ortadan kalkti.

            // Varlik kontrolu ayri ve TAKIPSIZ: "beden yok" (404) ile "musait degil" (400) ayrimi
            // korunsun. Takipsiz olmasi sart - CAS sonrasi bellekteki nesne bayat kalirdi ve ayni
            // DbContext'te sonraki bir SaveChanges'i haksiz yere concurrency hatasina dusururdu.
            if (!await _productStockDal.AnyAsync(s => s.product_id == productId && s.size == size && s.is_active))
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotFound));

            var reserved = await _productStockDal.TryReserveAsync(productId, size, quantity);
            if (reserved == 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.StockInsufficient));

            try
            {
                // Açıklayıcı yorum: Rezervasyon kaydı (süre dolunca job serbest bırakır)
                await _stockReservationDal.AddAsync(new StockReservation
                {
                    order_id = orderId,
                    product_id = productId,
                    size = size,
                    quantity = quantity,
                    status = (byte)ReservationStatusEnum.Active,
                    expires_at = DateTime.Now.AddMinutes(ReservationMinutes),
                    created_at = DateTime.Now
                });
            }
            catch
            {
                // TELAFI: sayac arttiktan sonra rezervasyon satiri yazilamazsa sayac SIZAR
                // (stok kimseye ait olmadan bloke kalir). Tek cagiran (OrderManager.PlaceOrder)
                // bu cagriyi zaten bir transaction icinde yapiyor ve rollback ikisini birden geri
                // alir; buradaki telafi transaction'siz bir cagiran icin savunmadir.
                // NOT: burada YENI transaction ACILMAZ - ayni DbContext'te zaten acik bir
                // transaction varken BeginTransaction istisna firlatir ve siparis akisini kirardi.
                await _productStockDal.ReleaseReservedAsync(productId, size, quantity);
                throw;
            }

            return (HttpStatusCode.OK, new SuccessResult(Messages.StockReserved));
        }

        // Açıklayıcı yorum: ONAYLA - ödeme başarılı; rezervasyonu gerçek stok düşümüne çevir (fiziksel -= , rezerve -=).
        //
        // ══ SUPHELI #18 DUZELTMESI - MINI DALGA 2 ═══════════════════════════════════════════
        //
        // ONCEKI HALI: sorgu YALNIZ `status == Active` rezervasyonlari getiriyordu. Asagidaki
        // "expire olmustu, stogu yeniden guvenceye al" telafi dali ise YALNIZ TryTransitionAsync
        // 0 dondugunde - yani expire islemi sorgu ILE gecis ARASINDA olustugunda - calisiyordu.
        // Rezervasyon sorgu anINDA ZATEN Expired ise dongu HIC donmuyor, telafi dali OLU kaliyordu.
        // CANLI OLCUM (siparis #33 kurtarmasi): odeme Success, siparis Confirmed, fatura kesildi,
        // puan yazildi - ama product_stocks DEGISMEDI ve stock_movements'a TEK SATIR bile
        // yazilmadi. Yani envanter SESSIZCE sisti; kimsenin gorebilecegi bir iz yoktu.
        //
        // ── SINIR OLCEREK CIZILDI: hangi durumlar DAHIL, hangileri DEGIL ────────────────────
        // Rezervasyon durum gecisleri okundu (TryReserve / ConfirmReservation / ReleaseReservation
        // / ReleaseExpiredReservations) ve her durum icin FIZIKSEL stok hali cikarildi:
        //
        //   Active(0)    reserved TUTULUYOR, stock_quantity DUSMEMIS
        //                -> onayda: atomik gecis + ConfirmStockAsync (reserved -= , stock -=)
        //   Confirmed(1) reserved SERBEST, stock_quantity ZATEN DUSMUS
        //                -> DOKUNULMAZ. Tekrar dusmek CIFT DUSUM olurdu.
        //   Expired(3)   reserved SERBEST (cleanup job birakti), stock_quantity DUSMEMIS
        //                -> DAHIL. Siparis hala mesru sekilde onaylanabilir (terk edilmis sepet
        //                   degil, gecikmis bildirim). Dogru islem: DOGRUDAN dusum.
        //   Released(2)  reserved SERBEST, stock_quantity DUSMEMIS  ── FIZIKSEL OLARAK Expired ILE AYNI
        //                -> **DAHIL EDILMEDI.** Gerekce FIZIKSEL DEGIL ANLAMSAL:
        //                   `Released`i YALNIZCA ReleaseReservation yaziyor ve o da yalniz iki
        //                   yerden cagriliyor - IyzicoPaymentManager'in odeme BASARISIZ dali ve
        //                   OrderManager'in siparis IPTAL yolu. Yani `Released` = "bu siparis
        //                   IPTAL EDILDI" karari. Boyle bir rezervasyonun onaya gelmesi bir stok
        //                   kurtarma senaryosu DEGIL, bir DURUM MAKINESI IHLALIDIR. Stogu orada
        //                   dusmek (a) kimsenin sevk etmeyecegi bir siparis icin hayalet kayip
        //                   yazar, (b) asil hatayi - iptal edilmis siparisin yeniden onaylanmasini -
        //                   SESSIZCE ortbas eder. Bu yuzden Released onaya DAHIL EDILMEZ.
        //                   NOT: burada risk "cift dusum" DEGIL - ReleaseReservedAsync yalniz
        //                   reserved_quantity'yi azaltir, fiziksel stogu GERI EKLEMEZ (kodda da
        //                   "fiziksel degismez" diye yaziyor). Sinirin gerekcesi anlamsal.
        //
        // ── ATOMIKLIK: telafi dali da artik gecis KAZANMAYA bagli ──────────────────────────
        // Eski telafi dali `TryDirectDeductAsync` yapip rezervasyonu Expired BIRAKIYORDU. Sorgu
        // Expired'i hic getirmedigi icin bu gorunmuyordu; ama Expired ARTIK normal bir yol oldugu
        // icin ikinci bir ConfirmReservation cagrisi ayni satiri TEKRAR dusurebilirdi. Bu yuzden
        // her iki yol da Expired->Confirmed / Active->Confirmed ATOMIK gecisini KAZANMAK zorunda.
        public async Task<(HttpStatusCode, Result)> ConfirmReservation(int orderId)
        {
            var reservations = await _stockReservationDal.GetListAsync(r => r.order_id == orderId &&
                (r.status == (byte)ReservationStatusEnum.Active || r.status == (byte)ReservationStatusEnum.Expired));
            foreach (var res in reservations)
            {
                // Concurrency: rezervasyonu OKUNAN durumdan ATOMIK olarak Confirmed'a gecir;
                // YALNIZCA gecisi kazanan cagri stok islemini yapar. Iki eszamanli onay
                // (iki admin / callback+webhook) ayni rezervasyonu CIFT DUSEMEZ.
                var okunanDurum = res.status;
                var won = await _stockReservationDal.TryTransitionAsync(res.id,
                    okunanDurum, (byte)ReservationStatusEnum.Confirmed);
                if (won == 0)
                {
                    // Durum okuma ILE gecis ARASINDA degisti. Iki olasilik:
                    //  (a) Baska bir cagri zaten Confirmed yapti -> stok dusuldu, DOKUNMA.
                    //  (b) Expiry job araya girip Active->Expired yapti. Odeme BASARILI oldugu
                    //      icin stok hala GEREKLI: Expired'dan Confirmed'a gecisi dene.
                    var current = await _stockReservationDal.GetAsync(r => r.id == res.id);
                    if (current != null && current.status == (byte)ReservationStatusEnum.Expired)
                    {
                        var wonExpired = await _stockReservationDal.TryTransitionAsync(res.id,
                            (byte)ReservationStatusEnum.Expired, (byte)ReservationStatusEnum.Confirmed);
                        if (wonExpired == 0) continue;   // baska cagri kaptı
                        await ExpireSonrasiTelafiAsync(res, orderId);
                    }
                    continue;
                }

                if (okunanDurum == (byte)ReservationStatusEnum.Expired)
                {
                    // Rezerve ZATEN serbest birakilmisti; ConfirmStockAsync reserved'i de dusurur
                    // ve sayaci EKSIYE cekerdi. Dogru islem DOGRUDAN dusumdur.
                    await ExpireSonrasiTelafiAsync(res, orderId);
                    continue;
                }

                await _productStockDal.ConfirmStockAsync(res.product_id, res.size, res.quantity);
                await _stockMovementDal.AddAsync(new StockMovement
                {
                    product_id = res.product_id,
                    size = res.size,
                    movement_type = (byte)StockMovementType.Out,
                    quantity = res.quantity,
                    reference_id = orderId,
                    note = "Sipariş - ödeme onaylı stok düşümü",
                    created_at = DateTime.Now
                });
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.StockReservationConfirmed));
        }

        // SUPHELI #18 - EXPIRE OLMUS REZERVASYONUN TELAFISI.
        // Rezerve zaten serbest birakilmis, fiziksel stok DUSMEMIS. Odeme basarili oldugu icin
        // stok gereklidir: mevcutsa DOGRUDAN dusulur. Yetmiyorsa SESSIZ KALINMAZ - iki kanal:
        //   1) stock_movements notu (envanter defteri; MEVCUT DAVRANIS AYNEN KORUNDU)
        //   2) siparis zaman cizelgesi (operatorun panelde GORDUGU yer; H53 "KRITIK:" kalibi)
        // Ikinci kanal eklendi cunku hareket kaydini kimse duzenli okumuyor; #33'te zaten hicbir
        // satir yazilmamisti ve sapma aylarca gorunmeyebilirdi.
        private async Task ExpireSonrasiTelafiAsync(StockReservation res, int orderId)
        {
            var deducted = await _productStockDal.TryDirectDeductAsync(res.product_id, res.size, res.quantity);
            await _stockMovementDal.AddAsync(new StockMovement
            {
                product_id = res.product_id,
                size = res.size,
                movement_type = (byte)StockMovementType.Out,
                quantity = res.quantity,
                reference_id = orderId,
                note = deducted > 0
                    ? "Ödeme onaylı - rezervasyon expire olmuştu, stok yeniden güvenceye alındı"
                    : "UYARI: ödeme alındı fakat stok yok (rezervasyon expire + tükendi) - manuel iade/tedarik gerekli",
                created_at = DateTime.Now
            });

            if (deducted > 0) return;

            // Zaman cizelgesi BEST-EFFORT: not yazilamazsa onay akisi KIRILMAZ (hareket kaydi
            // birinci kanal olarak zaten yazildi). Durum olarak Confirmed veriliyor - bu metot
            // yalnizca onay yolundan cagriliyor (OutboxProcessor'daki ayni kalip).
            try
            {
                await _statusHistory.RecordAsync(orderId, (byte)OrderStatusEnum.Confirmed,
                    $"UYARI: ödeme alındı fakat stok yok (ürün {res.product_id}, beden {res.size}, " +
                    $"{res.quantity} adet) - rezervasyon süresi dolmuştu ve stok tükendi. " +
                    "Manuel iade veya tedarik gerekli.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "STOK UYARISI zaman cizelgesine yazilamadi. siparis={OrderId} urun={ProductId} beden={Size}",
                    orderId, res.product_id, res.size);
            }
        }

        // Açıklayıcı yorum: SERBEST BIRAK - ödeme başarısız/iptal; rezerveyi geri ver (fiziksel değişmez).
        public async Task<(HttpStatusCode, Result)> ReleaseReservation(int orderId)
        {
            var reservations = await _stockReservationDal.GetListAsync(r => r.order_id == orderId && r.status == (byte)ReservationStatusEnum.Active);
            foreach (var res in reservations)
            {
                // Concurrency: rezervasyonu ATOMIK Active->Released gecir; yalnizca kazanan rezerve serbest birakir
                // (cift-release ile reserved_quantity fazla dusmesi engellenir).
                var won = await _stockReservationDal.TryTransitionAsync(res.id,
                    (byte)ReservationStatusEnum.Active, (byte)ReservationStatusEnum.Released);
                if (won == 0) continue;
                await _productStockDal.ReleaseReservedAsync(res.product_id, res.size, res.quantity);
            }
            return (HttpStatusCode.OK, new SuccessResult(Messages.StockReservationReleased));
        }

        // Açıklayıcı yorum: SÜRESİ DOLANLARI SERBEST BIRAK - Hangfire job (terk edilen sepetler).
        public async Task<int> ReleaseExpiredReservations()
        {
            var expired = await _stockReservationDal.GetListAsync(r => r.status == (byte)ReservationStatusEnum.Active && r.expires_at < DateTime.Now);
            int released = 0;
            foreach (var res in expired)
            {
                // Concurrency: iki job/ornek ayni suresi-dolmus rezervasyonu ISLEYEMEZ (atomik gecis).
                var won = await _stockReservationDal.TryTransitionAsync(res.id,
                    (byte)ReservationStatusEnum.Active, (byte)ReservationStatusEnum.Expired);
                if (won == 0) continue;
                await _productStockDal.ReleaseReservedAsync(res.product_id, res.size, res.quantity);
                released++;
            }
            return released;
        }

        // Açıklayıcı yorum: ADMIN STOK DÜZELTME - mutlak yeni değer atar, farkı hareket olarak kaydeder.
        // Yeni sevkiyat (artış) veya sayım düzeltmesi (azalış). Not zorunlu (denetim izi).
        public async Task<(HttpStatusCode, Result)> AdjustStock(int productId, string size, int newQuantity, string note)
        {
            size = (size ?? string.Empty).Trim();   // H48: beden normalizasyonu (bosluk kaynakli hayalet stok engeli)
            if (newQuantity < 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.StockAdjustInvalid));

            var stock = await _productStockDal.GetAsync(s => s.product_id == productId && s.size == size && s.is_active);
            if (stock == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.StockNotFound));

            // Açıklayıcı yorum: Rezerve altına inilemez (rezerve edilmiş stoktan az yapılamaz)
            if (newQuantity < stock.reserved_quantity)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.StockAdjustBelowReserved));

            // Açıklayıcı yorum: Bildirim için önceki müsait miktar
            var availableBeforeAdjust = stock.stock_quantity - stock.reserved_quantity;

            var delta = newQuantity - stock.stock_quantity;
            stock.stock_quantity = newQuantity;
            stock.updated_at = DateTime.Now;
            await _productStockDal.UpdateAsync(stock);

            // ══ DALGA-2-FIX (B11) - YON BILGISI SERBEST METINDE YASAYAMAZ ═════════════════════
            //
            // ONCEKI HALI: `quantity = Math.Abs(delta)`. Yon YALNIZCA `note` icindeki
            // "Admin duzeltme (-5)" metninde duruyordu; sayisal defterde artis ile azalis
            // AYIRT EDILEMIYORDU.
            //
            // OLCULEN ZARAR (Dalga 2, dev veritabani, urun 2 / M):
            //     isaretsiz defter:  10 + 20 + 2 - 14 = 18      product_stocks: 8   FARK 10
            //     isaretli defter :  10 + 15 - 5 + 2 - 14 = 8   product_stocks: 8   TUTAR
            // `stock_movements` bir DENETIM defteridir; mutabakat yapan biri sessizce YANLIS
            // sayiya ulasiyordu.
            //
            // NEDEN YALNIZ ADJUSTMENT ISARETLENIYOR (olcume dayali karar):
            //   - In(1) ve Out(2) satirlarinin yonu ZATEN `movement_type` ile belirli; onlari da
            //     isaretlemek hicbir bilgi eklemez ve iki mevcut pini (Out quantity==3,
            //     In quantity==4) gerekcesiz sekilde kirardi.
            //   - Adjustment, yonu tipinden TURETILEMEYEN TEK tur - isaret tam oraya ait.
            //   - Sema degisikligi GEREKMEDI (yeni yon kolonu redundant olurdu).
            // MUTABAKAT FORMULU (tek ve uniform):
            //     SUM(CASE movement_type WHEN 2 THEN -quantity ELSE quantity END)
            //
            // TUKETICI RISKI OLCULDU: depoda `stock_movements` tablosunu OKUYAN uretim kodu YOK
            // (yalniz AddAsync cagrilari var; DTO/uc/rapor yok) - defter salt-yazilir bir denetim
            // izidir. Bu yuzden isaretleme hicbir okuyucuyu bozmuyor.
            await _stockMovementDal.AddAsync(new StockMovement
            {
                product_id = productId,
                size = size,
                movement_type = (byte)StockMovementType.Adjustment,
                quantity = delta,   // ISARETLI FARK - azalista NEGATIF
                reference_id = null,
                note = $"Admin düzeltme ({(delta >= 0 ? "+" : "")}{delta}): {note}",
                created_at = DateTime.Now
            });

            // Açıklayıcı yorum: Düzeltmeyle stok pozitife çıktıysa bekleyen abonelere bildir
            var availableAfterAdjust = newQuantity - stock.reserved_quantity;
            if (availableBeforeAdjust <= 0 && availableAfterAdjust > 0)
                await _stockNotificationService.NotifyBackInStock(productId, size);

            return (HttpStatusCode.OK, new SuccessResult(Messages.StockAdjusted));
        }

        // ADMIN STOK DETAYI (E4a): beden basina fiziksel + rezerve + satilabilir.
        // Rezerve edilmis adet fiziksel stokta DURUR ama satilamaz; operator yalniz stock_quantity
        // gorursa "10 var ama 3 satamiyorum" farkini anlayamaz. NoTracking - salt okuma.
        // Yalniz aktif stok satirlari (is_active) - pasifler urun yonetiminden gelir, stok ekraninda degil.
        public async Task<(HttpStatusCode, Result)> GetStockDetail(int productId)
        {
            var rows = await _productStockDal.GetListNoTrackingAsync(s => s.product_id == productId && s.is_active);
            var dtos = rows
                .OrderBy(s => s.size)
                .Select(s => new ProductStockDetailDto
                {
                    size = s.size,
                    stock_quantity = s.stock_quantity,
                    reserved_quantity = s.reserved_quantity,
                    available = s.stock_quantity - s.reserved_quantity
                })
                .ToList();

            // Bos liste HATA DEGIL: urunun henuz beden satiri olmayabilir. Cagiran ayirt edebilsin
            // diye 200 + bos dizi doner (404 "urun yok" ile karistirilirdi).
            return (HttpStatusCode.OK, new SuccessDataResult<List<ProductStockDetailDto>>(dtos));
        }
    }
}
