using System.Security.Cryptography;

namespace Divisima.Core.Utilities.Security
{
    // SPRINT 8 MADDE 10 - ABONELIKTEN CIKMA JETONU URETICISI.
    //
    // Neden jeton, neden kimlik dogrulamasi DEGIL: stok/fiyat abonelikleri ANONIM kurulabiliyor
    // (uclar AllowAnonymous, kayit yalnizca e-posta ile). Cikma yolunu kimlik dogrulamasina
    // baglamak, uye OLMAYAN bir abonenin verdigi izni GERI ALAMAMASI demek olurdu.
    // "E-posta + urun ile cikma" da secilemezdi: o zaman herkes herkesi abonelikten cikarabilir
    // ve uc, "bu e-posta abone mi?" sorusuna yanit veren bir SIZINTI KANALI haline gelirdi.
    //
    // Jeton TAHMIN EDILEMEZ olmali - kriptografik rastgele. HEX secildi (base64 DEGIL):
    // deger e-postadaki bir URL'de tasiniyor ve base64'un "+", "/", "=" karakterleri URL
    // kacislari yuzunden bozulabiliyor. Hex yalniz [0-9A-F] uretir.
    // 32 bayt = 64 hex karakter; kolon uzunlugu da 64.
    public static class UnsubscribeToken
    {
        public static string Yeni() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
