using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Coupon;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Kupon ekleme validasyonu (kod, değer, limit).
    public class CouponAddRequestValidator : AbstractValidator<CouponAddRequestDto>
    {
        public CouponAddRequestValidator()
        {
            RuleFor(c => c.code).NotEmpty().WithMessage("Kupon kodu boş olamaz.")
                .MaximumLength(40).Matches("^[A-Za-z0-9]+$").WithMessage("Kupon kodu harf ve rakamdan oluşmalı.");
            RuleFor(c => c.value).GreaterThanOrEqualTo(0).WithMessage("İndirim değeri negatif olamaz.");
            RuleFor(c => c.min_amount).GreaterThanOrEqualTo(0);
            RuleFor(c => c.usage_limit).GreaterThanOrEqualTo(0);
            RuleFor(c => c.max_discount_amount).GreaterThan(0).When(c => c.max_discount_amount.HasValue)
                .WithMessage("İndirim tavanı 0'dan büyük olmalı.");
            RuleFor(c => c.per_user_limit).GreaterThanOrEqualTo(0);
            // Yüzde indirim 0-100 aralığında olmalı (150% -> negatif sipariş tutarı engeli)
            RuleFor(c => c.value).LessThanOrEqualTo(100)
                .When(c => c.discount_type == DiscountTypeEnum.Percentage)
                .WithMessage("Yüzde indirim 100'den büyük olamaz.");
        }
    }
}
