using Divisima.Entity.Dtos.Return;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: İade talebi validasyonu.
    public class ReturnCreateRequestValidator : AbstractValidator<ReturnCreateRequestDto>
    {
        public ReturnCreateRequestValidator()
        {
            RuleFor(x => x.order_id).GreaterThan(0).WithMessage("Geçerli bir sipariş seçin.");
            RuleFor(x => x.product_id).GreaterThan(0).WithMessage("Geçerli bir ürün seçin.");
            RuleFor(x => x.quantity).GreaterThan(0).WithMessage("Adet en az 1 olmalı.");
            RuleFor(x => x.reason).LessThanOrEqualTo((byte)3).WithMessage("Geçersiz iade nedeni.");
            RuleFor(x => x.return_type).LessThanOrEqualTo((byte)1).WithMessage("Geçersiz iade tipi.");
            RuleFor(x => x.description).MaximumLength(1000).When(x => x.description != null);
        }
    }
}
