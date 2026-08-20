using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Microsoft.EntityFrameworkCore;
using Divisima.Entity.Dtos.Stock;
using Divisima.Entity.Entities;

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

        // Açıklayıcı yorum: Rezervasyon süresi - sipariş verilip ödeme yapılmazsa bu süre sonunda stok serbest kalır
        private const int ReservationMinutes = 20;

        public StockManager(IProductStockDal productStockDal, IStockMovementDal stockMovementDal, IStockReservationDal stockReservationDal,
            IStockNotificationService stockNotificationService)
        {
            _productStockDal = productStockDal;
            _stockMovementDal = stockMovementDal;
            _stockReservationDal = stockReservationDal;
            _stockNotificationService = stockNotificationService;
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
        public async Task<(HttpStatusCode, Result)> ConfirmReservation(int orderId)
        {
            var reservations = await _stockReservationDal.GetListAsync(r => r.order_id == orderId && r.status == (byte)ReservationStatusEnum.Active);
            foreach (var res in reservations)
            {
                // Concurrency DUZELTMESI: rezervasyonu ATOMIK olarak Active->Confirmed gecir; YALNIZCA bu cagri
                // gecisi kazanirsa stok duser. Iki eszamanlı onay (iki admin / callback+havale) ayni rezervasyonu
                // CIFT DUSEMEZ (online yolda distributed lock vardi; bu koruma artik release/expire dahil her yerde).
                var won = await _stockReservationDal.TryTransitionAsync(res.id,
                    (byte)ReservationStatusEnum.Active, (byte)ReservationStatusEnum.Confirmed);
                if (won == 0)
                {
                    // Rezervasyon Active DEGIL. Iki olasilik:
                    //  (a) Baska bir cagri zaten Confirmed yapti -> stok dusuldu, DOKUNMA.
                    //  (b) Expiry job (ReservationCleanupJob) rezervasyonu Expired yapip stogu SERBEST birakti.
                    //      Ama ODEME BASARILI -> stok GEREKLI. Mevcutsa dogrudan dus (reserved zaten serbest).
                    var current = await _stockReservationDal.GetAsync(r => r.id == res.id);
                    if (current != null && current.status == (byte)ReservationStatusEnum.Expired)
                    {
                        var deducted = await _productStockDal.TryDirectDeductAsync(res.product_id, res.size, res.quantity);
                        await _stockMovementDal.AddAsync(new StockMovement
                        {
                            product_id = res.product_id, size = res.size,
                            movement_type = (byte)StockMovementType.Out,
                            quantity = res.quantity, reference_id = orderId,
                            note = deducted > 0
                                ? "Ödeme onaylı - rezervasyon expire olmuştu, stok yeniden güvenceye alındı"
                                : "UYARI: ödeme alındı fakat stok yok (rezervasyon expire + tükendi) - manuel iade/tedarik gerekli",
                            created_at = DateTime.Now
                        });
                    }
                    continue;   // (a) durumu veya expire islendi
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

            // Açıklayıcı yorum: Fark hareketi (denetim + geçmiş)
            await _stockMovementDal.AddAsync(new StockMovement
            {
                product_id = productId,
                size = size,
                movement_type = (byte)StockMovementType.Adjustment,
                quantity = Math.Abs(delta),
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
