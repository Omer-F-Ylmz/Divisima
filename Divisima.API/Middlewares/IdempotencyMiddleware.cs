using Divisima.API.Filters;
using Divisima.Core.Utilities.Caching;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Idempotency-Key başlığı olan POST/PUT isteklerinde çift işlemi engeller.
    // İstemci aynı anahtarla iki kez gönderirse (ağ tekrarı, çift tık) ikinci istek işlenmez.
    //
    // === DALGA D / D4 - UC OLCULMUS KUSUR DUZELTILDI ==================================
    //
    // Canli tur (gercek API, gercek hesaplar) uc sorun olctu:
    //
    //  (1) CAPRAZ KULLANICI CAKISMASI. Middleware `UseAuthentication`DAN ONCE kosuyordu,
    //      dolayisiyla anahtar kullaniciyla kapsanamiyordu. OLCULDU: A anahtar K ile 201
    //      aldi; B AYNI K ile 409 aldi ve B'nin kaydi HIC OLUSMADI. Kodun kendi yorumu bunu
    //      zaten soyluyordu ("kullanici-bazli kapsam icin middleware auth SONRASINA tasinmali").
    //      COZUM: middleware `UseAuthorization`DAN SONRAYA tasindi ve anahtara KULLANICI
    //      bileseni eklendi. Yan kazanc: 401/403 alan bir istek artik anahtari HIC talep etmez.
    //
    //  (2) BASARISIZ ISTEK ANAHTARI 24 SAAT YAKIYORDU. Anahtar `_next`ten ONCE talep edilip
    //      yalnizca ISTISNA durumunda birakiliyordu; normal donen bir hata yaniti (400/404/405)
    //      istisna DEGILDIR. OLCULDU: bozuk govde -> 400; ardindan AYNI anahtar + GECERLI govde
    //      -> 409. Istemci hatasini duzeltse bile istegi HIC islenmiyordu.
    //      COZUM: anahtar YALNIZCA 2xx yanitta tutulur; digerlerinde BIRAKILIR.
    //
    //  (3) FILTRENIN REPLAY DALI ULASILAMAZDI. `IdempotencyAttribute` ikinci istekte ILK
    //      YANITIN KOPYASINI donmek uzere yazilmisti; middleware ondan ONCE kostugu icin
    //      istemci 409 aliyor ve ILK ISTEGIN SONUCUNU (or. siparis numarasi) OGRENEMIYORDU.
    //      OLCULDU: isaretli ucta 2. istek 409, "Idempotency-Replayed" basligi YOK.
    //      COZUM (olcume dayali): FILTRE KALIR, MIDDLEWARE DARALIR. Filtre yalnizca DORT
    //      PARA UCUNDA (order/place, guest-checkout/place, loyalty/redeem, giftcard/redeem)
    //      ve orada REPLAY dogru davranistir - ag tekrari yapan musteri siparis numarasini
    //      OGRENMELIDIR. Middleware geri kalan TUM mutasyonlarda genis emniyet agi olarak
    //      kalir. Ikisi de ULASILABILIR; OLU KOD YOK.
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

            // D4(3): Bu ucun KENDI idempotency filtresi varsa middleware KENARA CEKILIR.
            // Aksi halde filtre hicbir zaman calisamaz (middleware once davranir) ve onun
            // REPLAY dali olu kalirdi - olculen durum tam olarak buydu.
            if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyAttribute>() != null)
            {
                await _next(context);
                return;
            }

            // GÜVENLİK/DOĞRULUK: anahtarı method+path ile kapsa. Önceki "idem:{key}" GLOBAL'di -> istemci aynı
            // Idempotency-Key'i farklı endpoint'lerde kullanırsa (veya key rastgele-GUID değilse) çapraz-endpoint
            // yanlış 409 olurdu. Method+path ile her endpoint kendi idempotency kapsamına sahip.
            //
            // D4(1): KULLANICI bileseni eklendi. Middleware artik auth SONRASINDA kostugu icin
            // kimlik BURADA MEVCUT. Kimliksiz (anonim) uclarda kapsam "anon"dur - yani ANONIM
            // UCLARDA BUGUNKU DAVRANIS AYNEN KORUNUR. Bu bilinclidir: anonim bir cagirani
            // ayirt edecek guvenilir bir kimlik YOKTUR (IP taşınabilir/paylasilir; onu anahtara
            // koymak ayni istemcinin ag degistirmesi durumunda korumayi SESSIZCE kaldirirdi).
            // Anonim para uclari (guest-checkout) zaten FILTRE tarafindan korunuyor.
            // KIMLIK KAYNAGI: ClaimTypes.NameIdentifier (musteri id'si). `Identity.Name`
            // KULLANILMAZ - OLCULDU: JwtHelper token'a ClaimTypes.Name YAZMIYOR, dolayisiyla
            // `Identity.Name` NULL doner ve TUM kimlikli kullanicilar AYNI kapsama duserdi;
            // yani capraz-kullanici cakismasi kapanmis GORUNUR ama KAPANMAZDI (bu test
            // yazilirken birebir goruldu: B hala 409 aliyordu).
            // CurrentUserService de musteri id'sini AYNI claim'den okuyor - tek kaynak.
            var kullanici = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? "anon";
            var cacheKey = $"idem:{context.Request.Method}:{context.Request.Path}:{kullanici}:{key}";

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

                // D4(2): anahtar YALNIZCA BASARILI (2xx) bir sonuc icin tutulur. Basarisiz bir
                // istek anahtari yakarsa istemci, girdisini DUZELTIP ayni anahtarla tekrar
                // denedigNde 409 alir ve istegi HIC islenmez - olculen zarar buydu.
                var basarili = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300;
                if (!basarili) cache.Remove(cacheKey);
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
