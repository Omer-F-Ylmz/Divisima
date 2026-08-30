using Divisima.Entity.Dtos.Guest;
using FluentValidation;

namespace Divisima.Bussiness.ValidationRules.FluentValidation
{
    // ══ MANTIK-FIX-3 / K5 - MISAFIR YOLUNUN ADRES GIRDI SINIFI ═════════════════════════
    //
    // OLCULEN ONCE-DURUM: adresi YAZAN IKI YOL VAR ve yalniz BIRI dogrulaniyordu.
    //   uye yolu     -> AddressRequestDto  -> AddressRequestValidator (VAR)
    //   misafir yolu -> GuestCheckoutDto   -> validator YOKTU
    // Sonuc canli veride gorunuyor: telefonu BOS 8 adres, hic rakam tasimayan telefon 1,
    // sehri BOS 1, ilcesi BOS 1. Bos sehir/ilce tasiyan bir adres TESLIM EDILEMEZ; yani
    // eksik dogrulama musteriye "siparisin alindi" deyip kargoyu imkansiz kiliyordu.
    //
    // KAPSAM BILINCLI OLARAK DAR - YALNIZ ADRES GIRDI SINIFI:
    // guest_name / guest_email / items / payment_method / coupon_code kurallari
    // GuestCheckoutManager'in KENDI bolgesinde ZATEN var (:68 :70 :72 :79) ve buraya
    // KOPYALANMAZ - bu depoda "ayni kuralin ikinci kopyasi" sinifinin bedeli YEDI KEZ
    // odendi. Burada YALNIZCA bugun HICBIR YERDE dogrulanmayan alanlar var.
    //
    // KURALLAR UYE YOLUYLA BIREBIR AYNI (AddressRequestValidator): yeni politika ICAT
    // EDILMEDI, var olan sozlesme ikinci yola TASINDI. Mesajlar da ayni - iki yol
    // musteriye AYNI dili konusur.
    public class GuestCheckoutValidator : AbstractValidator<GuestCheckoutDto>
    {
        public GuestCheckoutValidator()
        {
            RuleFor(x => x.guest_phone).NotEmpty().WithMessage("Telefon boş olamaz.")
                .Matches(@"^[0-9+\s()-]{7,20}$").WithMessage("Geçerli bir telefon girin.");
            RuleFor(x => x.city).NotEmpty().WithMessage("Şehir boş olamaz.").MaximumLength(50);
            RuleFor(x => x.district).NotEmpty().WithMessage("İlçe boş olamaz.").MaximumLength(50);
            RuleFor(x => x.full_address).NotEmpty().WithMessage("Açık adres boş olamaz.").MaximumLength(500);
            RuleFor(x => x.zip_code).MaximumLength(10).When(x => x.zip_code != null);
        }
    }
}
