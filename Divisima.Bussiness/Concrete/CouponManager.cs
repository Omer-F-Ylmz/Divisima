using System.Net;
using AutoMapper;
using Divisima.Bussiness.Abstract;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using Divisima.Core.Utilities.Pricing;
using Divisima.Core.Utilities.Results;
using Divisima.DataAccess.Abstract;
using Divisima.Entity.Dtos.Coupon;
using Divisima.Entity.Entities;

namespace Divisima.Bussiness.Concrete
{
    // Açıklayıcı yorum: Kupon iş kuralları. ValidateCoupon frontend'deki applyCoupon + couponDiscount'un
    // backend karşılığı: kod bul -> min kontrol -> tipe göre indirim hesapla.
    public class CouponManager : ICouponService
    {
        private readonly ICouponDal _couponDal;
        private readonly IOrderDal _orderDal;
        private readonly IMapper _mapper;

        public CouponManager(ICouponDal couponDal, IMapper mapper, IOrderDal orderDal)
        {
            _couponDal = couponDal;
            _orderDal = orderDal;
            _mapper = mapper;
        }

        // Açıklayıcı yorum: Kupon ekle. Aynı kod varsa reddet.
        public async Task<(HttpStatusCode, Result)> Add(CouponAddRequestDto dto)
        {
            // Açıklayıcı yorum: DEĞER VALİDASYONU - yüzde 0-100, tutarlar negatif olamaz.
            // (Aksi halde %150 indirim -> negatif sipariş tutarı, veya negatif değer -> fiyata EKLEME olurdu.)
            if (dto.value < 0 || dto.min_amount < 0 || dto.usage_limit < 0
                || (dto.max_discount_amount.HasValue && dto.max_discount_amount.Value < 0))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CouponInvalidValue));
            if (dto.discount_type == DiscountTypeEnum.Percentage && dto.value > 100)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CouponInvalidValue));

            var normalized = Divisima.Core.Utilities.Text.KimlikDizgesi.KanonikKod(dto.code);   // B2: KANONIK kupon kodu
            var exists = await _couponDal.GetAsync(c => c.code == normalized && c.is_active);
            if (exists != null)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CouponAlreadyExists));

            var coupon = _mapper.Map<Coupon>(dto);
            coupon.code = normalized;
            coupon.discount_type = (byte)dto.discount_type;
            coupon.is_active = true;
            coupon.created_at = DateTime.Now;
            await _couponDal.AddAsync(coupon);

            return (HttpStatusCode.Created, new SuccessResult(Messages.CouponAdded));
        }

        // Açıklayıcı yorum: Kupon güncelle.
        public async Task<(HttpStatusCode, Result)> Update(CouponUpdateRequestDto dto)
        {
            var coupon = await _couponDal.GetAsync(c => c.id == dto.id);
            if (coupon == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CouponNotFound));

            // Açıklayıcı yorum: DEĞER VALİDASYONU (Add ile AYNI - Update bir bypass olmamalı).
            if (dto.value < 0 || dto.min_amount < 0 || dto.usage_limit < 0
                || (dto.max_discount_amount.HasValue && dto.max_discount_amount.Value < 0))
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CouponInvalidValue));
            if (dto.discount_type == DiscountTypeEnum.Percentage && dto.value > 100)
                return (HttpStatusCode.BadRequest, new ErrorResult(Messages.CouponInvalidValue));

            _mapper.Map(dto, coupon);
            coupon.code = Divisima.Core.Utilities.Text.KimlikDizgesi.KanonikKod(dto.code);   // B2: KANONIK kupon kodu
            coupon.discount_type = (byte)dto.discount_type;
            coupon.updated_at = DateTime.Now;
            await _couponDal.UpdateAsync(coupon);

            return (HttpStatusCode.OK, new SuccessResult(Messages.CouponUpdated));
        }

        // Açıklayıcı yorum: Kalıcı sil.
        public async Task<(HttpStatusCode, Result)> Delete(int id)
        {
            var coupon = await _couponDal.GetAsync(c => c.id == id);
            if (coupon == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CouponNotFound));

            // Açıklayıcı yorum: Soft-delete - kayıt silinmez, pasifleştirilir (sipariş/ilişki bütünlüğü korunur)
            coupon.is_active = false;
            await _couponDal.UpdateAsync(coupon);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CouponDeleted));
        }

        // Açıklayıcı yorum: Aktif/pasif toggle.
        public async Task<(HttpStatusCode, Result)> ChangeStatus(int id)
        {
            var coupon = await _couponDal.GetIgnoringFiltersAsync(c => c.id == id);
            if (coupon == null)
                return (HttpStatusCode.NotFound, new ErrorResult(Messages.CouponNotFound));

            coupon.is_active = !coupon.is_active;
            coupon.updated_at = DateTime.Now;
            await _couponDal.UpdateAsync(coupon);
            return (HttpStatusCode.OK, new SuccessResult(Messages.CouponStatusChanged));
        }

        // Açıklayıcı yorum: Tüm aktif kuponlar (admin).
        public async Task<(HttpStatusCode, Result)> GetList()
        {
            var coupons = await _couponDal.GetListAsync(c => c.is_active);
            var data = _mapper.Map<List<CouponListResponseDto>>(coupons);
            return (HttpStatusCode.OK, new SuccessDataResult<List<CouponListResponseDto>>(data, Messages.CouponListed));
        }

        // Açıklayıcı yorum: Kupon doğrula + indirim hesapla. Frontend applyCoupon/couponDiscount birebir:
        // 1) kod bulunamadı -> geçersiz, 2) sepet < min -> min tutmuyor, 3) tipe göre indirim.
        public async Task<(HttpStatusCode, Result)> ValidateCoupon(CouponValidateRequestDto dto)
        {
            var coupon = await _couponDal.GetByCodeAsync(dto.code);
            if (coupon == null)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<CouponValidateResponseDto>(Messages.CouponInvalid));

            // Açıklayıcı yorum: Son kullanma tarihi kontrolü (WebCoupon expire_date)
            if (coupon.expire_date.HasValue && coupon.expire_date.Value < DateTime.Now)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<CouponValidateResponseDto>(Messages.CouponExpired));

            // Açıklayıcı yorum: Kullanım limiti kontrolü (0 = sınırsız). TUTARLILIK: PlaceOrder enforcement'ı SİPARİŞ SAYISI
            // ile denetliyor (used_count kart-only olduğundan güvenilmez); önizleme de AYNI order-count ile hesaplanmalı,
            // aksi halde önizleme "geçerli" derken sipariş reddedilir (ya da tersi) - kullanıcı için kafa karıştırıcı tutarsızlık.
            if (coupon.usage_limit > 0)
            {
                // TUTARSIZLIK FIX (H52): onizleme hala "!= Cancelled" ile sayiyordu -> ODENMEMIS siparisler
                // limiti tuketiyor gorunuyordu. PlaceOrder (H50) artik "odenmis + taze bekleyen" sayiyor;
                // ikisi AYRISINCA onizleme "kupon tukendi" derken siparis GECIYORDU (kodun kendi yorumunun
                // uyardigi celiskI). Ayni kural + AYNI merkezi sure (PaidOrderSpec.PendingGraceMinutes).
                var couponGrace = DateTime.Now.AddMinutes(-PaidOrderSpec.PendingGraceMinutes);
                var globalUses = await _orderDal.CountAsync(o =>
                    o.coupon_code == coupon.code &&
                    (PaidOrderSpec.PaidStatuses.Contains(o.status)
                     || (o.status == (byte)OrderStatusEnum.Pending && o.created_at >= couponGrace)));
                if (globalUses >= coupon.usage_limit)
                    return (HttpStatusCode.BadRequest, new ErrorDataResult<CouponValidateResponseDto>(Messages.CouponUsageLimitReached));
            }

            // Açıklayıcı yorum: Minimum sepet tutarı kontrolü (frontend sub < d.min)
            if (dto.cart_total < coupon.min_amount)
                return (HttpStatusCode.BadRequest, new ErrorDataResult<CouponValidateResponseDto>(Messages.CouponMinAmountNotMet));

            // Açıklayıcı yorum: İlk-sipariş kuponu - müşterinin tamamlanmış siparişi varsa geçersiz
            if (coupon.first_order_only)
            {
                // PERFORMANS (H51): EXISTS - "hic tamamlanmis siparisi var mi".
                var hasCompleted = await _orderDal.AnyAsync(o =>
                    o.customer_id == dto.customer_id && PaidOrderSpec.PaidStatuses.Contains(o.status));   // H52: merkezi kural
                if (hasCompleted)
                    return (HttpStatusCode.BadRequest, new ErrorDataResult<CouponValidateResponseDto>(Messages.CouponFirstOrderOnly));
            }

            var response = new CouponValidateResponseDto
            {
                code = coupon.code,
                discount_type = ((DiscountTypeEnum)coupon.discount_type).ToString(),
                free_shipping = false,
                discount_amount = 0m
            };

            // Açıklayıcı yorum: Tipe göre indirim (frontend couponDiscount: pct/fixed/ship)
            // Açıklayıcı yorum: byte discount_type (0=Percentage, 1=Fixed, 2=FreeShipping)
            switch ((DiscountTypeEnum)coupon.discount_type)
            {
                case DiscountTypeEnum.Percentage:
                    // Açıklayıcı yorum: Yüzde indirim - Math.round(sub*val)/100 mantığı
                    var pct = MoneyHelper.Percentage(dto.cart_total, coupon.value);
                    // Açıklayıcı yorum: Yüzde kuponlarda indirim tavanı (WebCoupon max_discount_amount)
                    if (coupon.max_discount_amount.HasValue && pct > coupon.max_discount_amount.Value)
                        pct = coupon.max_discount_amount.Value;
                    response.discount_amount = pct;
                    break;
                case DiscountTypeEnum.Fixed:
                    // Açıklayıcı yorum: Sabit indirim sepeti geçemez (Math.min(val, sub))
                    response.discount_amount = Math.Min(coupon.value, dto.cart_total);
                    break;
                case DiscountTypeEnum.FreeShipping:
                    // Açıklayıcı yorum: Kargo bedava - indirim 0, bayrak true
                    response.free_shipping = true;
                    break;
            }

            return (HttpStatusCode.OK, new SuccessDataResult<CouponValidateResponseDto>(response, Messages.CouponValid));
        }
    }
}
