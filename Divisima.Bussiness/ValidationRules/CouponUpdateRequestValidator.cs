using FluentValidation;
using Divisima.Entity.Dtos.Coupon;
using Divisima.Core.Utilities.Enums;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Kupon GÜNCELLEME validasyonu - Add ile AYNI kurallar (Update bir bypass olmamalı).
    public class CouponUpdateRequestValidator : AbstractValidator<CouponUpdateRequestDto>
    {
        public CouponUpdateRequestValidator()
        {
            RuleFor(c => c.code).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9]+$");
            RuleFor(c => c.value).GreaterThanOrEqualTo(0);
            RuleFor(c => c.min_amount).GreaterThanOrEqualTo(0);
            RuleFor(c => c.usage_limit).GreaterThanOrEqualTo(0);
            RuleFor(c => c.per_user_limit).GreaterThanOrEqualTo(0);
            RuleFor(c => c.max_discount_amount).GreaterThan(0).When(c => c.max_discount_amount.HasValue);
            RuleFor(c => c.value).LessThanOrEqualTo(100)
                .When(c => c.discount_type == DiscountTypeEnum.Percentage)
                .WithMessage("Yüzde indirim 100'den büyük olamaz.");
        }
    }
}
