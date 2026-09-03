using System.Security.Cryptography;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Katalog GET yanıtlarına ETag ekler; istemci If-None-Match ile aynı ETag'i
    // gönderirse 304 Not Modified döner (gövde tekrar gönderilmez -> bant genişliği tasarrufu).
    // Yalnız okuma-ağırlıklı katalog yollarında (ürün/kategori) çalışır; yazma ve diğer uçlar etkilenmez.
    // Savunmacı: herhangi bir sapmada (200 değil, boş gövde, zaten ETag var) dokunmadan geçer.
    public class ETagMiddleware
    {
        private readonly RequestDelegate _next;

        // Açıklayıcı yorum: Yalnız bu ön eklerdeki GET'ler ETag'lenir (dar kapsam = düşük risk).
        //
        // ═══ FAZ 0 / K1 - OLU ONEK KALDIRILDI ══════════════════════════════════════════
        // Listede "/api/sizeguide" vardi ve ILK COMMIT'ten (df91863) beri HIC ESLESMIYORDU:
        // gercek rota "api/size-guide" (SizeGuideController) ve eslesme StartsWithSegments
        // ile SEGMENT SINIRLI yapiliyor - "sizeguide" ile "size-guide" ayri segmentlerdir.
        // CANLI OLCULDU (FAZ 0):
        //     /api/size-guide/category/1  -> 200, ETag YOK,  Cache-Control: no-store...
        //     /api/product/get/1          -> 200, ETag VAR,  Cache-Control: private, max-age=60
        //     /api/category/getlist       -> 200, ETag VAR   (If-None-Match ile 304 + 0 bayt)
        //     /api/product-attribute/...  -> 200, ETag YOK   (segment siniri DOGRU calisiyor)
        // Onek KALDIRILDI, DUZELTILMEDI - gerekce olcume dayali: SizeGuide'in iki anonim GET'i
        // de bugun OLU YUZEY (storefront hic cagirmiyor), yani ETag kazanci SIFIR; buna karsilik
        // oneki duzeltmek o uclarin Cache-Control'unu SecurityHeaders'in "no-store"undan
        // "private, max-age=60"a GEVSETIRDI (asagida: ETag dali bu basligi EZIYOR).
        // SIZE-GUIDE VITRINE BAGLANIRSA: onek "/api/size-guide" olarak BILINCLI geri eklenir ve
        // Cache-Control karari ONUNLA BIRLIKTE verilir. Defterde kayitli.
        //
        // YAPISAL KURAL: bu listedeki her onek, gercek bir uca SEGMENT-ESLESMELIDIR.
        // Olu onek yasak - p-k1a (Faz0SozlesmeTests) yapisal olarak tarar.
        private static readonly string[] CacheablePrefixes = { "/api/product", "/api/category", "/api/collection" };

        public ETagMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            // Açıklayıcı yorum: Yalnız GET + hedef yol katalog ise devreye gir
            if (!HttpMethods.IsGet(context.Request.Method) || !IsCacheablePath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);

                // Açıklayıcı yorum: Yalnız 200 + dolu gövde + zaten ETag yoksa
                // GF-3/K7: ... VE UC KIMLIKSIZ ISE (bkz. KimlikliYanit).
                if (context.Response.StatusCode == StatusCodes.Status200OK
                    && buffer.Length > 0
                    && !context.Response.Headers.ContainsKey("ETag")
                    && !KimlikliYanit(context))
                {
                    var hash = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
                    var etag = "\"" + Convert.ToHexString(hash) + "\"";
                    context.Response.Headers["ETag"] = etag;
                    context.Response.Headers["Cache-Control"] = "private, max-age=60";

                    var ifNoneMatch = context.Request.Headers["If-None-Match"].ToString();
                    if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
                    {
                        // Açıklayıcı yorum: İçerik değişmemiş - 304 + boş gövde
                        context.Response.StatusCode = StatusCodes.Status304NotModified;
                        context.Response.Headers.ContentLength = 0;
                        context.Response.Body = originalBody;
                        return;
                    }
                }

                // Açıklayıcı yorum: Tamponlanan gövdeyi gerçek çıkışa yaz
                buffer.Position = 0;
                context.Response.Body = originalBody;
                await buffer.CopyToAsync(originalBody);
            }
            catch
            {
                // Açıklayıcı yorum: Hata olursa gövdeyi olduğu gibi akıt (bozma), sonra fırlat
                context.Response.Body = originalBody;
                if (buffer.Length > 0) { buffer.Position = 0; await buffer.CopyToAsync(originalBody); }
                throw;
            }
        }

        // ══ GF-3 / K7 (AV-1: E-6) - KIMLIKLI YANIT ONBELLEKLENMEZ ══════════════════════════
        //
        // OLCULEN KUSUR: onek listesi (`/api/product|category|collection`) kimlikli ve
        // kimliksiz ucu AYIRT ETMIYORDU. Bu middleware `SecurityHeadersMiddleware`den ONCE
        // kayitlidir, yani DIS halkadir ve yanit yolunda SONRA calisir: onun `no-store`
        // basligini `private, max-age=60` ile EZIYORDU (`Pragma: no-cache` yerinde kalip
        // CELISKILI bir baslik cifti uretiyordu). Kapsamdaki SEKIZ GET'ten BIRI admin-only:
        // `GET /api/Product/getlist` (`ProductController.cs:109-110`) - yani admin urun
        // listesi paylasilan bir ara onbellege/proxy'ye ya da diske dusebilirdi.
        //
        // ONEK LISTESI SILINEREK COZULEMEZ - iki pin onu kilitliyor: `Faz0SozlesmeTests`
        // (>=2 onek bulunmali) ve `StorefrontCatalogContractTests` (`/api/product` ETag
        // TASIMALI). Cozum kapsam daraltma degil, KIMLIK AYRIMI.
        //
        // IKI OLCUT, ikisi de gerekli:
        //  (1) UC KIMLIK ISTIYOR MU: `MapControllers().RequireAuthorization()` (GF-1/K5)
        //      yuzunden `[AllowAnonymous]` TASIMAYAN her controller ucu kimlik ister.
        //      Metadata `_next`ten SONRA okunur - rota o ana kadar cozulmus olur.
        //  (2) ISTEK KIMLIKLI GELDI MI: `[AllowAnonymous]` bir uc, jeton varsa kisiye ozel
        //      icerik donebilir (ornegin fiyatlandirma). Bu dal onu da kapsar.
        // Ikisinden BIRI dogruysa ETag da `max-age` de YAZILMAZ; `SecurityHeaders`in
        // `no-store` basligi OLDUGU GIBI KALIR.
        private static bool KimlikliYanit(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true) return true;

            var endpoint = context.GetEndpoint();
            if (endpoint == null) return false;   // rota cozulmediyse zaten govde de yok

            return endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() == null;
        }

        private static bool IsCacheablePath(PathString path)
        {
            foreach (var p in CacheablePrefixes)
                if (path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
