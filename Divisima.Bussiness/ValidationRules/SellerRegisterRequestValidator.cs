using Divisima.Core.Security;
using Divisima.Entity.Dtos.Seller;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules
{
    // Açıklayıcı yorum: Satıcı kayıt doğrulama (Cafixo FluentValidation kalıbı). Şifre gücü müşteriyle aynı.
    public class SellerRegisterRequestValidator : AbstractValidator<SellerRegisterRequestDto>
    {
        public SellerRegisterRequestValidator()
        {
            RuleFor(s => s.business_name).NotEmpty().WithMessage("İşletme adı boş olamaz.").MaximumLength(200);
            RuleFor(s => s.email).NotEmpty().EmailAddress().WithMessage("Geçerli bir e-posta giriniz.");
            RuleFor(s => s.phone).NotEmpty().Matches(@"^[0-9+\s()-]{7,20}$").WithMessage("Geçerli telefon giriniz.");
            RuleFor(s => s.tax_number).MaximumLength(30).When(s => s.tax_number != null);
            // A2-FIX (SUPHELI #21): bu kural musteri kaydindaki kuralin BIREBIR KOPYASIYDI -
            // yani politikanin DORDUNCU kopyasi. Tek merkeze baglandi; DAVRANIS DEGISMEDI
            // (kural zaten ayniydi), yalnizca kopya kalkti. Satici modulu bugun kapali
            // (Seller:RegistrationEnabled=false) ama kopyayi birakmak "TEK MERKEZ" iddiasini
            // bosa dusururdu.
            RuleFor(s => s.password)
                .Must(p => SifrePolitikasi.Gecerli(p))
                .WithMessage(s => SifrePolitikasi.Dogrula(s.password) ?? "");
        }
    }
}
