using System.Text.RegularExpressions;

namespace Divisima.Core.Utilities.Logging
{
    // Açıklayıcı yorum: Log'a yazılmadan önce hassas veriyi maskeler (savunma derinliği).
    // Kart no/CVC zaten sunucuya gelmiyor ama token/imza gibi alanlar log'da maskelenir.
    public static class SensitiveDataMask
    {
        private static readonly Regex CardLike = new(@"\b\d{13,19}\b", RegexOptions.Compiled);

        public static string Mask(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            input = CardLike.Replace(input, m => new string('*', Math.Max(0, m.Value.Length - 4)) + m.Value[^4..]);
            return input;
        }

        public static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length <= 6) return "***";
            return token[..6] + "***";
        }
    }
}
