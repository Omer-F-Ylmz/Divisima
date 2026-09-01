using System.Net;
using Divisima.Bussiness.Abstract;
using Divisima.Core.DataAccess;
using Divisima.Core.Integrations.Iyzico;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
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

        // SPRINT 8 MADDE 5: iade listesine urun adi doldurmak icin.
        private readonly IProductDal _productDal;
        private readonly IReturnRequestDal _returnDal;
        private readonly IOrderDal _orderDal;
        private readonly IOrderItemDal _orderItemDal;
        private readonly IIyzicoClient _iyzico;
        private readonly IRefundService _refundService;
        private readonly IStockService _stockService;
        private readonly ICustomerDal _customerDal;
        private readonly IStoreCreditTransactionDal _creditTxDal;
        private readonly IUnitOfWork _unitOfWork;

        // DALGA B / B3: iade sonucunu musteriye bildirmek icin.
        private readonly Divisima.Bussiness.Outbox.IOutboxService _outboxService;
        private readonly Divisima.Core.Utilities.Mail.IMailLinkBuilder _links;

        public ReturnManager(IReturnRequestDal returnDal, IOrderDal orderDal, IOrderItemDal orderItemDal, IIyzicoClient iyzico, IStockService stockService,
            ICustomerDal customerDal, IStoreCreditTransactionDal creditTxDal, IUnitOfWork unitOfWork, IRefundService refundService,
            IProductDal productDal, Divisima.Bussiness.Outbox.IOutboxService outboxService,
            Divisima.Core.Utilities.Mail.IMailLinkBuilder links)
        {
            _productDal = productDal;
            _returnDal = returnDal;
            _refundService = refundService;
            _orderDal = orderDal;
            _orderItemDal = orderItemDal;
            _iyzico = iyzico;
            _stockService = stockService;
            _customerDal = customerDal;
            _creditTxDal = creditTxDal;
            _unitOfWork = unitOfWork;
            _outboxService = outboxService;
            _links = links;
        }

        public async Task<(HttpStatusCode, Result)> CreateReturn(ReturnCreateRequestDto dto)
        {
            var order = await _orderDal.GetAsync(o => o.id == dto.order_id);
            if (order == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

            // ══ GF-1 / K4 (B-1) - SAHIPLIK IHLALI 404, 403 DEGIL ══════════════════════════
            //
            // TEK SOZLESME `SecureControllerBase`te YAZILI: "sahiplik ihlalinde artik tek
            // sozlesme 404 (varlik sizdirilmaz)". Bu satir onu ihlal ediyordu: 403 + "Bu
            // siparis size ait degil." yaniti, siparisin VAR OLDUGUNU ve BASKASINA ait
            // oldugunu soyluyor - saldirgan id araligini tarayarak hangi id'lerin gercek
            // siparis oldugunu sayabilirdi.
            //
            // YANIT USTTEKI "yok" DALIYLA BIREBIR AYNI (durum VE mesaj): ayirt edilebilir
            // kalan tek bir alan bile sizintinin kendisidir.
            if (order.customer_id != dto.customer_id)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.OrderNotFound));

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
                // DALGA B / B3: SIRA ONEMLI - once KALICI olsun, sonra bildir. Tersi olsaydi
                // kaydedilemeyen bir ret icin musteriye "iaden reddedildi" maili gidebilirdi.
                await IadeSonucuMailiYazAsync(ret, onaylandi: false, sonuc: null);
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

                // DALGA B / B3: bildirim mesaji TRANSACTION ICINDE yaziliyor (Sprint 8 madde 3 kalibi).
                // Boylece rollback olursa mail de yazilmamis olur - "iaden onaylandi, para iade edildi"
                // maili alip iadesi geri alinmis bir musteri OLUSAMAZ.
                await IadeSonucuMailiYazAsync(ret, onaylandi: true, sonuc: refundOutcome);

                await _unitOfWork.CommitAsync();
                return (HttpStatusCode.OK, new SuccessResult(Messages.ReturnApproved));
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                return (HttpStatusCode.InternalServerError, new ErrorResult(Messages.ReturnProcessingError));
            }
        }

        // ══ DALGA B / B3 - IADE SONUCU MUSTERIYE BILDIRILIR ═══════════════════════════════
        // OLCULEN ONCE-DURUM: bu dosyada mail / outbox / bildirim SIFIR referanstı (tarandi).
        // Admin iadeyi onaylayip 499,90 TL magaza kredisi yazsa da, ya da reddetse de musteriye
        // HICBIR SEY gitmiyordu; ogrenmesinin tek yolu Hesabim > Iadelerim'i acmakti. Iadenin
        // sonucu, musterinin talebi actiktan sonra BEKLEDIGI tek seydir.
        //
        // KANAL: Dalga A'nin kurdugu "EmailNotification" outbox tipi. SMTP patlarsa iade akisi
        // DUSMEZ (mesaj 5 kez yeniden denenir, sonra Failed olarak GORUNUR).
        //
        // TUTAR NEREYE GITTI SORUSU UYDURULMUYOR: RefundOutcome zaten OnlineRefunded /
        // CreditRefunded ayrimini tasiyor. Kartla odenmis siparisin iadesi karta, kapida
        // odenmisinki magaza kredisine gider ve musteriye HANGISI oldugu yazilir - "iade edildi"
        // deyip nereye gittigini soylememek, parasini kartinda arayan musteri uretir.
        //
        // TRY/CATCH YOK - BILINCLI: buradaki tek is iki salt-okur sorgu ve bir outbox satiri.
        // Onay yolunda cagri transaction'in ICINDEDIR; oradaki bir DB hatasi zaten TUM iadeyi
        // geri almalidir (mevcut catch/rollback dali bunu yapar). Hatayi burada yutmak,
        // "para iade edildi ama kayit yok" durumunu sessizlestirmek olurdu.
        private async Task IadeSonucuMailiYazAsync(ReturnRequest ret, bool onaylandi, RefundOutcome? sonuc)
        {
            var musteri = await _customerDal.GetAsync(c => c.id == ret.customer_id);
            if (musteri == null || string.IsNullOrWhiteSpace(musteri.email)) return;   // adres yoksa gonderilecek bir sey de yok

            var urun = await _productDal.GetAsync(p => p.id == ret.product_id);
            // Urun pasiflenmis/silinmisse UYDURMA ad yazilmaz - kimlikle gosterilir (ZenginlestirAsync ile ayni kural).
            var urunAdi = string.IsNullOrWhiteSpace(urun?.name) ? $"Ürün #{ret.product_id}" : urun!.name;
            var kalem = $"{urunAdi} · {ret.size} · {ret.quantity} adet";

            var baglanti = _links.VitrinBaglantisi("#/hesabim/iadelerim");
            var yonerge = baglanti == null
                ? "Ayrıntı için Hesabım > İadelerim sayfasına bakabilirsin."      // origin yoksa yarim URL URETILMEZ
                : $"Ayrıntı için:\n{baglanti}";

            string konu, govde;
            if (onaylandi)
            {
                // Tutarlar kultur PINLI bicimle (Sprint 8 madde 13) - surec zaten tr-TR'ye pinli.
                var nereye = (sonuc != null && sonuc.OnlineRefunded > 0 && sonuc.CreditRefunded > 0)
                    ? $"{sonuc.OnlineRefunded:N2} TL ödeme yaptığın karta, {sonuc.CreditRefunded:N2} TL mağaza kredine yatırıldı."
                    : (sonuc != null && sonuc.OnlineRefunded > 0)
                        ? $"{sonuc.OnlineRefunded:N2} TL ödeme yaptığın karta iade edildi. Bankana bağlı olarak hesabına geçmesi birkaç iş günü sürebilir."
                        : $"{(sonuc != null ? sonuc.CreditRefunded : ret.refund_amount):N2} TL mağaza kredine yatırıldı; sonraki siparişinde kullanabilirsin.";

                konu = "Divisima - İade talebin onaylandı";
                govde = $"Merhaba {musteri.name},\n\nİade talebin onaylandı.\n{kalem}\n\n{nereye}\n\n{yonerge}";
            }
            else
            {
                var not = string.IsNullOrWhiteSpace(ret.admin_note) ? "" : $"\nDeğerlendirme notu: {ret.admin_note}\n";
                konu = "Divisima - İade talebin hakkında";
                govde = $"Merhaba {musteri.name},\n\nİade talebin değerlendirildi ve onaylanmadı.\n{kalem}\n{not}\nBu sonuçla ilgili sorun varsa bize yanıt yazabilirsin.\n\n{yonerge}";
            }

            await _outboxService.WriteAsync("EmailNotification",
                new Divisima.Core.Utilities.Mail.MailMessageDto { To = musteri.email, Subject = konu, Body = govde });
        }

        public async Task<(HttpStatusCode, Result)> GetMyReturns(int customerId)
        {
            var returns = await _returnDal.GetListAsync(r => r.customer_id == customerId);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ReturnResponseDto>>(await ZenginlestirAsync(returns)));
        }

        public async Task<(HttpStatusCode, Result)> GetPendingReturns()
        {
            var returns = await _returnDal.GetListAsync(r => r.status == (byte)ReturnStatusEnum.Pending);
            return (HttpStatusCode.OK, new SuccessDataResult<List<ReturnResponseDto>>(await ZenginlestirAsync(returns)));
        }

        // SPRINT 8 MADDE 5: urun adi ve siparis numarasi DTO'ya doldurulur.
        // N+1 YOK: urunler ve siparisler TEK sorguda cekilip sozlukten eslestirilir.
        // Urun silinmis/pasiflenmisse ad null kalir - istemci o zaman kimlikle gosterir;
        // UYDURMA ad yazilmaz.
        private async Task<List<ReturnResponseDto>> ZenginlestirAsync(List<ReturnRequest> returns)
        {
            var liste = returns.Select(Map).ToList();
            if (liste.Count == 0) return liste;

            var urunIds = liste.Select(x => x.product_id).Distinct().ToList();
            var urunAdlari = (await _productDal.GetListNoTrackingAsync(p => urunIds.Contains(p.id)))
                .ToDictionary(p => p.id, p => p.name);

            var siparisIds = liste.Select(x => x.order_id).Distinct().ToList();
            var siparisNolar = (await _orderDal.GetListNoTrackingAsync(o => siparisIds.Contains(o.id)))
                .ToDictionary(o => o.id, o => o.order_number);

            foreach (var x in liste)
            {
                if (urunAdlari.TryGetValue(x.product_id, out var ad)) x.product_name = ad;
                if (siparisNolar.TryGetValue(x.order_id, out var no)) x.order_number = no;
            }
            return liste;
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
