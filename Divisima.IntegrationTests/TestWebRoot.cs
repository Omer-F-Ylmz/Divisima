using System;
using System.IO;

namespace Divisima.IntegrationTests
{
    // === DALGA D / D1 - TEST YUKLEMELERI DEPOYU KIRLETMESIN ==============================
    //
    // OLCULEN SIZINTI: her test kosumu `Divisima.API/wwwroot/uploads/products` altina 64
    // baytlik sahte PNG'ler birakiyordu - olcum aninda **96 dosya** (hepsi 64 bayt) ve
    // `Divisima.IntegrationTests/bin/.../wwwroot/uploads/products` altinda 35 dosya daha.
    // Ikisinin toplami 131 dosyaydi ve HICBIRININ veritabaninda karsiligi yoktu.
    //
    // NEDEN URETIM WWWROOT'UNA YAZIYORDU: Sprint 8 madde 4'te LocalImageStorage DOGRU sekilde
    // `IWebHostEnvironment.WebRootPath`e tasindi (oncesinde CWD'ye yaziyor ve sunum farkli
    // dizinden yapiliyordu - E2b'de canli 404'lerin sebebi buydu). Test host'unun ContentRoot'u
    // `Divisima.API` oldugu icin WebRootPath de `Divisima.API/wwwroot` oluyor. Yani duzeltme
    // dogruydu; eksik olan, TESTIN kendi yuklemelerini AYRI bir koke yazmasiydi.
    //
    // COZUM: her test SURECI icin gecici bir WebRoot. Boylece:
    //   * depo agaci kirlenmiyor,
    //   * Sprint 8 madde 4'un invarianti KORUNUYOR - yazma ve sunum HALA AYNI kokten turer,
    //     yalnizca o kok artik UCUNCU bir dizin (ne CWD ne ContentRoot). Pin bu yuzden
    //     zayiflamiyor, GUCLENIYOR.
    //   * `UseContentRoot(Directory.GetCurrentDirectory())` GERI GELMIYOR - o ayar uretimdeki
    //     gercek ayrismayi testin icinde GIZLIYORDU (Sprint 8 madde 4'un kaldirdigi sey).
    //
    // Dizin isletim sisteminin gecici klasorunde; ayrica surec cikisinda SILINIYOR
    // (kosum artigi birakilmasin). Surec sert oldurulurse dizin OS temp'te kalir - orasi
    // zaten ucucudur.
    internal static class TestWebRoot
    {
        private static readonly Lazy<string> _kok = new(() =>
        {
            var yol = Path.Combine(Path.GetTempPath(), "divisima-test-webroot",
                                   Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(Path.Combine(yol, "uploads", "products"));

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(yol, recursive: true); } catch { /* temizlik best-effort */ }
            };

            return yol;
        });

        public static string Yol => _kok.Value;

        // Pinlerin kullandigi fiziksel yukleme dizini.
        public static string YuklemeDizini => Path.Combine(Yol, "uploads", "products");
    }
}
