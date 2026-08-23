using Divisima.Core.Security;
using Divisima.Entity.Dtos.Auth;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Müşteri kayıt validasyonu + ŞİFRE POLİTİKASI (min 8, büyük/küçük harf + rakam).
    public class CustomerRegisterRequestValidator : AbstractValidator<CustomerRegisterRequestDto>
    {
        public CustomerRegisterRequestValidator()
        {
            RuleFor(c => c.name).NotEmpty().WithMessage("Ad boş olamaz.").MaximumLength(100);
            RuleFor(c => c.email).NotEmpty().EmailAddress().WithMessage("Geçerli bir e-posta giriniz.");
            RuleFor(c => c.phone).NotEmpty().Matches(@"^[0-9+\s()-]{7,20}$").WithMessage("Geçerli telefon giriniz.");
            // A2-FIX (SUPHELI #21): kural ARTIK BURADA TANIMLI DEGIL - tek merkez
            // Divisima.Core.Security.SifrePolitikasi. Ayni kural dort ayri yerde kopyalanmisti
            // ve en gevsek kopya (reset-password: HIC) en kolay ulasilan yoldu.
            // Ozel mesajlar KORUNUYOR: Dogrula() ihlal edilen ILK kuralin mesajini doner.
            RuleFor(c => c.password)
                .Must(p => SifrePolitikasi.Gecerli(p))
                .WithMessage(c => SifrePolitikasi.Dogrula(c.password) ?? "");
        }
    }
}
