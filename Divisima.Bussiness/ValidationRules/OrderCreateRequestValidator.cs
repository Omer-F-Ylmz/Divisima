using FluentValidation;
using Divisima.Entity.Dtos.Order;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Sipariş oluşturma validasyonu.
    public class OrderCreateRequestValidator : AbstractValidator<OrderCreateRequestDto>
    {
        public OrderCreateRequestValidator()
        {
            RuleFor(o => o.customer_id).GreaterThan(0).WithMessage("Geçerli müşteri gerekli.");
            RuleFor(o => o.items).NotEmpty().WithMessage("Sepet boş olamaz.");
            RuleForEach(o => o.items).ChildRules(item =>
            {
                item.RuleFor(i => i.product_id).GreaterThan(0).WithMessage("Geçerli ürün gerekli.");
                item.RuleFor(i => i.quantity).GreaterThan(0).WithMessage("Adet en az 1 olmalı.")
                    .LessThanOrEqualTo(100).WithMessage("Tek üründen en fazla 100 adet.");
                item.RuleFor(i => i.size).NotEmpty().WithMessage("Beden seçilmeli.");
            });
        }
    }
}
