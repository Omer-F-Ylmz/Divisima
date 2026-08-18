using FluentValidation;
using Divisima.Entity.Dtos.Product;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Ürün ekleme validasyonu (fiyat, renk hex, indirim mantığı).
    public class ProductAddRequestValidator : AbstractValidator<ProductAddRequestDto>
    {
        public ProductAddRequestValidator()
        {
            RuleFor(p => p.name).NotEmpty().WithMessage("Ürün adı boş olamaz.").MaximumLength(200);
            RuleFor(p => p.brand).NotEmpty().WithMessage("Marka boş olamaz.").MaximumLength(120);
            RuleFor(p => p.category_id).GreaterThan(0).WithMessage("Kategori gerekli.");
            RuleFor(p => p.price).GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalı.");
            RuleFor(p => p.color_hex).Matches("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")
                .When(p => !string.IsNullOrEmpty(p.color_hex))
                .WithMessage("Renk geçerli hex formatında olmalı (#RRGGBB).");
            // Açıklayıcı yorum: İndirimli fiyat (old_price) satış fiyatından büyük olmalı
            RuleFor(p => p.old_price).GreaterThan(p => p.price)
                .When(p => p.old_price.HasValue)
                .WithMessage("İndirim öncesi fiyat, satış fiyatından büyük olmalı.");
        }
    }
}
