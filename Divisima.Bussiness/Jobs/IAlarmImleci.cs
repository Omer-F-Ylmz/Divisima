namespace Divisima.Bussiness.Jobs
{
    // MON-1 / D1: KritikOlayAlarmJob'un "son islenen security_events.id" imleci.
    //
    // NEDEN AYRI ARAYUZ: kalici deger YENI KOLON/TABLO olmadan tutulmali (migration YOK) ve
    // onbellekte tutulamaz - Redis yeniden baslarsa imlec kaybolur, ilk tur ya ayni olaylari
    // yeniden bildirir ya da tabani sifirdan kurup araya dusenleri YUTAR. Uretim uygulamasi
    // Hangfire'in kendi SQL deposundaki hash'tir (`Divisima.API.Services.HangfireAlarmImleci`);
    // Bussiness Hangfire paketine BAGIMLI DEGIL, o yuzden sozlesme burada, uygulama API'de.
    public interface IAlarmImleci
    {
        // Hic yazilmamissa null (ilk kosum).
        Task<int?> OkuAsync();

        Task YazAsync(int sonId);
    }
}
