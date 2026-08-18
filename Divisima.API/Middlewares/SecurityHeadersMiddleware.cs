namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Güvenlik HTTP başlıkları ekler (OWASP önerileri).
    // Clickjacking, MIME-sniffing, XSS, referrer sızıntısı, tarayıcı özellik kötüye kullanımına karşı.
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;
            // Açıklayıcı yorum: Sayfanın iframe'e gömülmesini engelle (clickjacking)
            headers["X-Frame-Options"] = "DENY";
            // Açıklayıcı yorum: Tarayıcının içerik tipini tahmin etmesini engelle (MIME-sniffing)
            headers["X-Content-Type-Options"] = "nosniff";
            // Açıklayıcı yorum: Referrer bilgisini kısıtla (gizlilik)
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // Açıklayıcı yorum: Gereksiz tarayıcı özelliklerini kapat
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            // Açıklayıcı yorum: İçerik güvenlik politikası (API için sıkı; Iyzico iframe'ine izin)
            // GÜVENLİK SERTLEŞTİRME (OWASP): default/frame/script-src'e ek olarak object-src 'none' (eklenti/Flash tabanlı XSS
            // engeli), base-uri 'self' (<base> etiketi enjeksiyonuyla göreli URL kaçırma engeli), form-action 'self' (form'un
            // harici siteye gönderilmesi/hijack engeli), frame-ancestors 'none' (clickjacking - X-Frame-Options'ın CSP karşılığı).
            headers["Content-Security-Policy"] = "default-src 'self'; frame-src https://*.iyzipay.com; " +
                "script-src 'self' https://*.iyzipay.com; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            // Açıklayıcı yorum: API yanıtları önbelleğe alınmasın (hassas veri proxy/tarayıcı cache sızıntısı)
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            headers["Pragma"] = "no-cache";
            // Açıklayıcı yorum: Flash/PDF cross-domain policy engeli
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            // Açıklayıcı yorum: Sunucu bilgisini gizle
            headers.Remove("Server");
            await _next(context);
        }
    }
}
