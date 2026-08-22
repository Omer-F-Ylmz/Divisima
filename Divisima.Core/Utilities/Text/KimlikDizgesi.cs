using System.Text;

namespace Divisima.Core.Utilities.Text
{
    // ══ KIMLIK DIZGELERI ICIN KANONIKLESTIRME - KALITE SUPURMESI DALGA 1 ═══════════════════
    //
    // KOK ILKE: kimlik/makine dizgelerinde casing ve karsilastirma KULTURDEN BAGIMSIZ olmalidir.
    // Kultur YALNIZ insan-gorunur bicimlendirmede kullanilir (fatura tutari, tarih).
    //
    // Cogu kimlik dizgesi icin `ToLowerInvariant` / `ToUpperInvariant` YETER (e-posta, MIME tipi,
    // URL yolu). Bu sinif, YETMEDIGI tek durum icin vardir: KULLANICININ ELLE YAZDIGI kodlar.
    //
    // NEDEN AYRI BIR ADIM GEREKIYOR (olculdu):
    // Turkce klavyede buyuk harf 'i' -> 'İ' (U+0130), kucuk harf 'I' -> 'ı' (U+0131). Bunlar
    // ASCII 'I'/'i' ile AYNI KARAKTER DEGIL ve invariant casing onlari BIRBIRINE CEVIRMEZ:
    //     'İNDİRİM10'.ToUpperInvariant() -> 'İNDİRİM10'   (degismez)
    //     'INDIRIM10'.ToUpperInvariant() -> 'INDIRIM10'
    // Veritabani collation'i Turkish_CI_AS'te de bu ikisi FARKLI'dir (cift'ler I<->ı, İ<->i).
    // Yani musteri, kupon kodunu Turkce klavyede BUYUK harfle yazdiginda ('İNDİRİM10') hicbir
    // sey eslesmezdi - ustelik ekranda kodu DOGRU yazmis gorunurken.
    //
    // KAPSAM BILEREK DAR: bu katlama YALNIZ pazarlama/promosyon kodlarina uygulanir. Bunlar
    // ASCII olmasi beklenen, basili/paylasilan ve ELLE YAZILAN tanimlayicilardir.
    // E-POSTAYA UYGULANMAZ: e-posta kullanicinin KENDI kimligidir; oradaki bir karakteri
    // sessizce degistirmek kimlik verisini yeniden yazmak olur. (E-postayi Turkce klavyede
    // buyuk yazan kullanicinin durumu AYRI bir bulgu olarak deftere yazildi.)
    public static class KimlikDizgesi
    {
        // Turkce'ye ozgu harfleri ASCII karsiliklarina katlar, sonra invariant BUYUK harfe cevirir.
        // Sonuc: 'indirim10' / 'INDIRIM10' / 'İNDİRİM10' / 'ındırım10' -> hepsi 'INDIRIM10'.
        public static string KanonikKod(string? deger)
        {
            if (string.IsNullOrWhiteSpace(deger)) return "";

            var sb = new StringBuilder(deger.Length);
            foreach (var ch in deger.Trim())
            {
                sb.Append(ch switch
                {
                    'İ' => 'I',   // U+0130 - Turkce buyuk noktali I
                    'ı' => 'i',   // U+0131 - Turkce kucuk noktasiz i
                    _ => ch
                });
            }
            return sb.ToString().ToUpperInvariant();
        }
    }
}
