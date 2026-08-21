using Divisima.Entity.Dtos.Stock;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Stok düzeltme validasyonu.
    public class StockAdjustValidator : AbstractValidator<StockAdjustDto>
    {
        public StockAdjustValidator()
        {
            RuleFor(x => x.product_id).GreaterThan(0).WithMessage("Geçerli bir ürün seçin.");
            RuleFor(x => x.new_quantity).GreaterThanOrEqualTo(0).WithMessage("Stok negatif olamaz.");
            RuleFor(x => x.note).NotEmpty().WithMessage("Düzeltme notu zorunlu (denetim izi).").MaximumLength(300);
        }
    }
}
