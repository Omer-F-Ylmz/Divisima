namespace Divisima.Core.Utilities.Mail
{
    // LAUNCH-FIX DALGA A / A1(c) - E-POSTADAKI BAGLANTILARIN TEK KAYNAGI.
    //
    // OLCULEN ENGEL: kapsama denetiminde auth maillerinin (dogrulama, sifre sifirlama, 2FA)
    // govdesinde HICBIR baglanti olmadigi olculdu - govde yalnizca ciplak bir jeton tasiyordu
    // ("Hesabinizi dogrulamak icin token: <token>"), ne URL ne yonerge vardi.
    //
    // IKI AYRI ORIGIN VAR VE BU BIR CELISKI DEGIL - OLCULDU:
    //   VITRIN  (Storefront:BaseUrl)  -> kullanicinin ACACAGI SAYFA. Ayni ayari
    //                                    PaymentController.Callback yonlendirmesi de kullanir,
    //                                    yani vitrin origin'inin TEK KAYNAGI zaten budur.
    //   API     (Api:PublicBaseUrl -> Storage:PublicBaseUrl) -> dogrudan bir API UCUNA giden
    //                                    baglantilar (abonelikten cikma). Bu kalip Sprint 8
    //                                    madde 10'da kurulmustu; burada AYNEN korunuyor.
    // Yeni/ucuncu bir sabit origin EKLENMEZ. Kaynak dosyada tek bir "http://..." literali yoktur.
    //
    // BOS ORIGIN'DE GURULTULU: her iki metot da null doner VE LogError basar. Cagiran, kullaniciya
    // gosterilecek yedek yonergeyi kendi yazar (metin baglama gore degisir); sessizce bos bir
    // baglanti ya da yarim URL URETILMEZ.
    public interface IMailLinkBuilder
    {
        // hashYolu ornegi: "#/dogrula/<token>". Bas taraftaki '/' cakismasi burada cozulur.
        string? VitrinBaglantisi(string hashYolu);

        // yolVeSorgu ornegi: "/api/StockNotification/unsubscribe?token=<...>"
        string? ApiBaglantisi(string yolVeSorgu);
    }
}
