using FluentValidation;
using Divisima.Entity.Dtos.ProductQuestion;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Ürün soru-cevap validasyonları.
    public class ProductQuestionAskValidator : AbstractValidator<ProductQuestionAskDto>
    {
        public ProductQuestionAskValidator()
        {
            RuleFor(q => q.product_id).GreaterThan(0);
            RuleFor(q => q.question).NotEmpty().MinimumLength(5).MaximumLength(1000)
                .WithMessage("Soru 5-1000 karakter olmalı.");
        }
    }

    public class ProductQuestionAnswerValidator : AbstractValidator<ProductQuestionAnswerDto>
    {
        public ProductQuestionAnswerValidator()
        {
            RuleFor(a => a.question_id).GreaterThan(0);
            RuleFor(a => a.answer).NotEmpty().MaximumLength(2000);
        }
    }
}
