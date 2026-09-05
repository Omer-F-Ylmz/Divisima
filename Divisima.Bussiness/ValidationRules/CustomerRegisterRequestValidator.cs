using Divisima.Core.Security;
using Divisima.Core.Utilities.Validation;
using Divisima.Entity.Dtos.Auth;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // Açıklayıcı yorum: Müşteri kayıt validasyonu + ŞİFRE POLİTİKASI (min 8, büyük/küçük harf + rakam).
    //
    // GF-5 / K4: `name` uzunlugu ve telefon deseni `GirdiSinirlari`dan okunuyor - DEGERLER
    // AYNI (100 ve ayni regex), davranis DEGISMEDI. Misafir yolunun `guest_name` siniri de
    // ayni `MusteriAdi` sabitine baglandi: iki yol artik tek degere bakar.
    // Mesaj metni TASINMADI ("Gecerli telefon giriniz." adres ucundakinden FARKLI ve oyle
    // kaliyor - metin birlestirmesi bu dalganin kapsami degil, VITRIN-KALAN 3'te kayitli).
    public class CustomerRegisterRequestValidator : AbstractValidator<CustomerRegisterRequestDto>
    {
        public CustomerRegisterRequestValidator()
        {
            RuleFor(c => c.name).NotEmpty().WithMessage("Ad boş olamaz.").MaximumLength(GirdiSinirlari.MusteriAdi);
            // GF-5 / F4 (C-2): uzunluk sınırı EKLENDİ - `customers.email` kolonu 200 karakter
            // ve bugüne kadar HİÇBİR yolda uzunluk kontrolü yoktu (201+ karakter HTTP 500).
            // Misafir yolu AYNI sabite bakar.
            RuleFor(c => c.email).NotEmpty().EmailAddress().WithMessage("Geçerli bir e-posta giriniz.")
                .MaximumLength(GirdiSinirlari.EPosta)
                    .WithMessage($"E-posta en fazla {GirdiSinirlari.EPosta} karakter olabilir.");
            RuleFor(c => c.phone).NotEmpty().Matches(GirdiSinirlari.TelefonDeseni).WithMessage("Geçerli telefon giriniz.");
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
