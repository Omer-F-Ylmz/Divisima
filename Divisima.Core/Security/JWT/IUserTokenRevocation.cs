namespace Divisima.Core.Security.JWT
{
    // ══ GF-1b / K1 - KULLANICI BASINA TOPLU ACCESS TOKEN IPTALI ═══════════════════════════
    //
    // OLCULEN BOSLUK (GF-1 muhru, BILINEN SINIR): `ITokenBlacklist` YALNIZ SUNULAN jetonu
    // (`jti`) iptal eder. Kullanicinin BASKA cihazlardaki access token'lari `jti`leri
    // hicbir yerde saklanmadigi icin iptal EDILEMIYORDU. GF-1'de olculdu: sifre degisiminden
    // sonra cihaz1 401 alirken IKINCI CIHAZ 200 almaya devam ediyordu.
    //
    // COZUM: jetonlari TEK TEK saymak yerine bir ESIK tutulur - "su ANDAN once uretilmis
    // TUM jetonlar gecersiz". Jetonun `iat` claim'i esikten KUCUKSE reddedilir. Boylece
    // kac cihaz oldugu bilinmeden hepsi tek yazimla dusurulur ve HICBIR MIGRATION gerekmez.
    public interface IUserTokenRevocation
    {
        // Bu ANDAN once uretilmis tum access token'lari gecersiz kilar.
        // `tokenLifetime` = access token omru; kayit o omur + skew kadar yasar, cunku daha
        // eski bir jeton ZATEN kendiliginden suresi dolmus olur.
        Task RevokeAllBeforeNowAsync(int userType, int userId, TimeSpan tokenLifetime);

        // Jetonun `iat` degeri esikten KUCUKSE true (= iptal edilmis).
        Task<bool> IsRevokedAsync(int userType, int userId, long iatUnixSeconds);
    }
}
