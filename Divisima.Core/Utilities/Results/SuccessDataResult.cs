namespace Divisima.Core.Utilities.Results
{
    // Açıklayıcı yorum: Başarılı sonuç + veri.
    //
    // SPRINT 8 MADDE 11 - KURUCU SETI YENIDEN TASARLANDI (kok cozum).
    //
    // ONCEKI SET DORT KURUCUYDU:
    //     (T data, string message) / (T data) / (string message) / ()
    // "T = string" oldugunda "(T data)" ile "(string message)" AYNI IMZAYA duser; C# asiri
    // yukleme cozumu bu durumda generic OLMAYAN adayi secer. Yani
    //     new SuccessDataResult<string>(html)
    // dizeyi MESSAGE'a yazar, DATA null kalir - ve Success yine true oldugu icin HATA SESSIZDIR.
    //
    // E3'te bu tuzagin bedeli UCTA olculdu:
    //   GET /api/order/{id}/invoice-html  -> HTTP 200 ama Content-Length: 0 ("Faturalarim" ekrani
    //                                        hic calismamisti)
    //   GET /api/referral/my-code         -> {"data":null,"success":true,"message":"REF..."}
    // E3 yalniz o iki CAGRI YERINI "data:" adlandirilmis argumana cevirdi; belirsizligin KENDISI
    // dilde kaldi ve yeni yazilacak tek argumanli bir string cagrisi yine sessizce bozulacakti.
    //
    // KOK COZUM: belirsizligi ureten "(string message)" kurucusu KALDIRILDI. Depo tarandi -
    // OLCULDU: bu kurucunun cagrisi SIFIR (mesaj niyetli tek argumanli cagrilarin tamami
    // ErrorDataResult uzerinde). Kullanilmayan parametresiz kurucu da kaldirildi (0 cagri).
    // Geriye kalan iki kurucu HICBIR T icin cakismaz: biri iki parametreli, digeri tek
    // parametreli ve o parametre HER ZAMAN veridir.
    //
    // Basarili bir sonuca MESAJ eklemek icin iki argumanli kurucu kullanilir:
    //     new SuccessDataResult<string>(kod, Messages.X)
    public class SuccessDataResult<T> : DataResult<T>
    {
        public SuccessDataResult(T data, string message) : base(data, true, message) { }
        public SuccessDataResult(T data) : base(data, true) { }
    }
}
