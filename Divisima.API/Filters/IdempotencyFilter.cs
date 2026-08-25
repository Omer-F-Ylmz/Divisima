using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Constants;
using Divisima.Core.Utilities.Results;
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
    //
    // ══ GUVENLIK-FIX-4 / SUPHELI #22 - UC OLCULMUS KUSUR DUZELTILDI ═══════════════════════
    //
    // Canli tur (gercek uclar, iki GERCEK hesap, gercek misafir siparisleri) uc sey olctu:
    //
    //  (a) KAPSAMDA KULLANICI AYRIMI YOKTU. `User.Identity.Name` DAIMA null (JwtHelper
    //      `ClaimTypes.Name` yazmiyor - D4'te de birebir goruldu), dolayisiyla HER kimlikli
    //      cagiran "anon" kapsamina dusuyordu. OLCULDU (/api/order/place):
    //         A + anahtar K -> 201 siparis 180 · B + AYNI K -> 201 replayed, GOVDEDE 180
    //         B'nin siparis sayisi -> 0   (B'nin istegi SESSIZCE dustu)
    //      COZUM: kimlik cozunurlugu `IdempotencyKimligi.Coz` ile TEK KAYNAKTAN - middleware
    //      ile BIREBIR ayni (ClaimTypes.NameIdentifier).
    //
    //  (b) ANAHTAR GOVDEYE BAGLI DEGILDI. Kayitta istek govdesinin ozeti tutulmuyordu, yani
    //      AYNI anahtarla FARKLI bir govde gonderen istemci BASKA BIR ISTEGIN yanitini
    //      "basarili" olarak aliyor ve kendi istegi HIC islenmiyordu. OLCULDU
    //      (/api/guest-checkout/place): anahtar K + govde(E2) -> 201 siparis 179;
    //      anahtar K + govde(E3) -> 201 replayed, govdede 179; E3 icin musteri 0, siparis 0.
    //      COZUM: kayda istek govdesinin SHA-256'si yazilir. Ayni anahtar + FARKLI ozet ->
    //      422 (islenmez, replay EDILMEZ, sessizce DUSMEZ - istemci ne oldugunu OGRENIR).
    //      Ayni anahtar + ayni ozet -> replay (asil vaat korunur).
    //
    //  (c) REPLAY YANITI ORIJINALLE AYNI DEGILDI. Govde `JsonSerializer.SerializeToElement`
    //      ile VARSAYILAN secenekler kullanilarak saklaniyordu; MVC ise camelCase yaziyor.
    //      OLCULDU: orijinal {"data":179,...} · replay {"Data":179,...}.
    //      COZUM: yanit HAM BAYT olarak yakalanir ve AYNEN geri verilir - bicimlendirme
    //      hakkinda hicbir varsayim YOK, dolayisiyla bayt-birebir olmasi YAPISALDIR.
    //
    // NEDEN RESOURCE FILTER (eskiden ActionFilter idi): (b) ham istek govdesini MODEL
    // BINDING'DEN ONCE okumayi, (c) ham yanit baytlarini SONUC YURUTULDUKTEN SONRA
    // yakalamayi gerektirir. Action filter ikisini de goremez - `next()` dondugunde sonuc
    // HENUZ YURUTULMEMISTIR. Resource filter yonlendirmeden sonraki her seyi (model binding
    // + action + sonuc yurutme) sarar, yani ikisi de ayni yerde elde edilir.
    //
    // CACHE ANAHTARI ONEKI `idem2:` - BILINCLI: saklanan kaydin SEKLI degisti (govde ozeti +
    // ham bayt eklendi). Eski onekle devam etmek, dagitim aninda cache'te duran ESKI SEKILLI
    // kayitlarin sessizce "bozuk" sayilmasina ve o anahtarlarin yeniden islenmesine yol
    // acardi; onek degisince eski kayitlar dogal olarak devre disi kalir (24 saatte duser).
    public class IdempotencyAttribute : Attribute, IAsyncResourceFilter
    {
        private const string HeaderName = "Idempotency-Key";
        private const string ReplayHeader = "Idempotency-Replayed";

        private static readonly DistributedCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };

        // Saklanan yanit: durum + icerik tipi + HAM GOVDE (base64) + ISTEK GOVDESININ OZETI.
        // Govde `JsonElement` DEGIL ham bayttir - (c)'nin cozumu tam olarak budur.
        private sealed class KayitliYanit
        {
            public int StatusCode { get; set; }
            public string? ContentType { get; set; }
            public string GovdeB64 { get; set; } = "";
            public string IstekGovdeOzeti { get; set; } = "";
        }

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var istek = context.HttpContext.Request;

            // Açıklayıcı yorum: Anahtar yoksa filtre devre dışı (normal akış).
            // STOREFRONT BU BASLIGI BUGUN HIC GONDERMIYOR (olculdu: frontend'de 0 gecis) -
            // yani basliksiz akis DEGISMEZ ve hicbir mevcut istemci etkilenmez.
            if (!istek.Headers.TryGetValue(HeaderName, out var anahtarDegeri)
                || string.IsNullOrWhiteSpace(anahtarDegeri.ToString()))
            {
                await next();
                return;
            }

            var cache = context.HttpContext.RequestServices.GetService(typeof(IDistributedCache)) as IDistributedCache;
            if (cache == null) { await next(); return; }

            // (b) ISTEK GOVDESININ OZETI. Resource filter model binding'den ONCE kostugu icin
            // govde HENUZ OKUNMAMISTIR; `EnableBuffering` ile geri sarilabilir hale getirilip
            // okunur ve basa alinir - model binding ayni govdeyi sorunsuz okur.
            istek.EnableBuffering();
            var govdeOzeti = await GovdeOzetiAsync(istek.Body);
            istek.Body.Position = 0;

            // (a) Kimlik cozunurlugu TEK KAYNAKTAN - middleware ile birebir ayni.
            var kullanici = IdempotencyKimligi.Coz(context.HttpContext.User);
            var ham = $"{anahtarDegeri}|{istek.Method}|{istek.Path}|{kullanici}";
            var cacheKey = "idem2:" + Sha256(Encoding.UTF8.GetBytes(ham));

            var mevcut = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(mevcut))
            {
                KayitliYanit? kayit = null;
                try { kayit = JsonSerializer.Deserialize<KayitliYanit>(mevcut); }
                catch { /* bozuk cache - normal akışa devam */ }

                if (kayit != null)
                {
                    // (b) FARKLI GOVDE -> replay EDILMEZ, sessizce DUSMEZ.
                    // 422 secildi cunku istek SOZDIZIMSEL olarak gecerli (400 degil) ve
                    // cakisan bir KAYNAK durumu yok (409 degil): anahtarin yeniden
                    // kullanilmasi ISLENEMEZ bir semantik durumdur.
                    // GOVDE MEVCUT HATA SOZLESMESIYLE: bu API'de ELE ALINAN hatalar
                    // `ErrorResult` zarfi doner (Program.cs `InvalidModelStateResponseFactory`
                    // de varsayilan ProblemDetails yerine ACIKCA bunu secer);
                    // `application/problem+json` YALNIZCA yakalanmayan istisnalarda
                    // (`ExceptionMiddleware`) kullanilir. Olculdu ve o sozlesmeye uyuldu.
                    if (!string.Equals(kayit.IstekGovdeOzeti, govdeOzeti, StringComparison.Ordinal))
                    {
                        context.Result = new ObjectResult(new ErrorResult(Messages.IdempotencyBodyMismatch))
                        {
                            StatusCode = StatusCodes.Status422UnprocessableEntity
                        };
                        return;
                    }

                    // (c) BAYT-BIREBIR replay.
                    context.Result = new HamYanitSonucu(kayit);
                    return;
                }
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
                    // GUVENLIK-FIX-4: bu govde eskiden ANONIM bir nesneydi (`new { Success, Message }`)
                    // ve VARSAYILAN seceneklerle PascalCase seriliyordu - ayni ucun diger hatalari
                    // camelCase `ErrorResult` zarfi donerken. Zarf birlestirildi. Kirilma riski YOK:
                    // bu dala ancak `Idempotency-Key` gonderen bir istemci ulasir ve storefront o
                    // basligi HIC gondermiyor (olculdu).
                    context.Result = new ObjectResult(new ErrorResult(Messages.IdempotencyInFlight))
                    {
                        StatusCode = StatusCodes.Status409Conflict
                    };
                    return;
                }
            }

            // (c) Yanit HAM BAYT olarak yakalanir: gercek govde akisi gecici bir tampona
            // degistirilir, boru hatti kosar, sonra baytlar AYNEN gercek akisa yazilir.
            // Istemcinin gordugu bayt dizisi ile cache'e yazilan BIREBIR AYNIDIR.
            var yanit = context.HttpContext.Response;
            var gercekAkis = yanit.Body;
            using var tampon = new MemoryStream();
            yanit.Body = tampon;

            byte[] baytlar;
            try
            {
                await next();
            }
            catch
            {
                yanit.Body = gercekAkis;
                if (cacheSvc != null) cacheSvc.Remove(lockKey);   // istisna -> lock'u bırak (retry), sonra yeniden fırlat
                throw;
            }
            finally
            {
                baytlar = tampon.ToArray();
                yanit.Body = gercekAkis;
            }

            if (baytlar.Length > 0) await gercekAkis.WriteAsync(baytlar);

            var durum = yanit.StatusCode;

            // DALGA D / D4: YALNIZCA BASARILI (2xx) yanit cache'lenir; digerlerinde
            // lock BIRAKILIR. Eskiden kosul `status < 500` idi - yani bir 400 de
            // "kesin sonuc" sayilip cache'leniyor ve anahtari 24 SAAT yakiyordu.
            // OLCULEN ZARAR (middleware tarafinda birebir ayni sinif): istemci
            // girdisini DUZELTIP ayni anahtarla tekrar dendiginde istegi HIC islenmiyordu.
            // 4xx bir ISTEMCI HATASIDIR ve duzeltilebilir; "kesin sonuc" DEGILDIR.
            if (durum >= 200 && durum < 300)
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new KayitliYanit
                    {
                        StatusCode = durum,
                        ContentType = yanit.ContentType,
                        GovdeB64 = Convert.ToBase64String(baytlar),
                        IstekGovdeOzeti = govdeOzeti
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

        // Saklanan ham baytlari AYNEN yazan sonuc. Bir `ObjectResult` KULLANILAMAZ: o, govdeyi
        // YENIDEN serilestirir ve (c)'deki bicim ayrismasini geri getirirdi.
        private sealed class HamYanitSonucu : IActionResult
        {
            private readonly KayitliYanit _kayit;
            public HamYanitSonucu(KayitliYanit kayit) => _kayit = kayit;

            public async Task ExecuteResultAsync(ActionContext context)
            {
                var yanit = context.HttpContext.Response;
                var baytlar = Convert.FromBase64String(_kayit.GovdeB64);
                yanit.StatusCode = _kayit.StatusCode;
                if (!string.IsNullOrEmpty(_kayit.ContentType)) yanit.ContentType = _kayit.ContentType;
                yanit.Headers[ReplayHeader] = "true";
                yanit.ContentLength = baytlar.Length;
                await yanit.Body.WriteAsync(baytlar);
            }
        }

        private static async Task<string> GovdeOzetiAsync(Stream govde)
        {
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(govde);
            return Convert.ToHexString(hash);
        }

        private static string Sha256(byte[] girdi) => Convert.ToHexString(SHA256.HashData(girdi));
    }
}
