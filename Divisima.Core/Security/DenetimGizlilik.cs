using System;
using System.Collections.Generic;

namespace Divisima.Core.Security
{
    // ═══ FIX-1A / F2 + F3 - DENETIM IZI GIZLILIGININ TEK KAYNAGI ═══════════════════════════
    //
    // FAZ 1'de OLCULDU (canli, dev veritabani):
    //   * `audit_logs.changes` her Customer degisikliginde TUM varligi (35 alan) tasiyordu ve
    //     icinde `password_hash.old` + `password_hash.new` (88'er karakter, FARKLI degerler) ile
    //     `password_salt` (357 karakter) DURUYORDU. Yani denetim izi, musterinin GECMIS VE GUNCEL
    //     parola ozet+tuzunu saklayan ikincil bir kimlik deposuna donusmustu.
    //   * Ayni sinif tek tabloya ozgu DEGILDI: `UserSession.changes` 33 satirda `refresh_token`,
    //     `CustomerDevice.changes` 3 satirda `device_token` tasiyordu.
    //   * KVKK silmesinden SONRA silinen hesabin e-postasi 2, adi 3, telefonu 9, acik adres metni
    //     1 satirda audit_logs'ta KALIYORDU; silme isleminin KENDI audit satiri da `old`
    //     degerlerinde silinen PII'yi yeniden kaydediyordu.
    //
    // BU SINIF O IKI KARARIN TEK KAYNAGIDIR. Yeni bir alan eklendiginde BURADAN buyur; iki ayri
    // liste tutmak bu depoda defalarca isirdi (bkz. F1 - ayni kuralin ikinci kopyasi).
    //
    // ESLESME ORDINAL ve BUYUK/KUCUK HARF DUYARSIZ (CLAUDE.md bolum 6c): alan adi bir MAKINE
    // dizgesidir, kulturlu casing YASAK - uygulama tr-TR'ye pinli oldugu icin `ToLower()` ile
    // `I` -> `ı` olur ve `IpAddress` gibi bir ad eslesmeden KACARDI.
    public static class DenetimGizlilik
    {
        // Denetim kaydinda deger yerine yazilan SABIT isaret. "Bir sey degisti" izi kalir,
        // degerin KENDISI (uzunlugu, ozeti, kirpilmis hali DAHIL) HIC yazilmaz.
        public const string Isaret = "[REDACTED]";

        // ── (1) SIR ALANLARI: denetim kaydina HIC GIRMEZ ───────────────────────────────────
        // Bunlar kimlik dogrulama sirlaridir. Bir kez yazildiklarinda, o satiri okuyan herkes
        // musterinin hesabina erisebilir (parola ozeti+tuzu -> cevrimdisi kaba kuvvet;
        // refresh_token -> dogrudan oturum devralma). Maskeleme URETIM NOKTASINDA yapilir:
        // interceptor bu alanlari SERILESTIRMEZ, dolayisiyla degerleri DB'ye hic inmez.
        public static readonly IReadOnlyCollection<string> SirAlanlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Customer
            "password_hash",
            "password_salt",
            "two_factor_secret",
            "two_factor_code",
            "email_verification_token",
            "password_reset_token",
            // UserSession - canli oturum kimlik bilgisi (FAZ 1'de 33 satirda olculdu)
            "refresh_token",
            // CustomerDevice - kalici cihaz tanimlayicisi / push kimlik bilgisi
            "device_token",
            // Payment - saglayici oturum jetonu. Depo bunu zaten KanitMaskesi ile maskeliyor;
            // denetim izinde ciplak birakmak o kurali bir kanal oteden delerdi.
            "token"
        };

        // ── (2) KISISEL ALANLAR: normal yazilir, SILMEDE redakte edilir ────────────────────
        // Bunlar mesru denetim verisidir (kim neyi ne zaman degistirdi) ama KVKK unutulma
        // hakki kullanildiginda kisiyi tanimlanabilir kilarlar. Satir SILINMEZ - id/action/
        // entity_id/created_at/user_id korunur, yalnizca DEGERLER isaretle degistirilir.
        // KAPSAM TABLO ILE SINIRLIDIR (bkz. RedaksiyonTablolari): "name" gibi genel bir ad
        // Product/Category satirlarini vurmaz.
        public static readonly IReadOnlyCollection<string> KisiselAlanlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Customer
            "name", "email", "phone", "address", "city", "birthdate", "gender", "referral_code",
            // Address
            "full_name", "full_address", "title", "district", "zip_code",
            // UserSession
            "ip_address", "device"
        };

        // ── (3) REDAKSIYON KAPSAMI: musteriye AIT varliklarin tablolari ────────────────────
        // FAZ 1'de OLCULDU: ticari tablolarin (Order / Invoice / CartItem / Payment) `changes`
        // payload'lari musteri PII'si TASIMIYOR - yalniz id, tutar, durum, siparis/fatura no,
        // sirket unvani ve vergi no (bunlar SIRKETIN mali beyanidir, yasal saklama kapsaminda).
        // Ayrica `user_id` ekseninde bu dort tablonun DISINDA kalan satir sayisi 0 olculdu.
        // Bu yuzden redaksiyon ENTITY eksenindedir ve ticari kayda DOKUNMAZ.
        public static readonly IReadOnlyCollection<string> RedaksiyonTablolari = new HashSet<string>(StringComparer.Ordinal)
        {
            "Customer", "Address", "UserSession", "CustomerDevice"
        };

        public static bool SirMi(string alanAdi) => alanAdi != null && ((HashSet<string>)SirAlanlari).Contains(alanAdi);

        public static bool KisiselMi(string alanAdi) => alanAdi != null && ((HashSet<string>)KisiselAlanlar).Contains(alanAdi);

        // Redaksiyonda hem SIR hem KISISEL alanlar isaretlenir: eski satirlar (bu duzeltmeden
        // ONCE yazilmis olanlar) hala sir tasiyor olabilir; silme onlari da temizlemelidir.
        public static bool RedakteEdilmeli(string alanAdi) => SirMi(alanAdi) || KisiselMi(alanAdi);
    }
}
