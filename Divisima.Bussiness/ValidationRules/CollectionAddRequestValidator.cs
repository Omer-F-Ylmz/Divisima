using FluentValidation;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Collection;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Koleksiyon ekleme validasyonu. Elçi tipinde küratör zorunlu.
    public class CollectionAddRequestValidator : AbstractValidator<CollectionAddRequestDto>
    {
        public CollectionAddRequestValidator()
        {
            RuleFor(c => c.name).NotEmpty().WithMessage("Koleksiyon adı boş olamaz.").MaximumLength(150);
            RuleFor(c => c.slug).NotEmpty().WithMessage("Slug boş olamaz.")
                .Matches("^[a-z0-9-]+$").WithMessage("Slug sadece küçük harf, rakam ve tire içerebilir.");

            // Açıklayıcı yorum: Stil elçisi koleksiyonunda küratör adı zorunlu
            When(c => c.collection_type == CollectionTypeEnum.Ambassador, () =>
            {
                RuleFor(c => c.curator_name).NotEmpty().WithMessage("Stil elçisi koleksiyonunda küratör adı zorunludur.");
            });
        }
    }
}
