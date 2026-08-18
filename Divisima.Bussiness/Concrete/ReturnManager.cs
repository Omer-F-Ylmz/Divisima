using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Results;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Sanitization;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Return;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: İade/değişim yöneticisi. Akış:
    //   1) Müşteri teslim edilmiş, kendine ait sipariş için talep açar (iade süresi + sahiplik kontrolü).
    //   2) Admin onaylar -> Iyzico refund + stok iade + durum Tamamlandı (transaction'lı, atomik).
    //   3) Admin reddeder -> durum Reddedildi + not.
    public class ReturnManager : IReturnService
    {
        private const int ReturnWindowDays = 14;   // teslimden sonra iade süresi

        private readonly IReturnRequestDal _returnDal;
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IIyzicoClient _iyzico;
        private readonly IRefundService _refundService;
        private readonly IStockService _stockService;
        private readonly ICustomerDal _customerDal;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;

        public ReturnManager(IReturnRequestDal returnDal, IOrderDal orderDal, IOrderItemDal orderItemDal, IIyzicoClient iyzico, IStockService stockService,
            ICustomerDal customerDal, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork, IRefundService refundService)
        {
            _returnDal = returnDal;
            _refundService = refundService;
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _iyzico = iyzico;
            _stockService = stockService;
            _customerDal = customerDal;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
        }

        public async Task<(HttpStatusCode, Result)> CreateReturn(ReturnCreateRequestDto dto)
        {
            var order = await _orderDal.GetAsync(o => o.id == dto.order_id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // Açıklayıcı yorum: SAHİPLİK - yalnız kendi siparişi (IDOR engeli)
            if (order.customer_id != dto.customer_id)
                return (HttpStatusCode.Forbidden, new ErrorResult(Messages.ReturnNotYourOrder));

            // Açıklayıcı yorum: Yalnız teslim edilmiş sipariş iade edilebilir
            if (order.status != (byte)OrderStatusEnum.Delivered)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReturnOrderNotDelivered));

            // Açıklayıcı yorum: İade süresi TESLİM tarihinden sayılır (delivered_at); yoksa sipariş tarihine düşer.
            // (Önceden hep created_at'ti - geç teslim edilen siparişte müşterinin iade süresi haksız azalırdı.)
            var returnWindowBase = order.delivered_at ?? order.created_at;
            if (returnWindowBase.AddDays(ReturnWindowDays) < DateTime.Now)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReturnWindowExpired));

            // Açıklayıcı yorum: Sipariş kaleminde bu ürün/beden var mı - İPTAL EDİLMİŞ kalem iade EDİLEMEZ.
            // (İptal zaten iade+stok-geri yapmıştı; iptal edilmiş kalemi tekrar iade etmek ÇİFT PARA İADESİ olurdu.)
            var item = await _orderItemDal.GetAsync(i => i.order_id == order.id
                && i.product_id == dto.product_id && i.size == dto.size && !i.is_cancelled);
            if (item == null || dto.quantity <= 0)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReturnInvalidItem));

            // Açıklayıcı yorum: ÇİFT İADE ENGELİ - kalan iade-edilebilir miktar = orijinal adet - reddedilmemiş iadeler.
            // Kritik: sadece Pending değil, Approved/Completed de miktarı TÜKETİR (yoksa 5 al -> 5 iade et -> tekrar 5 iade
            // et ile çift para iadesi alınırdı). Rejected iade miktarı serbest bırakır (yeniden talep edilebilir).
            var priorReturns = await _returnDal.GetListAsync(r => r.order_id == order.id && r.product_id == dto.product_id
                && r.size == dto.size && r.status != (byte)ReturnStatusEnum.Rejected);
            int alreadyReturned = priorReturns.Sum(r => r.quantity);
            int remaining = item.quantity - alreadyReturned;
            if (dto.quantity > remaining)
                return (HttpStatusCode.Conflict, new ErrorResult(Messages.ReturnAlreadyRequested));

            await _returnDal.AddAsync(new ReturnRequest
            {
                order_id = order.id,
                customer_id = dto.customer_id,
                product_id = dto.product_id,
                size = dto.size,
                quantity = dto.quantity,
                reason = dto.reason,
                description = InputSanitizer.Sanitize(dto.description ?? ""),  // stored XSS savunması
                return_type = dto.return_type,
                status = (byte)ReturnStatusEnum.Pending,
                // FAZLA-IADE DUZELTMESI: kupon indirimli siparis icin liste fiyatini iade etmek fazla-iade olur.
                // Kaleme dusen indirimi orantili dus - musteri GERCEKTE ne odediyse o kadar iade.
                refund_amount = order.subtotal > 0
                    ? MoneyHelper.Round(item.unit_price * dto.quantity * (order.subtotal - order.discount_amount) / order.subtotal)
                    : item.unit_price * dto.quantity,
                created_at = DateTime.Now
            });

            return (HttpStatusCode.OK, new SuccessResult(Messages.ReturnCreated));
        }

        public async Task<(HttpStatusCode, Result)> ProcessReturn(ReturnProcessRequestDto dto)
        {
            var ret = await _returnDal.GetAsync(r => r.id == dto.return_id);
            if (ret == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.ReturnNotFound));
            if (ret.status != (byte)ReturnStatusEnum.Pending)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReturnAlreadyProcessed));

            // Açıklayıcı yorum: RET - refund yok, sadece durum + not
            if (!dto.approve)
            {
                ret.status = (byte)ReturnStatusEnum.Rejected;
                ret.admin_note = InputSanitizer.Sanitize(dto.admin_note ?? "");
                ret.processed_at = DateTime.Now;
                await _returnDal.UpdateAsync(ret);
                return (HttpStatusCode.OK, new SuccessResult(Messages.ReturnRejected));
            }

            // Açıklayıcı yorum: ONAY - Iyzico refund + stok iade (atomik)
            var order = await _orderDal.GetAsync(o => o.id == ret.order_id);
            // SESSIZ PARA KAYBI FIX (H53): order null kontrolu YOKTU. RefundManager, order==null gelince
            // "yapacak bir sey yok" kabul edip Success=TRUE donuyor -> iade Completed isaretlenir, STOK geri
            // yuklenir, ama MUSTERIYE PARA GITMEZ ve sistem "onaylandi" der. Veri tutarsizliginda (silinmis/
            // bulunamayan siparis) sessizce para kaybi olurdu; artik acikca hata donuyoruz.
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));
            // OLU SORGU KALDIRILDI (H53): "payment" cekiliyordu ama merkezi RefundManager refactor'unden sonra
            // HIC KULLANILMIYORDU (odeme kaydini RefundManager kendi cekiyor) -> her onaylanan iadede bosuna DB turu.

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Açıklayıcı yorum: ATOMIK GUARD - iadeyi Pending->Completed atomik geçir; YALNIZCA bu çağrı kazanırsa
                // refund yapılır. İki eşzamanlı ProcessReturn (iki admin / çift-tık) aynı iadeyi ÇİFT refund EDEMEZ.
                var won = await _returnDal.TryTransitionAsync(ret.id,
                    (byte)ReturnStatusEnum.Pending, (byte)ReturnStatusEnum.Completed);
                if (won == 0)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReturnAlreadyProcessed));
                }

                // MERKEZİ İADE (DRY): ödeme kaynağına göre iade - RefundManager (kart->Iyzico, cüzdan->store credit,
                // COD/nakit->tümü store credit). Önceden bu ~20 satır ReturnManager + OrderManager'da tekrarlanıyordu;
                // artık tek yerde (duplikasyon-drift riski yok). Iyzico başarısızsa Success=false -> rollback.
                var refundOutcome = await _refundService.RefundToSourceAsync(order, ret.refund_amount, "İade - ödeme kaynağına iade");
                if (!refundOutcome.Success)
                {
                    await _unitOfWork.RollbackAsync();
                    return (HttpStatusCode.BadRequest, new ErrorResult(Messages.ReturnRefundFailed));
                }
                ret.refund_id = refundOutcome.RefundId;

                // Açıklayıcı yorum: Stok iade (ürün geri geldi)
                await _stockService.IncreaseStock(ret.product_id, ret.size, ret.quantity, ret.order_id);

                ret.status = (byte)ReturnStatusEnum.Completed;
                ret.admin_note = InputSanitizer.Sanitize(dto.admin_note ?? "");
                ret.processed_at = DateTime.Now;
                await _returnDal.UpdateAsync(ret);

                await _unitOfWork.CommitAsync();
                return (HttpStatusCode.OK, new SuccessResult(Messages.ReturnApproved));
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.ReturnProcessingError));
            }
        }

        public async Task<(HttpStatusCode, Result)> GetMyReturns(int customerId)
        {
            var returns = await _returnDal.GetListAsync(r => r.customer_id == customerId);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ReturnResponseDto>>(returns.Select(Map).ToList()));
        }

        public async Task<(HttpStatusCode, Result)> GetPendingReturns()
        {
            var returns = await _returnDal.GetListAsync(r => r.status == (byte)ReturnStatusEnum.Pending);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ReturnResponseDto>>(returns.Select(Map).ToList()));
        }

        // Açıklayıcı yorum: Entity -> DTO
        private static ReturnResponseDto Map(ReturnRequest r) => new()
        {
            id = r.id,
            order_id = r.order_id,
            product_id = r.product_id,
            size = r.size,
            quantity = r.quantity,
            reason = r.reason,
            return_type = r.return_type,
            status = r.status,
            status_name = ((ReturnStatusEnum)r.status).ToString(),
            refund_amount = r.refund_amount,
            admin_note = r.admin_note,
            created_at = r.created_at
        };
    }
}
