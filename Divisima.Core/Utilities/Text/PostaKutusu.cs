using System;

namespace Divisima.Core.Utilities.Text
{
    // ══ GUVENLIK-FIX-4 - KANONIK POSTA KUTUSU (YALNIZCA KOTUYE KULLANIM SAYACI ICIN) ══════
    //
    // BU BIR KIMLIK NORMALIZASYONU DEGILDIR - ve bilincli olarak `KimlikDizgesi`nin ICINE
    // KONMADI. O dosya "E-POSTAYA UYGULANMAZ" diye ACIKCA sinir koyuyor ve gerekcesi orada
    // yazili: e-posta kullanicinin KENDI kimligidir, oradaki karakteri sessizce degistirmek
    // kimlik verisini yeniden yazmak olur. O sinir AYNEN gecerlidir.
    //
    // BURADAKI DONUSUM YALNIZ BIR SAYAC EKSENIDIR:
    //   - hesap kimligi DEGISMEZ  (musteri satirlari RFC'ye uygun sekilde AYRI kalir)
    //   - `customers.email` DEGISMEZ
    //   - misafir checkout'un "bu e-posta kayitli" (409) semantigi DEGISMEZ
    // Yalnizca "ayni posta KUTUSUNA kac acik misafir siparisi yigildi" sorusu bu eksende
    // sorulur.
    //
    // OLCULEN GEREKCE (GUVENLIK-FIX-4): `+etiket` varyanti mevcut 409 guard'ini ASIYOR ve
    // ayni fiziksel kutuya yigiliyor -
    //   kurban@example.com    -> 201 (siparis 181)
    //   kurban+a@example.com  -> 201 (siparis 182)     <- AYNI KUTU
    //   KURBAN@example.com    -> 409                   <- Dalga 1 kanoniklestirmesi TUTUYOR
    // Yani buyuk/kucuk harf ekseni ZATEN kapaliydi, `+etiket` ekseni ACIKTI.
    //
    // NOKTA SIYRILMAZ - BILINCLI SINIR: bazi saglayicilar (Gmail) yerel kisimdaki noktayi
    // yok sayar, COGU saglayici SAYMAZ. Nokta siyirmak, saglayici bazli bir varsayimi TUM
    // adreslere uygulamak ve `a.b@x` ile `ab@x`i AYNI kisi saymak olurdu - farkli iki
    // musteriyi birbirinin esigine yazan bir YANLIS POZITIF. Bilinen sinir olarak kayitli.
    public static class PostaKutusu
    {
        // "Local+etiket@Domain" -> "local@domain".  Cozumlenemeyen girdi kirpilip
        // KUCULTULEREK oldugu gibi doner (sessizce bir sey UYDURULMAZ).
        public static string Kanonik(string? eposta)
        {
            var deger = (eposta ?? string.Empty).Trim();
            if (deger.Length == 0) return string.Empty;

            var at = deger.LastIndexOf('@');
            if (at <= 0 || at == deger.Length - 1) return deger.ToLowerInvariant();

            var yerel = deger.Substring(0, at);
            var alan = deger.Substring(at + 1);

            var arti = yerel.IndexOf('+');
            // `arti == 0` (adres '+' ile BASLIYOR) durumunda yerel kisim BOSALIRDI ve butun
            // boyle adresler tek kovaya duserdi - o yuzden yalniz '+' SONRASI bir sey varken
            // ve oncesinde de bir sey varken kirpilir.
            if (arti > 0) yerel = yerel.Substring(0, arti);

            // KULTURSUZ: bu bir KIMLIK dizgesidir (CLAUDE.md bolum 6c) - kulturlu casing
            // ayni degerin iki yazimindan iki farkli anahtar uretirdi.
            return (yerel + "@" + alan).ToLowerInvariant();
        }
    }
}
