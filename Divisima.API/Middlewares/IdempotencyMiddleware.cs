using Divisima.Core.Utilities.Caching;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Idempotency-Key başlığı olan POST/PUT isteklerinde çift işlemi engeller.
    // İstemci aynı anahtarla iki kez gönderirse (ağ tekrarı, çift tık) ikinci istek işlenmez.
    // Ödeme dışı tüm mutasyonlar için genel koruma (cache tabanlı; Redis'e geçişte dağıtık).
    public class IdempotencyMiddleware
    {
        private readonly RequestDelegate _next;
        private const string HeaderName = "Idempotency-Key";

        public IdempotencyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICacheService cache)
        {
            // Açıklayıcı yorum: Sadece değiştiren metotlarda ve anahtar varsa çalış
            var isMutation = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method);
            if (!isMutation || !context.Request.Headers.TryGetValue(HeaderName, out var key) || string.IsNullOrWhiteSpace(key))
            {
                await _next(context);
                return;
            }

            // GÜVENLİK/DOĞRULUK: anahtarı method+path ile kapsa. Önceki "idem:{key}" GLOBAL'di -> istemci aynı
            // Idempotency-Key'i farklı endpoint'lerde kullanırsa (veya key rastgele-GUID değilse) çapraz-endpoint
            // yanlış 409 olurdu. Method+path ile her endpoint kendi idempotency kapsamına sahip.
            // NOT: kullanıcı-bazlı kapsam için (çapraz-kullanıcı collision) middleware auth SONRASINA taşınmalı (Ömer'in build/test'i).
            var cacheKey = $"idem:{context.Request.Method}:{context.Request.Path}:{key}";
            // ATOMIK set-if-not-exists (SETNX): eszamanli AYNI-key isteklerden yalniz BIRI true alir (anahtari o ekledi),
            // digerleri false -> 409. Onceki GetOrSet-Remove-GetOrSet check-then-act RACE'liydi: iki eszamanli istek
            // ikisi de seen=false okuyup ikisi de islenebiliyordu (cift-siparis/odeme/iade). Artik bolunmez.
            var isFirst = await cache.TryAddAsync(cacheKey, TimeSpan.FromHours(24));
            if (!isFirst)
            {
                // Açıklayıcı yorum: Anahtar zaten var (başka istek işledi/işliyor) - çift işlemi engelle (409)
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { Success = false, Message = "Bu istek zaten işlendi (idempotency)." });
                return;
            }

            try
            {
                await _next(context);
            }
            catch
            {
                // Açıklayıcı yorum: İşlem BAŞARISIZ (istisna/rollback) -> idempotency anahtarını kaldır ki istemci aynı
                // key ile GÜVENLE tekrar deneyebilsin (mutasyonlar transactional -> başarısız istek geri alındı).
                cache.Remove(cacheKey);
                throw;
            }
        }
    }
}
