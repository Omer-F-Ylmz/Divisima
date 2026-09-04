using Divisima.Core.Utilities.Validation;
using Divisima.Entity.Dtos.Address;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Adres ekleme/güncelleme validasyonu (teslimat için kritik alanlar).
    //
    // GF-5 / K4: SAYILAR VE DESEN `GirdiSinirlari`DAN OKUNUYOR - DAVRANIS DEGISMEDI.
    // Her sabit, YERINE GECTIGI literalle BIREBIR ayni degeri tasir (title 60, full_name 120,
    // city/district 50, full_address 500, zip_code 10, telefon deseni ayni). Amac misafir
    // yolunun (GuestCheckoutValidator) ayni degerlere BAKMASI; boylece iki yol yarin
    // sessizce ayrisamaz. Mesaj metinleri TASINMADI - musteriye gorunen dil degismiyor.
    public class AddressRequestValidator : AbstractValidator<AddressRequestDto>
    {
        public AddressRequestValidator()
        {
            RuleFor(x => x.title).NotEmpty().WithMessage("Adres başlığı boş olamaz.").MaximumLength(GirdiSinirlari.AdresBasligi);
            RuleFor(x => x.full_name).NotEmpty().WithMessage("Ad soyad boş olamaz.").MaximumLength(GirdiSinirlari.AdresAdSoyad);
            RuleFor(x => x.phone).NotEmpty().WithMessage("Telefon boş olamaz.")
                .Matches(GirdiSinirlari.TelefonDeseni).WithMessage("Geçerli bir telefon girin.");
            RuleFor(x => x.city).NotEmpty().WithMessage("Şehir boş olamaz.").MaximumLength(GirdiSinirlari.Sehir);
            RuleFor(x => x.district).NotEmpty().WithMessage("İlçe boş olamaz.").MaximumLength(GirdiSinirlari.Ilce);
            RuleFor(x => x.full_address).NotEmpty().WithMessage("Açık adres boş olamaz.").MaximumLength(GirdiSinirlari.AcikAdres);
            RuleFor(x => x.zip_code).MaximumLength(GirdiSinirlari.PostaKodu).When(x => x.zip_code != null);
        }
    }
}
