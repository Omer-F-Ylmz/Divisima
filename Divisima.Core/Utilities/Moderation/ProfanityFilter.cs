using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Divisima.Core.Utilities.Moderation
{
    // Açıklayıcı yorum: Basit küfür/uygunsuz içerik filtresi. Kelime listesi maskeler (yorum/Q&A için).
    // Üretimde harici moderasyon servisi (Perspective API vb.) ile değiştirilebilir.
    public static class ProfanityFilter
    {
        // Açıklayıcı yorum: Örnek liste - üretimde genişletilir/dış kaynaktan yüklenir
        private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "amk", "aq", "orospu", "piç", "sik", "yavşak", "gavat", "pezevenk", "salak", "aptal", "mal"
        };

        // Açıklayıcı yorum: İçerik uygunsuz kelime içeriyor mu
        public static bool ContainsProfanity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var words = Regex.Split(text.ToLowerInvariant(), @"[^\wçğıöşü]+");
            return words.Any(w => Blacklist.Contains(w));
        }

        // Açıklayıcı yorum: Uygunsuz kelimeleri yıldızla maskele
        public static string Mask(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return Regex.Replace(text, @"[\wçğıöşü]+", m =>
                Blacklist.Contains(m.Value) ? new string('*', m.Value.Length) : m.Value);
        }
    }
}
