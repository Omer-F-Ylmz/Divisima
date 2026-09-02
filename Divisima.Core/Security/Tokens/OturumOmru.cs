namespace Divisima.Core.Security.Tokens
{
    // ══ GF-1b / K5 (GF1-B6) - OTURUM OMRU TEK KAYNAK ══════════════════════════════════════
    //
    // OLCULEN ONCE-DURUM: ayni buyuklugun IKI EL YAZMASI vardi ve BIRBIRINI TUTMUYORDU.
    //   AuthManager.cs  : `RefreshTokenDays = 7`  -> user_sessions.expires_at = simdi + 7 gun
    //   AuthController  : `DateTime.UtcNow.AddDays(30)` -> refresh cerezi 30 GUN yasiyor
    // Yani cerez, arkasindaki oturum satirindan **23 GUN DAHA UZUN** yasiyordu. 8. gunden
    // 30. gune kadar tarayici her refresh denemesinde SUNUCUYA gecerli gorunen bir cerez
    // gonderiyor, sunucu satiri suresi dolmus bulup 401 donuyordu: kullanicinin gordugu sey
    // "girisim var ama calismiyor" - ve o cerez, calinsa bile ise yaramayacak halde UC HAFTA
    // daha tarayicida duruyordu (gereksiz maruziyet penceresi).
    //
    // Cerezin yorumu da YANLISTI: "UserSession kaydinin omruyle hizali olsun diye 30 gun"
    // diyordu; kayit omru 7 idi. Yorum, kodun YAPMADIGI seyi anlatiyordu.
    //
    // COZUM: deger TEK YERDE durur ve iki taraf da BURADAN okur. Ikinci bir el yazmasi
    // acilmaz - "ayni kuralin ikinci kopyasi" ailesi bu depoda YEDI KEZ bedeli odenmis
    // sinifitir. Core'da durmasinin sebebi: hem Bussiness hem API bu projeye BAGIMLI,
    // tersi degil.
    public static class OturumOmru
    {
        // Refresh penceresi. `user_sessions.expires_at` ve refresh cerezinin `Expires`
        // degeri BUNDAN turer; ikisi AYNI ANDA biter.
        public const int RefreshGun = 7;

        public static TimeSpan RefreshSuresi => TimeSpan.FromDays(RefreshGun);
    }
}
