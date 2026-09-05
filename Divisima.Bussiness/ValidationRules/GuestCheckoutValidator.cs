using Divisima.Core.Utilities.Sanitization;
using Divisima.Core.Utilities.Validation;
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
    // ══ GF-5 / K4 - YUKARIDAKI KAPSAM CUMLESI DARALTILDI (bilincli, merkez karari) ══════
    // "guest_name ... buraya KOPYALANMAZ" satiri VARLIK kurallari icindi ve DOGRUYDU:
    // NotEmpty hala YALNIZ manager'da (GuestCheckoutManager.cs). Ama guest_name'in
    // UZUNLUGU o gun HICBIR YERDE dogrulanmiyordu ve AV-2 bunu LAUNCH BLOKER olarak olctu
    // (SD-7 `[VERI-BOZAN]`): 151 karakterlik ad -> EF insert-time 500, musteri satiri ZATEN
    // yazilmis -> YETIM MUSTERI (canli: id 179) + o e-postanin KALICI 409'u. Yani burasi
    // "bugun HICBIR YERDE dogrulanmayan alanlar" tanimina TAM OLARAK giriyor - kural
    // kopyalanmadi, EKSIK OLAN kural eklendi. Ikinci kopya SAYACI ARTMADI.
    //
    // UZUNLUK SANITIZE SONRASI OLCULUR (merkez karari, GF-5 / D3): DB'ye giden deger
    // `InputSanitizer.Sanitize(dto.guest_name.Trim())` sonucudur (GuestCheckoutManager.cs
    // ve adres yaziminda), ham girdi DEGIL. OLCULEN YON - KAYDA GECER: `Sanitize` HTML-ENCODE ETMEZ
    // (o ayri bir metottur, `HtmlEncode`, ve bu yolda CAGRILMIYOR); govdesi bes adet
    // `Replace(..., "")` + `Trim()`, yani `Sanitize(x).Length <= x.Length` HER ZAMAN.
    // Dolayisiyla ham uzerinden olcmek TASMA URETMEZDI - yalnizca sigacak bir degeri
    // gereksiz yere reddederdi. Sanitize sonrasi olcmek DOGRU degeri sinar; bu bir
    // 500 savunmasi degil, DOGRULUK duzeltmesidir.
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
            // GF-5 / K4 (SD-7): guest_name UZUNLUGU. NotEmpty BURAYA EKLENMEDI - o kural
            // manager'da (GuestCheckoutManager.cs) ve oradaki mesaj musteriye donuyor;
            // ikinci kopya acilmaz. Bos/whitespace burada GECER, manager reddeder.
            // Olcum SANITIZE SONRASI: DB'ye giden deger budur (gerekce sinif yorumunda).
            RuleFor(x => x.guest_name)
                .Must(ad => string.IsNullOrWhiteSpace(ad)
                            || InputSanitizer.Sanitize(ad.Trim()).Length <= GirdiSinirlari.MusteriAdi)
                .WithMessage($"Ad soyad en fazla {GirdiSinirlari.MusteriAdi} karakter olabilir.");

            // GF-5 / F4 (C-2): guest_email UZUNLUGU. Bicim kontrolu (`@` iceriyor mu)
            // manager'da (GuestCheckoutManager, PlaceGuestOrder girisi) ve ORASI DEGISMEDI -
            // ikinci kopya acilmiyor. Buraya YALNIZ bugune kadar HICBIR YERDE olculmeyen
            // UZUNLUK giriyor; sabit uye kayit ucuyla AYNI (kolon 200).
            RuleFor(x => x.guest_email)
                .MaximumLength(GirdiSinirlari.EPosta)
                    .WithMessage($"E-posta en fazla {GirdiSinirlari.EPosta} karakter olabilir.")
                .When(x => !string.IsNullOrWhiteSpace(x.guest_email));

            RuleFor(x => x.guest_phone).NotEmpty().WithMessage("Telefon boş olamaz.")
                .Matches(GirdiSinirlari.TelefonDeseni).WithMessage("Geçerli bir telefon girin.");
            RuleFor(x => x.city).NotEmpty().WithMessage("Şehir boş olamaz.").MaximumLength(GirdiSinirlari.Sehir);
            RuleFor(x => x.district).NotEmpty().WithMessage("İlçe boş olamaz.").MaximumLength(GirdiSinirlari.Ilce);
            RuleFor(x => x.full_address).NotEmpty().WithMessage("Açık adres boş olamaz.").MaximumLength(GirdiSinirlari.AcikAdres);
            RuleFor(x => x.zip_code).MaximumLength(GirdiSinirlari.PostaKodu).When(x => x.zip_code != null);

            // GF-5 / K4 (D2): request_id TASIYICI kapisi - BICIM kapisi DEGIL.
            // `orders.request_id` NVARCHAR(80); 81 karakter guest_name ile AYNI ailenin
            // insert-time 500'unu uretir. GUID SARTI YOK - gerekce GirdiSinirlari'nda
            // (olculdu: dolu 122 degerin 54'u GUID degil ve o bicim CANLI; ayrica
            // frontend'in yedek dali "co-..." uretiyor ve o dal PINLI + DOKUNULMAZ).
            RuleFor(x => x.request_id)
                .MaximumLength(GirdiSinirlari.RequestIdEnUzun)
                    .WithMessage($"İstek kimliği en fazla {GirdiSinirlari.RequestIdEnUzun} karakter olabilir.")
                .Matches(GirdiSinirlari.RequestIdDeseni)
                    .WithMessage("İstek kimliği yalnızca harf, rakam, nokta, alt tire ve tire içerebilir.")
                .When(x => !string.IsNullOrWhiteSpace(x.request_id));
        }
    }
}
