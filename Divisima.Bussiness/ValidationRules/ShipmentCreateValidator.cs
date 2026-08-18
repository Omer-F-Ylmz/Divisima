using FluentValidation;
using Divisima.Entity.Dtos.Shipping;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Kargo oluşturma validasyonu.
    public class ShipmentCreateValidator : AbstractValidator<ShipmentCreateDto>
    {
        public ShipmentCreateValidator()
        {
            RuleFor(x => x.order_id).GreaterThan(0).WithMessage("Geçerli bir sipariş seçin.");
            RuleFor(x => x.carrier).LessThanOrEqualTo((byte)4).WithMessage("Geçersiz kargo firması.");
            RuleFor(x => x.tracking_number).NotEmpty().WithMessage("Takip numarası boş olamaz.")
                .MaximumLength(100).Matches("^[A-Za-z0-9-]+$").WithMessage("Takip numarası harf, rakam ve tire içerebilir.");
        }
    }
}
