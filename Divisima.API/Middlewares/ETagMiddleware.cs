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
        // Açıklayıcı yorum: Yalnız bu ön eklerdeki GET'ler ETag'lenir (dar kapsam = düşük risk)
        private static readonly string[] CacheablePrefixes = { "/api/product", "/api/category", "/api/collection", "/api/sizeguide" };

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
                if (context.Response.StatusCode == StatusCodes.Status200OK
                    && buffer.Length > 0
                    && !context.Response.Headers.ContainsKey("ETag"))
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

        private static bool IsCacheablePath(PathString path)
        {
            foreach (var p in CacheablePrefixes)
                if (path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
