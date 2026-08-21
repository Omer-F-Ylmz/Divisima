using Divisima.Entity.Dtos.Address;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Adres ekleme/güncelleme validasyonu (teslimat için kritik alanlar).
    public class AddressRequestValidator : AbstractValidator<AddressRequestDto>
    {
        public AddressRequestValidator()
        {
            RuleFor(x => x.title).NotEmpty().WithMessage("Adres başlığı boş olamaz.").MaximumLength(60);
            RuleFor(x => x.full_name).NotEmpty().WithMessage("Ad soyad boş olamaz.").MaximumLength(120);
            RuleFor(x => x.phone).NotEmpty().WithMessage("Telefon boş olamaz.")
                .Matches(@"^[0-9+\s()-]{7,20}$").WithMessage("Geçerli bir telefon girin.");
            RuleFor(x => x.city).NotEmpty().WithMessage("Şehir boş olamaz.").MaximumLength(50);
            RuleFor(x => x.district).NotEmpty().WithMessage("İlçe boş olamaz.").MaximumLength(50);
            RuleFor(x => x.full_address).NotEmpty().WithMessage("Açık adres boş olamaz.").MaximumLength(500);
            RuleFor(x => x.zip_code).MaximumLength(10).When(x => x.zip_code != null);
        }
    }
}
