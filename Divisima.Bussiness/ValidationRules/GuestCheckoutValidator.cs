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
    // EDILMEDI. Mesajlar da ayni - iki yol musteriye AYNI dili konusur.
    //
    // MK-4b DENETIM DUZELTMESI (ITIRAZ-3) - BU YORUM ONCE "TASINDI" DIYORDU, YANLISTI:
    // sozlesme TASINMADI, **KOPYALANDI**. Denetci olctu ve kabul edildi:
    //   AddressRequestValidator · CustomerRegisterRequestValidator ·
    //   SellerRegisterRequestValidator · (K5 ile) GuestCheckoutValidator
    // -> telefon regex'i ARTIK DORT YERDE. Sinif K5 ile DOGMADI (uc kopya zaten vardi),
    // K5 sayiyi 3 -> 4 yapti.
    // BUGUN AKTIF KUSUR YOK: dort kopya arasindaki ayrisma OLCULDU ve SIFIR.
    // LATENT RISK: bu kopyalari koruyan HICBIR TARAMA PINI YOK - karsilastirma icin
    // sifre politikasinin `HICBIR_UC_KENDI_SIFRE_KURALINI_TANIMLAMAZ` sinif-duzeyi
    // tarama pini VAR ve besinci kopyada KIRILIR. Yarin dort kopyadan biri degisirse
    // iki yol musteriye FARKLI kural anlatir ve hicbir sey kirilmaz.
    // KALICI COZUM ADAYLARI (KARAR MERKEZIN, bu dalgada UYGULANMADI): ortak bir
    // RuleBuilder uzantisi (TelefonKurali() / AdresKurallari()) ya da sifre kalibindaki
    // gibi bir sinif-duzeyi tarama pini.
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
