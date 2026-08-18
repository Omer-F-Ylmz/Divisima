using FluentValidation;
using Divisima.Core.Utilities.Enums;
using Divisima.Entity.Dtos.Collection;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Koleksiyon GÜNCELLEME validasyonu. Add ile simetrik + id kontrolü.
    // Önceden Update DTO'sunun validator'ı yoktu -> Add-Update doğrulama asimetrisi kapatıldı.
    public class CollectionUpdateRequestValidator : AbstractValidator<CollectionUpdateRequestDto>
    {
        public CollectionUpdateRequestValidator()
        {
            RuleFor(c => c.id).GreaterThan(0).WithMessage("Geçerli bir koleksiyon id gerekli.");
            RuleFor(c => c.name).NotEmpty().WithMessage("Koleksiyon adı boş olamaz.").MaximumLength(150);
            RuleFor(c => c.slug).NotEmpty().WithMessage("Slug boş olamaz.")
                .Matches("^[a-z0-9-]+$").WithMessage("Slug sadece küçük harf, rakam ve tire içerebilir.");

            // Açıklayıcı yorum: Stil elçisi koleksiyonunda küratör adı zorunlu (Add ile aynı)
            When(c => c.collection_type == CollectionTypeEnum.Ambassador, () =>
            {
                RuleFor(c => c.curator_name).NotEmpty().WithMessage("Stil elçisi koleksiyonunda küratör adı zorunludur.");
            });
        }
    }
}
