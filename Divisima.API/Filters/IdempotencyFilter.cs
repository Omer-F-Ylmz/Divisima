using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Divisima.Core.Utilities.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace Divisima.API.Filters
{
    // Açıklayıcı yorum: Idempotency-Key desteği. Aynı anahtarla gelen mutasyon isteği ikinci kez işlenmez;
    // ilk yanıt cache'ten döner. Ağ tekrar denemesi/çift-tık kaynaklı çift işlemi (çift sipariş, çift ödeme) önler.
    // B3: IDistributedCache (Redis) tabanlı - ÇOK-INSTANCE deploy'da tutarlı (instance-başı IMemoryCache değil).
    //
    // DALGA D / D4 - BU YORUM ESKIDEN YANLISTI: "Redis yoksa DI IDistributedCache'i in-memory
    // implementasyona duser" diyordu. ASP.NET Core bu servisi VARSAYILAN OLARAK KAYDETMEZ;
    // `IDistributedCache` yalnizca Redis dalinda (AddStackExchangeRedisCache) kayitliydi.
    // OLCULEN SONUC: `cache == null` -> filtre `await next()` ile SESSIZCE devre disi kaliyor,
    // yani dev/test/CI'da bu filtre HIC CALISMIYORDU. Program.cs'in Redis-disi dalina
    // `AddDistributedMemoryCache()` eklendi; yorum artik DOGRU.
    //
    // KAPSAM (D4 tasarim karari): bu filtre DORT PARA UCUNDA kullanilir ve orada REPLAY
    // dogru davranistir - ag tekrari yapan musteri ILK istegin sonucunu (siparis numarasi)
    // ogrenmelidir. IdempotencyMiddleware bu uclardan KENARA CEKILIR (endpoint metadata'sinda
    // bu ozniteligi gorurse atlar), boylece iki mekanizma da ULASILABILIR ve OLU KOD YOKTUR.
    public class IdempotencyAttribute : ActionFilterAttribute
    {
        private const string HeaderName = "Idempotency-Key";
        private static readonly DistributedCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };

        // Açıklayıcı yorum: Cache'lenen yanıt (durum + gövde) - JSON olarak Redis'te saklanır
        private class CachedResponse
        {
            public int StatusCode { get; set; }
            public JsonElement Body { get; set; }
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Açıklayıcı yorum: Anahtar yoksa filtre devre dışı (normal akış)
            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues)
                || string.IsNullOrWhiteSpace(keyValues.ToString()))
            {
                await next();
                return;
            }

            var cache = context.HttpContext.RequestServices.GetService(typeof(IDistributedCache)) as IDistributedCache;
            if (cache == null) { await next(); return; }

            // Açıklayıcı yorum: Anahtarı yol + kullanıcı ile birleştir (farklı endpoint/kullanıcıda karışmasın)
            var userScope = context.HttpContext.User?.Identity?.Name ?? "anon";
            var raw = $"{keyValues}|{context.HttpContext.Request.Path}|{userScope}";
            var cacheKey = "idem:" + Sha256(raw);

            // Açıklayıcı yorum: Daha önce işlendiyse cache'lenmiş yanıtı dön (yeniden işleme yok)
            var existing = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(existing))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<CachedResponse>(existing);
                    if (cached != null)
                    {
                        context.Result = new ObjectResult(cached.Body) { StatusCode = cached.StatusCode };
                        context.HttpContext.Response.Headers["Idempotency-Replayed"] = "true";
                        return;
                    }
                }
                catch { /* bozuk cache - normal akışa devam */ }
            }

            // Açıklayıcı yorum: ATOMİK CLAIM (SETNX) - eşzamanlı AYNI-key isteklerden yalnız BİRİ işler; diğerleri 409
            // (istemci kısa süre sonra replay alır). Önceki GetString-check -> next -> SetString check-then-act RACE'liydi:
            // iki eşzamanlı istek ikisi de "cache boş" görüp İKİSİ de işleyebiliyordu (çift-sipariş/ödeme). Artık bölünmez.
            var cacheSvc = context.HttpContext.RequestServices.GetService(typeof(ICacheService)) as ICacheService;
            var lockKey = cacheKey + ":lock";
            if (cacheSvc != null)
            {
                var claimed = await cacheSvc.TryAddAsync(lockKey, TimeSpan.FromSeconds(60));
                if (!claimed)
                {
                    context.Result = new ObjectResult(new { Success = false, Message = "Bu istek işleniyor, lütfen tekrar deneyin." })
                    { StatusCode = StatusCodes.Status409Conflict };
                    return;
                }
            }

            // Açıklayıcı yorum: İşle + kesin sonucu (2xx/4xx) cache'le; geçici 5xx tekrar denenebilsin (lock bırakılır)
            try
            {
                var executed = await next();
                if (executed.Result is ObjectResult objResult)
                {
                    var status = objResult.StatusCode ?? 200;

                    // DALGA D / D4: YALNIZCA BASARILI (2xx) yanit cache'lenir; digerlerinde
                    // lock BIRAKILIR. Eskiden kosul `status < 500` idi - yani bir 400 de
                    // "kesin sonuc" sayilip cache'leniyor ve anahtari 24 SAAT yakiyordu.
                    // OLCULEN ZARAR (middleware tarafinda birebir ayni sinif): istemci
                    // girdisini DUZELTIP ayni anahtarla tekrar dendiginde istegi HIC islenmiyordu.
                    // 4xx bir ISTEMCI HATASIDIR ve duzeltilebilir; "kesin sonuc" DEGILDIR.
                    if (status >= 200 && status < 300)
                    {
                        try
                        {
                            var payload = JsonSerializer.Serialize(new CachedResponse
                            {
                                StatusCode = status,
                                Body = JsonSerializer.SerializeToElement(objResult.Value)
                            });
                            await cache.SetStringAsync(cacheKey, payload, CacheOptions);
                        }
                        catch { /* cache yazımı best-effort */ }
                    }
                    else if (cacheSvc != null)
                    {
                        cacheSvc.Remove(lockKey);   // 4xx/5xx -> lock'u bırak, tekrar denenebilsin
                    }
                }
            }
            catch
            {
                if (cacheSvc != null) cacheSvc.Remove(lockKey);   // istisna -> lock'u bırak (retry), sonra yeniden fırlat
                throw;
            }
        }

        private static string Sha256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
