using FluentValidation;
using Divisima.Entity.Dtos.Category;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Kategori ekleme validasyonu.
    public class CategoryAddRequestValidator : AbstractValidator<CategoryAddRequestDto>
    {
        public CategoryAddRequestValidator()
        {
            RuleFor(c => c.name).NotEmpty().WithMessage("Kategori adı boş olamaz.").MaximumLength(100);
            RuleFor(c => c.slug).NotEmpty().WithMessage("Slug boş olamaz.")
                .Matches("^[a-z0-9-]+$").WithMessage("Slug sadece küçük harf, rakam ve tire içerebilir.");
        }
    }
}
