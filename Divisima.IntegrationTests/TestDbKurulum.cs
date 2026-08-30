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

        // ══ MF-3 / FF - HAM `CREATE DATABASE ... COLLATE` ICIN ZAMAN ASIMI SINIRI ═════════
        //
        // OLCULEN ZARAR (MF-3 push turu, 0. adim): iki ardisik tam dogrulama BIREBIR
        // olmadi - ucuncu kosumda `SemaTekKaynakTests` DORDUNCU kirmizi olarak dustu ve
        // hata ASSERT DEGIL, KURULUM hatasiydi:
        //     SqlException : Execution Timeout Expired.
        //     Win32Exception : Bekleme islem zamani asildi.
        // Siklik olculdu: sinif TEK BASINA 6/6 yesil (3 sn), tam suitte **3'te 1**.
        //
        // NEDEN MEVCUT AG YAKALAMIYORDU - IKI YAPISAL SEBEP:
        //  (a) O cagri yeri HAM `CREATE DATABASE ... COLLATE Turkish_CI_AS` calistiriyor
        //      (collation ACIKCA verilmeli - CLAUDE.md 6c; EnsureCreated bunu yapamaz),
        //      yani bu yardimcidan HIC GECMIYORDU.
        //  (b) Gecseydi bile yeniden deneme yuklemi YALNIZ 1807'dir ve bu hata TIMEOUT'tur.
        //
        // COZUM DAR TUTULDU: ham CREATE'in TEK EVI burasi olur; zaman asimi yeniden
        // denemesi YALNIZ bu sinirda yasar, GENEL YOLA SIZMAZ. "Yalniz 1807, baska hata
        // yutulmaz" ilkesi (sart 1) AYNEN gecerlidir - `SilAsync`/`OlusturAsync` yuklemleri
        // DEGISMEDI.
        //
        // -2 : `Execution Timeout Expired` - istemci tarafi komut zaman asiminin kanonik
        // SqlException numarasi.
        public const int ZamanAsimiHataKodu = -2;

        // 120 sn = varsayilan 30'un DORT KATI. SECILMIS SABIT, olculmus bir esik DEGIL:
        // gozlenen belirti tek bir `CREATE DATABASE`in `model` kilidi altinda 30 sn'yi
        // asmasiydi; dort kat, ayni cekismenin cok daha agir bir aninda bile pencere
        // birakir. Daha buyuk bir deger kirmizi bir kosumu YAVASLATIR, daha kucugu
        // belirtiyi geri getirir.
        public const int OlusturmaZamanAsimiSaniye = 120;

        // Uc deneme = ilk deneme + EN COK IKI yeniden deneme (merkez tarifi).
        public const int ZamanAsimiMaxDeneme = 3;

        // Olcum kanallari AYRI TUTULUR: `YenidenDenemeSayisi` 1807'e aittir ve CI adimi
        // ciktida `[TestDbKurulum] 1807` satirini ARIYOR. Zaman asimi denemelerini ayni
        // sayaca/etikete yazmak o kanali KIRLETIRDI - "1807 atesledi mi" sorusu yanlis
        // yanitlanirdi.
        public const string ZamanAsimiEtiketi = "TIMEOUT";

        // Alti deneme, artan bekleme + serpinti: en kotu durumda ~3 sn. 136 DDL cagrisinin
        // tamami sirayla `model`'i beklerse bile bu pencere yeter; yetmezse GURULTULU duser.
        public const int MaxDeneme = 6;

        private static int _yenidenDeneme;
        private static int _basariliIslem;
        private static int _zamanAsimiYenidenDeneme;

        // OLCUM KANALI (sart 3): kosum sonunda "retry devrede miydi, gerekti mi" sorusu
        // ancak bu sayilarla yanitlanir. Sifir olmasi retry'in OLU oldugunu DEGIL, o kosumda
        // hic 1807 gelmedigini gosterir - ikisi ayri seydir ve raporda ayri yazilir.
        public static int YenidenDenemeSayisi => Volatile.Read(ref _yenidenDeneme);
        public static int BasariliIslemSayisi => Volatile.Read(ref _basariliIslem);
        // AYRI KANAL (MF-3/FF): zaman asimi denemeleri 1807 sayacina KARISMAZ.
        public static int ZamanAsimiYenidenDenemeSayisi => Volatile.Read(ref _zamanAsimiYenidenDeneme);

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

        /// <summary>
        /// Collation'i ACIKCA verilen bos bir veritabani olusturur. HAM
        /// `CREATE DATABASE ... COLLATE` YAZAN TEK YER BURASIDIR (kapsam pini bunu tarar).
        /// EnsureCreated collation veremedigi icin bu yol AYRI durur; karsiligi olarak
        /// zaman asimi siniri ve SINIRLI yeniden deneme BURADA yasar.
        /// </summary>
        public static Task CollationIleOlusturAsync(string masterBaglantiDizesi, string dbAdi, string collation) =>
            DeneAsync(async () =>
            {
                await using var master = new SqlConnection(masterBaglantiDizesi);
                await master.OpenAsync();
                await using var cmd = master.CreateCommand();
                cmd.CommandTimeout = OlusturmaZamanAsimiSaniye;
                cmd.CommandText = $"CREATE DATABASE [{dbAdi}] COLLATE {collation};";
                await cmd.ExecuteNonQueryAsync();
            }, ZamanAsimiMi, ZamanAsimiMaxDeneme,
               kanal: "CREATE DATABASE COLLATE", hataEtiketi: ZamanAsimiEtiketi);

        // Politika, SQL tespitinden AYRI tutuldu ki pinlenebilsin: yeniden deneme davranisi
        // sentetik bir islem + sentetik bir yuklem ile olculur, gercek 1807 uretmeye gerek
        // kalmadan. Donen deger YAPILAN YENIDEN DENEME SAYISIDIR (0 = ilk denemede olmus).
        //
        // `kanal` YALNIZ GERCEK veritabani yolundan doldurulur ve OLCUM KANALINI KORUR:
        // politika pinleri bu metodu SENTETIK bir hatayla (`SahteHata`) cagiriyor; sayaci
        // onlar da artirsaydi "kosumda 1807 geldi mi" olcumu KENDI TESTLERIMLE KIRLENIRDI.
        // Birebir yasandi: ilk kosumda log "retry devreye girdi: 5 kez" dedi, besinin de
        // kaynagi pinlerin sahte hatasiydi - rapor YANLIS yazilacakti.
        //
        // `hataEtiketi` OLCUM KANALINI AYIRIR (MF-3/FF): CI adimi ciktida
        // `[TestDbKurulum] 1807` satirini ariyor. Zaman asimi denemeleri ayni etiketle
        // yazilsaydi "1807 atesledi mi" sorusu YANLIS yanitlanirdi.
        internal static async Task<int> DeneAsync(Func<Task> islem, Func<Exception, bool> yenidenDenenebilir,
            int maxDeneme, string? kanal = null, string hataEtiketi = "1807")
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
                        var toplam = hataEtiketi == ZamanAsimiEtiketi
                            ? Interlocked.Increment(ref _zamanAsimiYenidenDeneme)
                            : Interlocked.Increment(ref _yenidenDeneme);
                        Bildir($"[TestDbKurulum] {hataEtiketi} ({kanal}) - yeniden deneniyor "
                             + $"(deneme {deneme}/{maxDeneme}, kosum toplami {toplam})");
                    }
                    // Artan bekleme + serpinti: serpinti olmadan ayni anda dusen istekler
                    // KILITLI ADIMDA yeniden denerdi ve cakismayi surdururdu.
                    await Task.Delay(TimeSpan.FromMilliseconds(120 * deneme + Random.Shared.Next(0, 180)));
                }
            }
        }

        internal static bool ModelKilidiMi(Exception ex) => HataKoduIceriyorMu(ex, ModelKilidiHataKodu);

        // MF-3/FF: YALNIZ komut zaman asimi. Ayni tarayiciyi kullanir, yani ic-istisna
        // zincirini yurur ve `SqlError` koleksiyonunu tarar - "Execution Timeout Expired"
        // ic istisnada gomulu geldiginde de bulunur (olculen belirti tam boyleydi).
        internal static bool ZamanAsimiMi(Exception ex) => HataKoduIceriyorMu(ex, ZamanAsimiHataKodu);

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
