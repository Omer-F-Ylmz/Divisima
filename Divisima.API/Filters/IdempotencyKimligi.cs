using System.Security.Claims;

namespace Divisima.API.Filters
{
    // ══ GUVENLIK-FIX-4 / #22(a) - IDEMPOTENCY KAPSAMININ KIMLIK COZUNURLUGU: TEK KAYNAK ═══
    //
    // OLCULEN ONCE-DURUM: iki mekanizma AYNI isi FARKLI sekilde yapiyordu.
    //   IdempotencyMiddleware : ClaimTypes.NameIdentifier ?? "anon"     (D4'te duzeltildi)
    //   IdempotencyAttribute  : User.Identity.Name        ?? "anon"     (ATLANDI)
    //
    // `Identity.Name` DAIMA NULL'dur - D4'te DAVRANISLA olculdu: JwtHelper token'a
    // `ClaimTypes.Name` YAZMIYOR. Sonuc: filtrenin kapsaminda kullanici ayrimi HIC YOKTU,
    // yani "kullanici ile kapsandi" diyen yorum dogru ama KOD yanlisti.
    //
    // CANLI KANIT (GUVENLIK-FIX-4 olcumu, /api/order/place, iki GERCEK hesap):
    //   A + Idempotency-Key K -> 201, siparis 180
    //   B + AYNI K            -> 201 "Idempotency-Replayed: true", govdede SIPARIS 180
    //   B'nin siparis sayisi  -> 0     (istegi SESSIZCE dustu, A'nin numarasini aldi)
    //
    // Ortaklastirma ZORLAMA DEGIL, DOGAL: iki tip de `Divisima.API` derlemesinde ve
    // `IdempotencyMiddleware` ZATEN `using Divisima.API.Filters;` diyor - bagimlilik yonu
    // hazirdi. `ICurrentUserService` de musteri kimligini AYNI claim'den okuyor.
    public static class IdempotencyKimligi
    {
        // Anonim cagiran icin TEK kapsam. Bilincli: anonim bir cagirani ayirt edecek
        // guvenilir bir kimlik YOKTUR (IP tasinabilir/paylasilir; onu anahtara koymak ayni
        // istemcinin ag degistirmesi durumunda korumayi SESSIZCE kaldirirdi).
        public const string AnonimKapsam = "anon";

        public static string Coz(ClaimsPrincipal? kullanici) =>
            kullanici?.FindFirst(ClaimTypes.NameIdentifier)?.Value is { Length: > 0 } kimlik
                ? kimlik
                : AnonimKapsam;
    }
}
