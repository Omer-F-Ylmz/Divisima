using Microsoft.AspNetCore.Http;

namespace Divisima.Core.Utilities.Http
{
    // ══ ISTEMCI IZI - TEK OKUMA NOKTASI (GF-5 / K1) ════════════════════════════════════════
    //
    // NEDEN VAR: ayni iki deger (IP + user-agent) bu depoda BIRDEN COK yerde okunuyordu ve
    // GF-5/K1 ucuncu bir okuyucu (`SecurityEventManager`) EKLIYORDU. "Ayni kuralin ikinci
    // kopyasi" sinifinin bedeli bu depoda YEDI KEZ odendi; ucuncu kopyayi acmak yerine
    // okuma+kirpma TEK YERE alindi. `AuthManager` artik buraya devrediyor.
    //
    // YENI BAGIMLILIK YOK (00a:180 - LAUNCH ONCESI eklenmez): `Divisima.Core.csproj` ZATEN
    // `<FrameworkReference Include="Microsoft.AspNetCore.App" />` tasiyor, dolayisiyla
    // `IHttpContextAccessor` paket eklemeden kullanilabiliyor (olculdu).
    //
    // X-FORWARDED-FOR BURADA OKUNMAZ - KALICI KARAR (GF-1b/K6'dan devralindi):
    // `Program.cs` ForwardedHeaders middleware'i YALNIZ bilinen proxy'lerden gelen basligi
    // kabul edip `RemoteIpAddress`i ZATEN duzeltiyor; spoofing engeli ORADA, tek yerde.
    // Basligi burada ikinci kez okumak o korumayi ATLAR ve saldirganin yazdigi degeri
    // dogrudan DB'ye gecirirdi.
    //
    // HttpContext YOKSA (arka plan isi, birim testi) null doner - akis BOZULMAZ.
    public static class IstemciBilgisi
    {
        // ══ IP SINIRI 64 -> 60 (GF-5 / K1 - OLCULEN CELISKI) ═══════════════════════════════
        //
        // Bu sabit 64 idi ve `user_sessions.ip_address` (64 karakter) icin DOGRUYDU. Ama K1
        // ayni degeri `security_events.ip_address` kolonuna da yaziyor ve O KOLON 60 KARAKTER
        // (sys.columns'tan olculdu: 120 bayt / nvarchar = 60; varlik sinifindan DEGIL).
        // 64'te birakmak, 61-64 karakterlik bir degerde EF insert-time 500 uretirdi - yani
        // A09 IZ YAZMAYA CALISIRKEN ISTEGI DUSURURDU. `guest_name` (SD-7) ailesinin BIREBIR
        // ayni tuzagi.
        //
        // TEK DEGER SECILDI, KOLON BASINA AYRI DEGIL: 60 her IKI kolona da sigar (60 <= 60
        // ve 60 <= 64) ve iki sabit tutmak "hangisi neredeydi" sorusunu her cagri yerinde
        // yeniden acardi. `user_sessions` tarafinda bu bir SIKILASTIRMADIR ve bilinclidir:
        // kaybedilen 4 karakter, en uzun IPv6 metninin (45 karakter, RFC 4291 + bolge eki)
        // COK USTUNDE - yani gercek bir adres KIRPILMAZ. Kirpma yalnizca anormal/uydurma
        // degerlerde devreye girer.
        public const int IpEnUzun = 60;

        // `user_sessions.device` 200, `security_events.user_agent` 300 karakter - 200 ikisine
        // de sigar, degistirilmedi.
        public const int CihazEnUzun = 200;

        public static string? Ip(IHttpContextAccessor? erisim)
        {
            var ham = erisim?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ham)) return null;
            return ham.Length <= IpEnUzun ? ham : ham.Substring(0, IpEnUzun);
        }

        public static string? UserAgent(IHttpContextAccessor? erisim)
        {
            var ham = erisim?.HttpContext?.Request?.Headers["User-Agent"].ToString();
            if (string.IsNullOrWhiteSpace(ham)) return null;
            return ham.Length <= CihazEnUzun ? ham : ham.Substring(0, CihazEnUzun);
        }
    }
}
