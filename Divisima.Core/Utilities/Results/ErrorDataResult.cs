namespace Divisima.Core.Utilities.Results
{
    // Açıklayıcı yorum: Hatalı sonuç + veri (genelde default).
    //
    // SPRINT 8 MADDE 11 - KURUCU SETI YENIDEN TASARLANDI (kok cozum).
    //
    // ONCEKI SET DORT KURUCUYDU:
    //     (T data, string message) / (T data) / (string message) / ()
    // Kardesi SuccessDataResult ile AYNI tuzagi tasiyordu: "T = string" oldugunda "(T data)" ile
    // "(string message)" ayni imzaya duser ve C# generic OLMAYAN adayi secer. Bugun bir
    // ErrorDataResult<string> cagrisi YOK, ama tuzak dilde duruyordu - ilk yazilacak cagri
    // sessizce yanlis kurucuya duserdi.
    //
    // KOK COZUM: veri tasiyan iki kurucu KALDIRILDI. Depo tarandi - OLCULDU: `ErrorDataResult`
    // cagrilarinin TAMAMI (23 adet) tek argumanli ve hepsi `Messages.X` geciyor, yani hepsi
    // MESAJ niyetli. Veri gecen ya da iki argumanli TEK BIR cagri bile yok; parametresiz kurucu
    // da hic kullanilmamis. Geriye tek kurucu kaldigi icin belirsizlik ihtimali de kalmadi:
    // tek argumanli bir cagri HER ZAMAN mesajdir.
    //
    // Ileride veri TASIYAN bir hata sonucu gerekirse, bu kurucular geri EKLENMEZ - acikca
    // ayirt edilebilir bir fabrika eklenir (ornegin `ErrorDataResult<T>.WithData(data, message)`).
    // Aksi halde ayni sessiz hata sinifi geri gelir.
    public class ErrorDataResult<T> : DataResult<T>
    {
        public ErrorDataResult(string message) : base(default!, false, message) { }
    }
}
