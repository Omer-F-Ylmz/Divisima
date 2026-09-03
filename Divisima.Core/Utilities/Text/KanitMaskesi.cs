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
    //
    // ── GF-3 / K1: IKINCI KURAL - E-POSTA (KVKK) ────────────────────────────────────────────
    // Yukaridaki karakter-sinifi olcutu e-postayi KACIRIYORDU ve bu OLCULDU: '@' ayrac oldugu
    // icin "omer@example.com" iki parcaya boluniyor, iki parca da 16 esiginin altinda kaliyor
    // ve adres log'a DUZ gidiyordu (AV-1/E-3: SmtpMailService.cs:42 ve :81'de musteri
    // e-postasi, KVKK). Cozum ayri bir yardimci DEGIL, ayni yardimcinin IKINCI DALI:
    //
    //     e-posta  ->  ilk 2 karakter + "***@" + alan adi     ("om***@example.com")
    //     jeton    ->  ilk 8 karakter + "…"                    (degismedi)
    //
    // Iki dal AYRI olcut kullanir cunku AYRI seyi korur: jeton olcutu SIRRI gizler, e-posta
    // olcutu KIMLIGI gizler. Alan adi GORUNUR KALIR - bir operatorun "hangi saglayici"
    // sorusunu yanitlar ve kimlik acmaz.
    public static class KanitMaskesi
    {
        // 8: CLAUDE.md bolum 1'in yazili kirpma uzunlugu. Ayni sayi iki yerde olmasin diye
        // buradan tureyecek; bolum 1 bu sinifa isaret ediyor.
        private const int GorunurOnEk = 8;
        private const int AsgariUzunluk = 16;

        // GF-3/K1 - e-postada gorunur birakilan yerel-kisim uzunlugu (merkez karari:
        // "ilk 2 karakter + *** + @alan"). Jetondaki 8'den AYRI bir sayidir ve AYRI
        // gerekcesi vardir: jetonda 8 karakter TESHIS icin gerekir (hangi jeton oldugunu
        // ayirt eder), e-postada 2 karakter bir operatorun "hangi hesap" sorusunu destek
        // kaydiyla eslestirmesine yeter ve daha fazlasi adresi TAHMIN EDILEBILIR kilar.
        private const int EPostaOnEk = 2;

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
        // GF-3/K1 - '@' EKLENDI. ONCEDEN ayracti; bu yuzden "omer@example.com" IKI parcaya
        // ("omer" 4 karakter · "example.com" 11 karakter) boluniyor, IKISI DE 16 esiginin
        // ALTINDA kaliyor ve e-posta HIC MASKELENMIYORDU (olculdu - GF-3 on olcum A).
        // '@' iceri alininca adres TEK parca olur ve asagidaki E-POSTA DALI onu yakalar.
        // MEVCUT PINLERE ETKISI YOK - olculdu: KanitMaskesiTests.cs icinde '@' 0 kez geciyor
        // (POZ kontrol: ayni dosyada "Maskele" 7 gecis · NEG kontrol: "zzz@" 0 gecis). '@'
        // tasimayan hicbir girdinin parcalanmasi degismedigi icin pinli sekiz ornek AYNEN kalir.
        private static bool JetonKarakteri(char c) =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '+' || c == '=' || c == '.'
            || c == '@';

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
                cikti.Append(ParcayiMaskele(parca));
            }
            return cikti.ToString();
        }

        /// <summary>
        /// Satir sonu ve diger kontrol karakterlerini TEK bosluga indirir. Log satirinin
        /// PARCALANMASINI (log forging) ve posta basligina satir enjeksiyonunu engeller.
        /// </summary>
        // GF-3/K4 (AV-1 bulgusu A-3) - NEDEN BURADA: bir Subject degeri IKI yere birden gidiyor -
        // MimeKit basligina ve `SmtpMailService`in log satirina ("... -> {To} | {Subject}").
        // MimeKit basligi serilestirirken kodladigi icin posta-basligi yarisi SUPHE olarak
        // kaldi; ama Serilog mesaj sablonu CRLF'i AYIKLAMAZ, dolayisiyla LOG yarisi
        // OLCULEBILIR bir kusurdur: Subject'e "\r\n" giren bir deger dosya sink'inde SAHTE
        // BIR LOG SATIRI yazar. Iki yarinin da TEK yerden gecmesi icin ayrac temizligi
        // maskeyle AYNI yardimciya konuldu (merkez karari: "aynı yardımcıdan").
        public static string? SatirGuvenli(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return metin;

            var cikti = new StringBuilder(metin.Length);
            bool oncekiBosluk = false;
            foreach (var c in metin)
            {
                // Ardisik kontrol karakterleri TEK bosluga KATLANIR: "Konu\r\n\r\nsahte" tek
                // satirda kalir ve okunabilirlik de bozulmaz.
                if (char.IsControl(c))
                {
                    if (!oncekiBosluk) { cikti.Append(' '); oncekiBosluk = true; }
                    continue;
                }
                cikti.Append(c);
                oncekiBosluk = false;
            }
            return cikti.ToString().Trim();
        }

        // Parca sinifi: ONCE e-posta, SONRA jeton. Sira ONEMLI - e-posta dali ON-GECIS
        // niteligindedir ve ciktisi ("om***@example.com") jeton kuralina TEKRAR SOKULMAZ.
        // Yoksa rakam tasiyan bir alan adi (ornegin "mail3.example.com", 17 karakter, rakam +
        // kucuk harf) ikinci kez kirpilir ve maskenin korumasi gereken TESHIS DEGERI - hangi
        // saglayiciya gidildigi - gereksiz yere kaybolurdu.
        private static string ParcayiMaskele(string parca)
        {
            if (EPostaMi(parca, out var yerel, out var alan))
            {
                // ETIKET AYRIMI - PIN YAKALADI (GF-3): '=' bir JETON KARAKTERI oldugu icin
                // "to=omer@example.com" TEK parca sayilir ve etiket yerel kismin ICINE girer;
                // "ilk 2 karakter" o zaman adresin degil ETIKETIN ilk 2'si olurdu ("to***@...").
                // Guvenlik yine saglanirdi (gercek yerel kisim yine gizlenir) ama TESHIS
                // YANILTICI olurdu. Bu yuzden son '=' isaretine kadar olan kisim ONEK sayilir
                // ve AYNEN korunur: "to=om***@example.com".
                // Ayni tuzagin jeton hali icin bkz. IyzicoClient cagri yerindeki not.
                var esit = yerel.LastIndexOf('=');
                var onek = esit >= 0 ? yerel.Substring(0, esit + 1) : "";
                var gercekYerel = esit >= 0 ? yerel.Substring(esit + 1) : yerel;

                return onek
                       + (gercekYerel.Length <= EPostaOnEk
                            ? gercekYerel
                            : gercekYerel.Substring(0, EPostaOnEk))
                       + "***@" + alan;
            }

            // ETIKET AYRIMI JETON DALINA UYGULANMAZ - BILINCLI ASIMETRI, GEREKCESI OLCULDU:
            // e-postada '@' yapiyi GARANTI eder, jetonda ise '=' cogu zaman BASE64 DOLGUSUDUR
            // ve dizgenin SONUNDA durur. "son '=' isaretinden bol" kurali jeton dalina
            // konsaydi "abcd==" gibi bir jetonda onek TUM DIZGE olur, kirpilacak kisim BOS
            // kalir ve jeton OLDUGU GIBI SIZARDI. Bu yuzden jeton dali sablonu ayirmaz.
            // BEDELI (durust kayit): "token=<jeton>" TEK parca sayilir ve cikti "token=94…"
            // olur - yani etiket kirpmanin icine girer ve jetonun ilk 8'i yerine ilk 2'si
            // gorunur. GUVENLIK ETKISI YOK (daha AZ sizar), yalniz teshis degeri azalir.
            // Cozum cagri yerindedir: maskeye SABLON DEGIL DEGER gecilir (bkz. IyzicoClient).
            return JetonBenzeri(parca) ? parca.Substring(0, GorunurOnEk) + "…" : parca;
        }

        // E-POSTA OLCUTU BILINCLI OLARAK DAR DEGIL, ZAYIF: bu bir dogrulayici DEGIL, bir
        // MASKELEME KAPISIDIR. Yanlis pozitifin bedeli teshis kaybi, yanlis negatifin bedeli
        // KVKK ihlali - bu yuzden olcut "tek @ · dolu yerel kisim · noktali alan" gibi genis
        // tutuldu. Kural '@' capasina baglidir, "email" SOZCUGUNE DEGIL: sozcuge baglansaydi
        // pinli "email_verification_token" ornegi (rakam YOK, gorunur KALMALI) kirilirdi.
        private static bool EPostaMi(string parca, out string yerel, out string alan)
        {
            yerel = ""; alan = "";
            int at = parca.IndexOf('@');
            // at <= 0 -> '@' yok ya da basta ("@RequireUserType" gibi oznitelikler burada elenir).
            // Ikinci bir '@' varsa e-posta saymayiz; parca jeton kuralina duser.
            if (at <= 0 || at != parca.LastIndexOf('@')) return false;

            yerel = parca.Substring(0, at);
            alan = parca.Substring(at + 1);
            // Alan adi en az bir nokta icermeli, nokta bas ya da son OLMAMALI:
            // "a@b" ve "a@b." e-posta SAYILMAZ.
            int nokta = alan.IndexOf('.');
            return nokta > 0 && nokta < alan.Length - 1;
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
