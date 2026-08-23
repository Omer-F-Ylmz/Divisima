using System.Text;

namespace Divisima.Core.Utilities.Text
{
    // ══ KANIT MASKESI - JETON BENZERI DIZGELER URETILDIGI YERDE KIRPILIR ═══════════════════
    //
    // NEDEN VAR: CLAUDE.md bolum 1'in "depoya yazilan her ornek govdede jeton ilk 8 karaktere
    // kirpilir" kurali UC KEZ KIRILDI ve ucunde de bedeli KIRMIZI BIR RUN oldu (Sprint 8 -
    // Iyzico odeme jetonu; GUVENLIK-FIX-2 - test sifreleri; LAUNCH-FIX Dalga A - ikisi birden).
    // Ortak nokta her seferinde URETIM KODU DEGIL, KANIT YAZMA ANIYDI. Kurali insan disiplinine
    // birakmak calismadi; bu yuzden maskeleme URETIM NOKTASINA tasindi: ham govdeyi ciktiya
    // koyan her yer once buradan gecer, elle kirpmaya GUVENILMEZ.
    //
    // KURAL VERIDEN CIKARILDI, TAHMINLE DEGIL. Aday dizgeler olculdu (Shannon entropi +
    // karakter sinifi) ve su ayrim tutarli cikti:
    //
    //   GORUNUR KALMALI (teshis degeri tasir)        n    entropi  rakam  kucuk
    //     paymentTransactionId                       20   3.746     -      +
    //     email_verification_token                   24   3.637     -      +
    //     InternalServerError                        19   3.076     -      +
    //     DVS20260823-<siparis no>                   22   3.516     +      -
    //
    //   MASKELENMELI (jeton/kimlik)
    //     dogrulama jetonu (base64url)               43   4.897     +      +
    //     sifre sifirlama jetonu (base64url)         43   4.600     +      +
    //     JWT bolumu (eyJhbGci...)                   36   4.417     +      +
    //     Guid("N") - onaltilik                      32   3.480     +      +   <-- ONEMLI
    //
    // ENTROPI TEK BASINA YETMIYOR: Guid("N") 3.480 ile gitleaks'in 3.5 esiginin ALTINDA kalir
    // ama maskelenmesi gerekir. Buna karsilik paymentTransactionId 3.746 ile esigin USTUNDE
    // olmasina ragmen GORUNMELIDIR. Bu yuzden olcut entropi degil KARAKTER SINIFI BILESIMI:
    //
    //     uzunluk >= 16  VE  en az bir RAKAM  VE  en az bir KUCUK HARF
    //
    // Yukaridaki sekiz ornegin SEKIZINI DE dogru siniflandirir. Turkce/Ingilizce tanimlayicilar
    // ve JSON anahtarlari rakam icermedigi icin dokunulmaz; siparis numarasi kucuk harf
    // icermedigi icin dokunulmaz.
    //
    // KIRPMA BICIMI: ilk 8 karakter + tek nokta ucu. Olcum degeri KAYBOLMAZ - bir baglantida
    // origin ve yol gorunur kalir (".../#/dogrula/94-SsO4Z…"), yalniz jetonun kendisi gider.
    public static class KanitMaskesi
    {
        // 8: CLAUDE.md bolum 1'in yazili kirpma uzunlugu. Ayni sayi iki yerde olmasin diye
        // buradan tureyecek; bolum 1 bu sinifa isaret ediyor.
        private const int GorunurOnEk = 8;
        private const int AsgariUzunluk = 16;

        // Jeton benzeri dizgelerde gecebilecek karakterler: base64url (- _), base64 dolgusu
        // (+ =), onaltilik ve nokta (JWT bolum ayraci). Bosluk ve JSON noktalama DISARIDA -
        // boylece "Beklenmeyen bir hata olustu" gibi cumleler tek parca sayilmaz.
        //
        // '/' BILINCLI OLARAK DISARIDA - OLCULDU: iceri alindiginda
        // "http://localhost:5173/#/dogrula/<jeton>" TEK parca sayiliyor ve cikti
        // "http://localhost:5173/#/dogrula…" oluyordu; yani YOL da yutuluyor ve maskenin
        // korumasi gereken TESHIS DEGERI kayboluyordu (pin bunu yakaladi).
        // BEDELI (durust kayit): standart base64 (base64url DEGIL) bir sir '/' karakterlerinde
        // parcalara bolunur. Zarar sinirli - her parca ayri degerlendirilir ve 16+ karakterli,
        // rakam+kucuk harf iceren parcalar YINE maskelenir. Bizim olctugumuz jetonlarin
        // (dogrulama/sifirlama jetonlari, JWT, Guid) hicbiri '/' icermiyor.
        private static bool JetonKarakteri(char c) =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '+' || c == '=' || c == '.';

        /// <summary>
        /// Metindeki jeton benzeri dizeleri ilk 8 karaktere kirpar. null/bos girdi aynen doner.
        /// </summary>
        public static string? Maskele(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return metin;

            var cikti = new StringBuilder(metin.Length);
            int i = 0;
            while (i < metin.Length)
            {
                if (!JetonKarakteri(metin[i])) { cikti.Append(metin[i]); i++; continue; }

                int bas = i;
                while (i < metin.Length && JetonKarakteri(metin[i])) i++;
                var parca = metin.Substring(bas, i - bas);
                cikti.Append(JetonBenzeri(parca) ? parca.Substring(0, GorunurOnEk) + "…" : parca);
            }
            return cikti.ToString();
        }

        // Olcut yukarida gerekcelendirildi: uzunluk + rakam + kucuk harf.
        private static bool JetonBenzeri(string parca)
        {
            if (parca.Length < AsgariUzunluk) return false;
            bool rakam = false, kucuk = false;
            foreach (var c in parca)
            {
                if (char.IsDigit(c)) rakam = true;
                else if (char.IsLower(c)) kucuk = true;
                if (rakam && kucuk) return true;
            }
            return false;
        }
    }
}
