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
            // Açıklayıcı yorum: Şifre politikası - kaba kuvvete karşı güçlü şifre zorunluluğu
            RuleFor(c => c.password)
                .NotEmpty().WithMessage("Şifre boş olamaz.")
                .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalı.")
                .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermeli.")
                .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermeli.")
                .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermeli.");
        }
    }
}
