using Divisima.Core.Entities.Abstract;

namespace Divisima.Core.Security.JWT
{
    // Açıklayıcı yorum: JWT üretim arayüzü (Cafixo ITokenHelper kalıbı). JwtHelper implemente eder.
    public interface ITokenHelper
    {
        // ══ GF-1 / K3 (C-2) - `authTime` VARSAYILAN DEGERLI EKLENDI ════════════════════════
        //
        // Verilirse jetonun `auth_time` claim'i O DEGERDEN uretilir; verilmezse (null) jeton
        // URETIM ANI kullanilir - yani ESKI davranis. Varsayilan deger BILINCLIDIR: satici
        // tarafi (`SellerAuthManager`) bu arayuzu cagiriyor ve GF-1'de DOKUNULMAZ; parametre
        // zorunlu olsaydi o dosya DERLENMEZDI.
        AccessToken CreateToken(IUser user, DateTime? authTime = null);
    }
}
