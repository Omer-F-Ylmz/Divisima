namespace Divisima.Bussiness.Abstract
{
    // Açıklayıcı yorum: Güvenlik olaylarını kaydeder + kritikse admin'e anlık bildirim/mail tetikler.
    public interface ISecurityEventService
    {
        Task LogAsync(string eventType, string severity, int? customerId, string? ip, string? userAgent, string? detail);

        // ══ GF-5 / K2 (D4) - SAHIPLIK IHLALI IZI ═══════════════════════════════════════════
        //
        // OLCULEN ONCE-DURUM (AV-2 / S-C matrisi): "404 sahiplik ihlali" satiri TAM BOSLUKTU -
        // GF-1/K4'un uc noktasinda da iz cagrisi 0 idi. Yani birinin baskasinin siparisini
        // yoklamasi hicbir yerde GORUNMUYORDU.
        //
        // KAPSAM ORDER + PAYMENT ILE SINIRLI - BILINCLI (merkez karari): sahiplik-404 yuzeyi
        // bugun YEDI manager'a dagilmis durumda ve bunlarin ALTISI (Address · Invoice · Return ·
        // Shipment · PriceDrop · StockNotification) AV-2'de KOR KALAN controller'lara ait.
        // Hepsine dokunmak, AV-2'nin ana bulgusunu ("kapsam GENISLEMEDI, YER DEGISTIRDI") fix
        // tarafinda tekrarlardi; ustelik `InvoiceManager` bu dalgada DOKUNULMAZ, yani refactor
        // yapisal olarak TAMAMLANAMAZDI. Kalan yedi nokta BILINEN kalemdir:
        // "sahiplik olayi kapsami Order/Payment (GF-5)".
        //
        // `EnsureOwner` GERI GETIRILMEDI: `SecureControllerBase.cs:40-44` onun BILINCLI olarak
        // kaldirildigini ve sahiplik kontrolunun IS KATMANINDA yapildigini kayda geciriyor
        // (ayni karar `Messages.cs:184-189`da ikinci kez yazili). Bu yardimci yalnizca IZ yazar,
        // KARAR VERMEZ - 404 sozlesmesi ve mesajlar DEGISMEDI.
        //
        // IMZADA `ip` YOK - GEREKCE: GF-5/K1 ile IP/user-agent `SecurityEventManager`in ICINDE
        // dolduruluyor (merkez karari D8, "is katmani HTTP baglamini GORMEZ" sinirini ikinci kez
        // delmemek icin). Cagiran manager'lara `IHttpContextAccessor` enjekte edilmedigi icin
        // gecirecekleri bir deger YOKTUR; parametre eklemek OLU BIR ARGUMAN olurdu (bu depoda
        // `LogAsync`in "sub" dali gibi olu dallar SUPHELI olarak kayitli). Merkez tarifi
        // `(kaynak, id, ip)` diyordu; `ip` yerine ISTEK SAHIBI konuldu - cunku olayin
        // yanitlamasi gereken soru "kim denedi", ve `security_events.customer_id` FK'si
        // Restrict oldugu icin oraya GERCEK bir musteri id'si yazilmalidir.
        //
        // `kaynak` ve `kaynakId` SERBEST METIN DEGILDIR (cagri yerlerinde literal + int):
        // `detail` alanina kullanici girdisi GIRMEZ, dolayisiyla log satiri bolme (log forging)
        // yuzeyi ACILMAZ.
        Task SahiplikIhlaliAsync(string kaynak, int kaynakId, int? istekSahibi);
    }
}
