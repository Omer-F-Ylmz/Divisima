using System.Globalization;
using System.Security.Claims;
using Divisima.Core.Security.JWT;
using Divisima.Core.Utilities.Caching;
using Divisima.Core.Utilities.Enums;
using Divisima.DataAccess.Abstract;

namespace Divisima.API.Middlewares
{
    // Açıklayıcı yorum: Her kimlikli istekte iki kontrol yapar:
    //   1) token'ın jti'si kara listede mi (logout/iptal edilmiş token),
    //   2) token'ın sahibi olan MÜŞTERİ HESABI hâlâ aktif mi.
    // İkincisi olmadan JWT stateless olduğu için askıya alınan/silinen kullanıcı, token'ının
    // süresi dolana kadar erişmeye devam ediyordu: oturumlar (user_sessions) düşürülse bile
    // access token çalışıyordu ve müşteri veri YAZABİLİYORDU. Erişim engeli yalnızca
    // Customer üzerindeki global is_active sorgu filtresinin satırı gizlemesine bağlıydı -
    // yani müşteri satırını okumayan uçlar (favori, sepet) pasif hesap için çalışmaya devam ediyordu.
    public class TokenBlacklistMiddleware
    {
        // Hesap durumu için kısa ömürlü cache: her kimlikli istekte DB'ye gitmemek için.
        // Askıya alma / silme yolları anahtarı DÜŞÜRDÜĞÜ için ban ANINDA etkili olur;
        // TTL yalnızca invalidate'in kaçırıldığı (ör. DB'den elle güncelleme) durumda üst sınırdır.
        private static readonly TimeSpan AccountStatusTtl = TimeSpan.FromSeconds(60);

        private readonly RequestDelegate _next;
        public TokenBlacklistMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, ITokenBlacklist blacklist,
            ICacheService cache, ICustomerDal customerDal, IUserTokenRevocation revocation)
        {
            var jti = context.User?.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti) && await blacklist.IsRevokedAsync(jti))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { Success = false, Message = "Oturum sonlandırılmış. Lütfen tekrar giriş yapın." });
                return;
            }

            // ══ GF-1b / K1 - KULLANICI BASINA TOPLU IPTAL ESIGI ═══════════════════════════
            //
            // `jti` kara listesi YALNIZ SUNULAN jetonu oldurur; kullanicinin BASKA
            // cihazlardaki jetonlari icin `jti` elimizde YOKTUR. Esik kontrolu o boslugu
            // kapatir: jetonun `iat`i, kullanicinin son toplu iptalinden ONCEYSE reddedilir.
            //
            // `iat` KISA AD OLARAK OKUNUR - olculdu: `JwtSecurityTokenHandler`in varsayilan
            // inbound haritasinda `iat` ESLESMESI YOKTUR (POZ kontrol: `email` ESLESIYOR),
            // dolayisiyla claim tipi kisa kalir. Deger TIRNAKSIZ SAYIDIR (Integer64).
            //
            // KAPSAM: musteri VE admin (ikisi de `customers` tablosunda, `user_type` ile
            // ayrisir) ile satici. Anahtar `user_type` tasidigi icin kimlik CAKISMASI YOK.
            var iatClaim = context.User?.FindFirst("iat")?.Value;
            if (context.User?.Identity?.IsAuthenticated == true
                && long.TryParse(iatClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iat)
                && int.TryParse(context.User.FindFirst("user_type")?.Value, out var userType)
                && int.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                && uid > 0
                && await revocation.IsRevokedAsync(userType, uid, iat))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { Success = false, Message = "Oturum sonlandırılmış. Lütfen tekrar giriş yapın." });
                return;
            }

            // Açıklayıcı yorum: Yalnız MÜŞTERİ token'ları kontrol edilir - admin ve satıcı kimlikleri
            // customers tablosunda değildir, orada aranırsa hepsi haksız yere reddedilirdi.
            // Anonim istekler (jti/claim yok) buraya hiç girmez: /health, ürün listeleme vb. etkilenmez.
            if (context.User?.Identity?.IsAuthenticated == true
                && context.User.FindFirst("user_type")?.Value == ((int)UserTypeEnum.Customer).ToString(System.Globalization.CultureInfo.InvariantCulture)
                && int.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var customerId)
                && customerId > 0)
            {
                var isActive = await cache.GetOrSetAsync(
                    CacheKeys.CustomerActive(customerId),
                    async () =>
                    {
                        // NOT: Customer üzerinde global is_active sorgu filtresi var, yani pasif müşteri
                        // zaten null döner. Yine de is_active AÇIKÇA kontrol ediliyor - filtre ileride
                        // kaldırılırsa bu kontrol sessizce devre dışı kalmasın.
                        var customer = await customerDal.GetAsync(c => c.id == customerId);
                        return customer != null && customer.is_active;
                    },
                    AccountStatusTtl);

                if (!isActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { Success = false, Message = "Hesabınız aktif değil. Lütfen destek ile iletişime geçin." });
                    return;
                }
            }

            await _next(context);
        }
    }
}
