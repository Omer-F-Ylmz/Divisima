using Divisima.Core.Utilities.Caching;

namespace Divisima.Core.Security.JWT
{
    // GF-1b / K1 - `IUserTokenRevocation`in onbellek tabanli uygulamasi.
    // Tek sunucuda IMemoryCache, cok sunucuda Redis - AYNI arayuz.
    public class CacheUserTokenRevocation : IUserTokenRevocation
    {
        private readonly ICacheService _cache;
        public CacheUserTokenRevocation(ICacheService cache) => _cache = cache;

        // ══ SAAT KAYMASI (SKEW) ESIGE DEGIL, TTL'E EKLENIR ════════════════════════════════
        //
        // Bu ayrim KRITIK ve olculmus bir tuzaktir: esik `now + skew` yazilsaydi, iptalden
        // HEMEN SONRA alinan YENI jeton da (iat = now) esikten kucuk kalir ve kullanici
        // KILITLENIRDI. Skew yalnizca kaydin NE KADAR YASAYACAGINI uzatir.
        private static readonly TimeSpan Skew = TimeSpan.FromSeconds(60);

        // Anahtar KULLANICI TIPINI de tasir: musteri 5 ile satici 5 AYRI kimliklerdir ve
        // ayni anahtari paylasirlarsa birinin iptali otekini de dusururdu.
        private static string Key(int userType, int userId) => $"revoked-before:{userType}:{userId}";

        public async Task RevokeAllBeforeNowAsync(int userType, int userId, TimeSpan tokenLifetime)
        {
            // UTC UNIX SANIYE - jetondaki `iat` ile AYNI eksende (JwtHelper `DateTimeOffset.UtcNow`
            // yaziyor). Bu dosyada `DateTime.Now` (YEREL) KULLANILMAZ: depo genelinde yerel saat
            // yaygin ve karistirilirsa tr-TR'de UC SAATLIK hata dogar (ya toplu kilitlenme ya
            // tam no-op).
            var esik = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Kayit, iptal edilen en YENI jetonun omru kadar yasamalidir; daha eski jetonlar
            // zaten kendiliginden suresi dolmus olur. Bu yuzden TTL sinirlidir - kayit
            // sonsuza kadar buyumez.
            await _cache.SetAsync(Key(userType, userId), esik, tokenLifetime + Skew);
        }

        public async Task<bool> IsRevokedAsync(int userType, int userId, long iatUnixSeconds)
        {
            var esik = await _cache.GetAsync<long?>(Key(userType, userId));
            if (esik == null) return false;   // kayit yok -> iptal de yok

            // KESIN OLARAK "<" - "<=" DEGIL. `iat` SANIYE cozunurluklu; iptalle AYNI saniyede
            // uretilen bir jeton (kullanici sifresini degistirip hemen yeni jeton aldi)
            // "<=" ile HAKSIZ YERE oldurulurdu.
            return iatUnixSeconds < esik.Value;
        }
    }
}
