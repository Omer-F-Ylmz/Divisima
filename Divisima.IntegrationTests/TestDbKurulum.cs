using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Divisima.IntegrationTests
{
    // === TEST VERITABANI KURULUMU - `model` KILIDINE KARSI YENIDEN DENEME ==================
    //
    // OLCULEN ZARAR (Security CI kirmizisi 10d794d): SQL Server `CREATE DATABASE` /
    // `DROP DATABASE` islemlerini **`model` veritabani uzerinden SERILESTIRIR**. Depoda her
    // test SINIFI kendi veritabanini kuruyor (bolum 4 - xUnit siniflari paralel kostugu icin
    // DOGRU tasarim), ve bu 136 DDL cagrisi demek. 47. katilimci eklenince BES AYRI sinif
    // ayni hatayla dustu:
    //
    //   SqlException 1807 : Could not obtain exclusive lock on database 'model'.
    //                       Retry the operation later.
    //
    // Hatanin kendi metni ne yapilmasi gerektigini SOYLUYOR ("Retry the operation later") -
    // 1807 GECICI bir hatadir. Marj bicak sirtiydi: 46 katilimci YESIL, 47 KIRMIZI. Bu yuzden
    // kurulum tek noktaya toplandi ve YALNIZ bu hataya karsi yeniden deneniyor.
    //
    // TASARIM SINIRLARI (kullanici sarti):
    //  1) Yeniden deneme YALNIZ 1807'ye ozel. Baska hicbir hata kodu YUTULMAZ - `SqlException`
    //     bile olsa farkli numarali bir hata ANINDA firlar (pin: `..._BASKA_HATA_KODU_YUTULMAZ`).
    //  2) Sinirli deneme. `MaxDeneme` dolunca hata GURULTULU firlar - sessiz sonsuz dongu YOK
    //     (pin: `..._GURULTULU_DUSER_SESSIZ_SONSUZ_DONGU_YOK`).
    //  3) "Yesil cunku hic 1807 gelmedi" ile "yesil cunku retry calisti" AYIRT EDILEBILIR
    //     olmali: her yeniden deneme SAYILIR ve GORUNUR sekilde raporlanir (asagi).
    //
    // ONEMLI - BU YARDIMCI SORUNU AZALTIR, YOK ETMEZ: en iyi cozum ihtiyac duyulmayan
    // veritabanini HIC KURMAMAKTIR. `ArkaPlanIsleriIzolasyonTests` tam bu yuzden artik sifir
    // DDL uretiyor. Yeni bir test sinifi eklerken once "bu sinifin gercekten veritabanina
    // ihtiyaci var mi?" diye sorulur.
    internal static class TestDbKurulum
    {
        // `model` veritabani uzerinde ozel kilit alinamadi. Belgelenmis GECICI hata.
        public const int ModelKilidiHataKodu = 1807;

        // Alti deneme, artan bekleme + serpinti: en kotu durumda ~3 sn. 136 DDL cagrisinin
        // tamami sirayla `model`'i beklerse bile bu pencere yeter; yetmezse GURULTULU duser.
        public const int MaxDeneme = 6;

        private static int _yenidenDeneme;
        private static int _basariliIslem;

        // OLCUM KANALI (sart 3): kosum sonunda "retry devrede miydi, gerekti mi" sorusu
        // ancak bu sayilarla yanitlanir. Sifir olmasi retry'in OLU oldugunu DEGIL, o kosumda
        // hic 1807 gelmedigini gosterir - ikisi ayri seydir ve raporda ayri yazilir.
        public static int YenidenDenemeSayisi => Volatile.Read(ref _yenidenDeneme);
        public static int BasariliIslemSayisi => Volatile.Read(ref _basariliIslem);

        /// <summary>Veritabanini sifirdan kurar (varsa siler, sonra olusturur).</summary>
        public static async Task YenidenOlusturAsync(DatabaseFacade db)
        {
            await SilAsync(db);
            await OlusturAsync(db);
        }

        public static Task SilAsync(DatabaseFacade db) =>
            DeneAsync(() => db.EnsureDeletedAsync(), ModelKilidiMi, MaxDeneme, kanal: "DROP DATABASE");

        public static Task OlusturAsync(DatabaseFacade db) =>
            DeneAsync(() => db.EnsureCreatedAsync(), ModelKilidiMi, MaxDeneme, kanal: "CREATE DATABASE");

        // Politika, SQL tespitinden AYRI tutuldu ki pinlenebilsin: yeniden deneme davranisi
        // sentetik bir islem + sentetik bir yuklem ile olculur, gercek 1807 uretmeye gerek
        // kalmadan. Donen deger YAPILAN YENIDEN DENEME SAYISIDIR (0 = ilk denemede olmus).
        //
        // `kanal` YALNIZ GERCEK veritabani yolundan doldurulur ve OLCUM KANALINI KORUR:
        // politika pinleri bu metodu SENTETIK bir hatayla (`SahteHata`) cagiriyor; sayaci
        // onlar da artirsaydi "kosumda 1807 geldi mi" olcumu KENDI TESTLERIMLE KIRLENIRDI.
        // Birebir yasandi: ilk kosumda log "retry devreye girdi: 5 kez" dedi, besinin de
        // kaynagi pinlerin sahte hatasiydi - rapor YANLIS yazilacakti.
        internal static async Task<int> DeneAsync(Func<Task> islem, Func<Exception, bool> yenidenDenenebilir,
            int maxDeneme, string? kanal = null)
        {
            for (var deneme = 1; ; deneme++)
            {
                try
                {
                    await islem();
                    if (kanal != null) Interlocked.Increment(ref _basariliIslem);
                    return deneme - 1;
                }
                catch (Exception ex) when (yenidenDenenebilir(ex) && deneme < maxDeneme)
                {
                    if (kanal != null)
                    {
                        var toplam = Interlocked.Increment(ref _yenidenDeneme);
                        Bildir($"[TestDbKurulum] 1807 ({kanal}) - yeniden deneniyor "
                             + $"(deneme {deneme}/{maxDeneme}, kosum toplami {toplam})");
                    }
                    // Artan bekleme + serpinti: serpinti olmadan ayni anda dusen istekler
                    // KILITLI ADIMDA yeniden denerdi ve cakismayi surdururdu.
                    await Task.Delay(TimeSpan.FromMilliseconds(120 * deneme + Random.Shared.Next(0, 180)));
                }
            }
        }

        internal static bool ModelKilidiMi(Exception ex) => HataKoduIceriyorMu(ex, ModelKilidiHataKodu);

        // Ic-istisna zincirini yurur ve `SqlException`larin HATA KOLEKSIYONUNDA aranan numarayi
        // arar. Tek `SqlException` birden cok `SqlError` tasiyabilir; `ex.Number` yalnizca
        // ilkini verir - bu yuzden koleksiyon taranir.
        internal static bool HataKoduIceriyorMu(Exception? ex, int kod)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is not SqlException sql) continue;
                foreach (SqlError hata in sql.Errors)
                    if (hata.Number == kod) return true;
            }
            return false;
        }

        // Yeniden denemeler SESSIZ OLMAMALI: hem kosum ciktisina hem de gecici bir dosyaya
        // yazilir. CI'da ciktiyi TESHIS adimi topluyor; yerelde dosya "retry gercekten
        // devreye girdi mi" sorusunu kosum sonrasi yanitlar. Best-effort - olcum kanalinin
        // kendisi bir testi DUSUREMEZ.
        private static void Bildir(string satir)
        {
            try { Console.Error.WriteLine(satir); } catch { }
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "divisima-testdb-retry.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} pid={Environment.ProcessId} {satir}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
