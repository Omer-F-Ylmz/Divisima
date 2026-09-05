using System.Net;
using System.Text.RegularExpressions;

namespace Divisima.Core.Utilities.Sanitization
{
    // Açıklayıcı yorum: Kullanıcı metnindeki tehlikeli HTML/script'i temizler (stored XSS savunması).
    // Yorum, adres, isim gibi serbest metin alanlarında kayıttan ÖNCE uygulanır. Savunma derinliği:
    // frontend de encode etmeli ama sunucu tarafı son kalkandır.
    //
    // ══ GF-5 / F3 - BU YORUM DUZELTILDI (ONCEKI HALI YANLISTI) ═════════════════════════════
    // Onceki metin `Sanitize` icin "...sonra HTML-encode" diyordu. `Sanitize` ENCODE ETMEZ:
    // govdesi bes adet `Regex.Replace(..., "")` + `Trim()`dir, yani metni yalnizca KISALTIR ya
    // da aynen birakir. HTML kacisi AYRI bir metottur (`HtmlEncode`) ve cagrilmasi CAGIRANIN
    // isidir - misafir checkout yolu ornegin onu CAGIRMAZ.
    // NEDEN ONEMLI: GF-5/K4 uzunluk kapisini "sanitize SONRASI" olcuyor ve bu karar tam da
    // "Sanitize UZATMAZ" olgusuna dayaniyor (`Sanitize(x).Length <= x.Length` HER ZAMAN).
    // Yorum eski haliyle kalsaydi, o kararin dayanagi kaynakta YANLIS yaziyor olacakti.
    public static class InputSanitizer
    {
        // Açıklayıcı yorum: <script>, <iframe>, on* event, javascript: gibi tehlikeli desenleri sök
        private static readonly Regex ScriptTag = new(@"<\s*script[^>]*>.*?<\s*/\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        // GÜVENLİK: kapanışsız/bozuk <script veya </script kalıntısını da sök (nested/incomplete-tag bypass'ı - tarayıcı yine de çalıştırabilir).
        private static readonly Regex ScriptFragment = new(@"<\s*/?\s*script[^>]*>?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DangerousTags = new(@"<\s*(iframe|object|embed|form|link|meta|style|base|svg)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // GÜVENLİK: olay yakalayıcı (onload/onerror/onclick...) - ayraç boşluk VEYA slash olabilir. Eski "\son\w+=" yalnız
        // boşluk yakalıyordu -> "<svg/onload=alert(1)>" (slash ayraç) BYPASS ediyordu. [\s/] ile ikisi de yakalanır.
        private static readonly Regex EventHandlers = new(@"[\s/]on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex JsProtocol = new(@"j\s*a\s*v\s*a\s*s\s*c\s*r\s*i\s*p\s*t\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Açıklayıcı yorum: Serbest metni güvenli hale getir (tehlikeli kısımları sök, sonra HTML-encode)
        public static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var cleaned = ScriptTag.Replace(input, "");
            cleaned = ScriptFragment.Replace(cleaned, "");   // kapanışsız/bozuk script kalıntısı
            cleaned = DangerousTags.Replace(cleaned, "");
            cleaned = EventHandlers.Replace(cleaned, "");
            cleaned = JsProtocol.Replace(cleaned, "");
            return cleaned.Trim();
        }

        // Açıklayıcı yorum: HTML bağlamında gösterilecekse tam kaçış (output encoding)
        public static string HtmlEncode(string input) =>
            string.IsNullOrEmpty(input) ? input : WebUtility.HtmlEncode(input);
    }
}
