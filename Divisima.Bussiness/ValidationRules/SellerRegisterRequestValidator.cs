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
            RuleFor(s => s.password)
                .NotEmpty().WithMessage("Şifre boş olamaz.")
                .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalı.")
                .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermeli.")
                .Matches("[a-z]").WithMessage("Şifre en az bir küçük harf içermeli.")
                .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermeli.");
        }
    }
}
