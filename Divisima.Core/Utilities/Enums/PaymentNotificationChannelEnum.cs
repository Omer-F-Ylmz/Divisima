namespace Divisima.Core.Utilities.Enums
{
    // ══ ODEME BILDIRIMININ GELDIGI KANAL - SPRINT 8 SONRASI MINI DALGA ═══════════════════
    //
    // HandleCallback'in gevsettigi iki savunma AYNI TEK OLGUDAN turer: bildirimi HANGI KANAL
    // getirdi. Onceden bu bilgi tek bir `bool imzaZorunlu` ile tasiniyordu ve OLCULDU ki artik
    // ayirt etmiyor: madde 9'dan sonra HER IKI uretim cagri yeri de `false` veriyordu
    // (PaymentController.Callback ve .Webhook). Yani parametre "kanal" degil yalnizca
    // "uretim mi, dogrudan servis testi mi" ayrimini yapiyordu - adiyla soyledigi sey bu degildi.
    //
    // Iki `bool` (imzaZorunlu + tokenYasiSiniriUygula) yerine TEK enum secildi. Gerekce:
    //  - GECERSIZ BILESIM YAZILAMAZ. Iki bool ile `imzaZorunlu: true, tokenYasi: false` gibi
    //    hicbir kanalin karsiligi olmayan bir bilesim yazilabilirdi.
    //  - Politika TEK YERDE turer (IyzicoPaymentManager.HandleCallback basi), cagri yerlerinde
    //    dagilmaz; yeni bir politika eklenince cagri yerleri DEGISMEZ.
    //  - Bu depoda "bir bayragin/asiri yuklemenin sessizce yanlis anlama gelmesi" bedeli bir kez
    //    odendi (SuccessDataResult<string> belirsizligi, Sprint 8 madde 11). Ayni tuzagi bilerek
    //    tekrarlamiyoruz.
    //
    // VARSAYILAN `Strict` - FAIL-CLOSED. Yeni bir cagiran dusunmeden yazarsa EN GUVENLI
    // davranisi alir; gevseme her zaman ACIKCA secilir.
    public enum PaymentNotificationChannel
    {
        // Tum savunmalar ACIK: imza ZORUNLU + token yasi siniri (30 dk) UYGULANIR.
        // Dogrudan servis cagrilari (ve pinler) buraya duser.
        Strict = 0,

        // TARAYICI callback'i (Iyzico CF, musterinin tarayicisi POST eder).
        //  - Imza ZORUNLU DEGIL: olculdu (E2b), CF callback govdesinde "signature" alani YOK.
        //    Imza GELIRSE yine dogrulanir.
        //  - Token yasi siniri AYNEN UYGULANIR. Bu yol bir TARAYICIDAN gelir; eski bir formun
        //    yeniden gonderilmesi (replay) gercek bir senaryodur ve 30 dk siniri ona karsidir.
        BrowserCallback = 1,

        // SAGLAYICI webhook'u (Iyzico bant-disi bildirimi, sunucu-sunucu).
        //  - Imza ZORUNLU DEGIL: olculdu (Sprint 8 madde 9), gercek bildirimde imza HIC YOK.
        //    Imza GELIRSE yine dogrulanir.
        //  - TOKEN YASI SINIRI UYGULANMAZ. Gerekce (SUPHELI #15 - olculdu): 30 dk siniri
        //    TARAYICI replay'i icin dogru bir savunmadir, ama webhook FARKLI zamanlama
        //    karakteristigine sahip bir kanaldir - saglayici bildirimi geciktirebilir ya da
        //    saatler sonra yeniden deneyebilir. Sinir burada da uygulanirsa GECIKMIS ama
        //    GERCEK bir bildirim, parasi ALINMIS bir odemeyi "Failed" diye defterler ve
        //    mutabakat kaybedilir. Siparis #33 canli ornegi buydu.
        //    OTORITE ZATEN RETRIEVE: sunucu-sunucu sorgu odemenin GERCEK durumunu soyler.
        //    Gevseyen TEK sey yas siniridir; "yalniz Pending islenir" + tutar + para birimi +
        //    fraud kontrolleri AYNEN durur.
        ProviderWebhook = 2
    }
}
