using FluentValidation;
using Divisima.Entity.Dtos.Category;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Kategori GÜNCELLEME validasyonu. Add ile simetrik (aynı kurallar) + id kontrolü.
    // Önceden Update DTO'sunun validator'ı yoktu -> Add doğrulanıyor ama Update doğrulanmıyordu (asimetri).
    public class CategoryUpdateRequestValidator : AbstractValidator<CategoryUpdateRequestDto>
    {
        public CategoryUpdateRequestValidator()
        {
            RuleFor(c => c.id).GreaterThan(0).WithMessage("Geçerli bir kategori id gerekli.");
            RuleFor(c => c.name).NotEmpty().WithMessage("Kategori adı boş olamaz.").MaximumLength(100);
            RuleFor(c => c.slug).NotEmpty().WithMessage("Slug boş olamaz.")
                .Matches("^[a-z0-9-]+$").WithMessage("Slug sadece küçük harf, rakam ve tire içerebilir.");
        }
    }
}
