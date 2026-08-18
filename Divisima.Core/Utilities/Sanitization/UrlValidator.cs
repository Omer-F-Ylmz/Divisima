using System.Net;

namespace Divisima.Core.Utilities.Sanitization
{
    // Açıklayıcı yorum: Kullanıcı tarafından verilen URL'leri SSRF'e karşı doğrular (ör. ödeme callback_url).
    // İç ağ (private IP), localhost, cloud metadata (169.254.169.254) ve http (yalnız https) reddedilir.
    public static class UrlValidator
    {
        public static bool IsSafePublicHttpsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

            // Açıklayıcı yorum: Yalnız HTTPS
            if (uri.Scheme != Uri.UriSchemeHttps) return false;

            var host = uri.Host;
            // Açıklayıcı yorum: localhost / loopback engeli
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;

            if (IPAddress.TryParse(host, out var ip))
            {
                // Açıklayıcı yorum: IP verilmişse iç ağ / metadata engeli
                if (IsPrivateOrReserved(ip)) return false;
            }
            return true;
        }

        // Açıklayıcı yorum: Özel/rezerve IP aralıkları (RFC1918 + loopback + link-local + metadata)
        private static bool IsPrivateOrReserved(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            if (IPAddress.IsLoopback(ip)) return true;
            if (b.Length == 4)
            {
                // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16 (link-local + metadata)
                if (b[0] == 10) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 169 && b[1] == 254) return true;
                if (b[0] == 127) return true;
                if (b[0] == 0) return true;
            }
            return false;
        }
    }
}
