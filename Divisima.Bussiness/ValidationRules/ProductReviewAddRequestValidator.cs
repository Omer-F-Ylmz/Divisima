using Divisima.Entity.Dtos.ProductReview;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Yorum ekleme validasyonu (puan 1-5).
    public class ProductReviewAddRequestValidator : AbstractValidator<ProductReviewAddRequestDto>
    {
        public ProductReviewAddRequestValidator()
        {
            RuleFor(r => r.product_id).GreaterThan(0);
            RuleFor(r => r.customer_id).GreaterThan(0);
            RuleFor(r => r.rating).InclusiveBetween(1, 5).WithMessage("Puan 1 ile 5 arasında olmalı.");
            RuleFor(r => r.comment).MaximumLength(1000).WithMessage("Yorum en fazla 1000 karakter.");
        }
    }
}
