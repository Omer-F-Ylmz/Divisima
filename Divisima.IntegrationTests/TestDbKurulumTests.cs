using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Divisima.IntegrationTests
{
    // === `model` KILIDI YENIDEN DENEMESININ PINLERI ======================================
    //
    // Security CI kirmizisi 10d794d: 136 DDL cagrisi SQL Server'in `model` veritabani
    // uzerinden serilesiyor; 47. katilimci eklenince bes AYRI sinif
    // "Could not obtain exclusive lock on database 'model'" (SqlException 1807) ile dustu.
    //
    // Bu pinler UC seyi ayri ayri tutar:
    //   (1) yeniden deneme YALNIZ 1807'ye ozel - baska hata kodu YUTULMAZ,
    //   (2) politika gercekten yeniden deniyor VE sinirli deneme sonrasi GURULTULU dusuyor,
    //   (3) hicbir test sinifi yardimciyi ATLAMIYOR (kaynak taramasi).
    //
    // "Retry GERCEKTEN devreye girdi mi" sorusu bir PIN ile yanitlanamaz - o kosuma bagli.
    // Onun kanali AYRI: `TestDbKurulum.YenidenDenemeSayisi` sayaci + her denemede basilan
    // `[TestDbKurulum] 1807 - yeniden deneniyor` satiri. Sifir olmasi retry'in OLU oldugunu
    // DEGIL, o kosumda hic 1807 gelmedigini gosterir; ikisi raporda AYRI yazilir.
    [Trait("Category", "Sql")]
    public class TestDbKurulumTests
    {
        private static readonly string? ExplicitConn = Environment.GetEnvironmentVariable("DIVISIMA_TEST_SQL");

        private sealed class SahteHata : Exception
        {
            public SahteHata(string m) : base(m) { }
        }

        // ── (1) YALNIZ 1807 ───────────────────────────────────────────────────────────────
        // GERCEK bir SqlException uretilir (sifira bolme -> hata 8134) ve yuklem ona
        // "yeniden denenebilir" DEMEMELIDIR. Boylece "her SqlException'i yut" gibi bir
        // uygulama bu pini GECEMEZ.
        [Fact]
        public async Task YENIDEN_DENEME_YALNIZ_1807_ICIN_BASKA_HATA_KODU_YUTULMAZ()
        {
            var conn = MasterBaglantisi();
            if (conn == null) return;   // yerelde SQL yoksa; DIVISIMA_TEST_SQL varsa asagida patlar

            SqlException? gercekHata = null;
            try
            {
                await using var c = new SqlConnection(conn);
                await c.OpenAsync();
                await using var cmd = new SqlCommand("SELECT 1/0;", c);
                await cmd.ExecuteScalarAsync();
            }
            catch (SqlException ex) { gercekHata = ex; }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ExplicitConn))
            {
                throw new InvalidOperationException(
                    "DIVISIMA_TEST_SQL verildi ancak SQL Server'a baglanilamadi - ATLANMAMALI.", ex);
            }
            catch { return; }

            gercekHata.Should().NotBeNull("sifira bolme GERCEK bir SqlException uretmeli");

            // VAKUM KIRICI: tarayici gercekten calisiyor olmali - kendi numarasini BULMALI.
            // Bu assert olmasaydi "her zaman false don" uygulamasi da pini gecerdi.
            var kendiNumarasi = gercekHata!.Number;
            TestDbKurulum.HataKoduIceriyorMu(gercekHata, kendiNumarasi).Should().BeTrue(
                $"istisna zinciri taranmali ve kendi hata numarasi ({kendiNumarasi}) bulunmali");

            // CIFT-ANLAM KIRICI: ayni GERCEK SqlException 1807 SAYILMAMALI.
            TestDbKurulum.HataKoduIceriyorMu(gercekHata, TestDbKurulum.ModelKilidiHataKodu).Should().BeFalse(
                "1807 DISINDAKI bir SQL hatasi 'model kilidi' sayilmamali - yoksa gercek hatalar YUTULUR");
            TestDbKurulum.ModelKilidiMi(gercekHata).Should().BeFalse(
                "yeniden deneme yuklemi baska hata kodlarina EVET dememeli");

            TestDbKurulum.ModelKilidiHataKodu.Should().Be(1807,
                "yeniden deneme YALNIZ 'Could not obtain exclusive lock on database model' icin");
        }

        // ── (2) POLITIKA: YENIDEN DENER ve BASARIR ────────────────────────────────────────
        [Fact]
        public async Task YENIDEN_DENENEBILIR_HATA_TEKRAR_DENENIR_ve_SONUNDA_BASARIR()
        {
            var cagri = 0;
            var yapilanDeneme = await TestDbKurulum.DeneAsync(
                islem: () =>
                {
                    cagri++;
                    if (cagri < 3) throw new SahteHata("gecici");
                    return Task.CompletedTask;
                },
                yenidenDenenebilir: _ => true,
                maxDeneme: 6);

            // VAKUM KIRICI: islem GERCEKTEN uc kez cagrilmis olmali. Yalnizca donen sayiya
            // bakan bir assert, hicbir sey yapmayan bir uygulamayla da yesil kalabilirdi.
            cagri.Should().Be(3, "iki basarisiz denemeden sonra ucuncusu basarmali");
            yapilanDeneme.Should().Be(2, "yapilan YENIDEN DENEME sayisi donmeli");
        }

        // ── (3) BASKA HATA ANINDA FIRLAR ──────────────────────────────────────────────────
        [Fact]
        public async Task YENIDEN_DENENEMEYEN_HATA_ANINDA_FIRLAR_YUTULMAZ()
        {
            var cagri = 0;
            Func<Task> eylem = () => TestDbKurulum.DeneAsync(
                islem: () => { cagri++; throw new SahteHata("kalici"); },
                yenidenDenenebilir: _ => false,
                maxDeneme: 6);

            await eylem.Should().ThrowAsync<SahteHata>(
                "yeniden denenemeyen hata OLDUGU GIBI cagirana ulasmali");
            cagri.Should().Be(1, "yeniden denenemeyen hatada TEK deneme yapilmali - bekleme bile olmamali");
        }

        // ── (4) SINIRLI DENEME - SESSIZ SONSUZ DONGU YOK ──────────────────────────────────
        [Fact]
        public async Task SINIRLI_DENEME_SONRASI_GURULTULU_DUSER_SESSIZ_SONSUZ_DONGU_YOK()
        {
            var cagri = 0;
            Func<Task> eylem = () => TestDbKurulum.DeneAsync(
                islem: () => { cagri++; throw new SahteHata("hep basarisiz"); },
                yenidenDenenebilir: _ => true,
                maxDeneme: 4);

            await eylem.Should().ThrowAsync<SahteHata>(
                "deneme hakki bitince hata YUTULMAZ - kosum GURULTULU duser");
            cagri.Should().Be(4, "tam olarak maxDeneme kadar denenmeli - ne eksik ne sonsuz");

            TestDbKurulum.MaxDeneme.Should().BeInRange(2, 10,
                "uretim degeri makul bir aralikta olmali - sinirsiz yeniden deneme YASAK");
        }

        // ── (5) KAPSAM: HICBIR SINIF YARDIMCIYI ATLAMAZ ───────────────────────────────────
        // Bu pin, mekanik degisikligin SESSIZCE eskimesini engeller: yarin bir sinif yine
        // dogrudan `EnsureCreatedAsync` cagirirsa kirilir.
        [Fact]
        public void HICBIR_TEST_SINIFI_KURULUM_YARDIMCISINI_ATLAMAZ()
        {
            var dizin = TestProjesiDizini();
            var dosyalar = Directory.GetFiles(dizin, "*.cs", SearchOption.TopDirectoryOnly);

            // VAKUM KIRICI: tarama gercekten dosya okumus olmali.
            dosyalar.Length.Should().BeGreaterThan(40,
                "tarama test projesinin kaynaklarini GERCEKTEN okumali - bos tarama vakumdur");

            // DESENLER CALISMA ANINDA KURULUR - BILINEN TUZAK (CLAUDE.md'de kayitli, DORDUNCU
            // tekrari): kaynak tarayan bir pin, kendi ICINDEKI desen metnini de bulur ve
            // YANLIS KIRMIZI verir. Ilk yazimda tam bu oldu ("found {TestDbKurulumTests.cs}").
            // Cozum olarak dosyayi DISLAMAK secilmedi - bir allowlist, o dosyaya yarin girecek
            // GERCEK bir ihlali de gizlerdi. Desen bolunerek yaziliyor: dosyada butun hali
            // GECMIYOR, dolayisiyla tarama kendini gormuyor ve hicbir dosya muaf degil.
            var silmeDeseni = ".Database." + "EnsureDeletedAsync()";
            var olusturmaDeseni = ".Database." + "EnsureCreatedAsync()";

            var atlayanlar = new List<string>();
            var yardimciCagrisi = 0;

            foreach (var yol in dosyalar)
            {
                var ad = Path.GetFileName(yol);
                var metin = File.ReadAllText(yol);

                yardimciCagrisi += Say(metin, "TestDbKurulum.SilAsync(")
                                 + Say(metin, "TestDbKurulum.OlusturAsync(")
                                 + Say(metin, "TestDbKurulum.YenidenOlusturAsync(");

                if (ad == "TestDbKurulum.cs") continue;   // yardimcinin KENDISI

                if (metin.Contains(silmeDeseni, StringComparison.Ordinal)
                 || metin.Contains(olusturmaDeseni, StringComparison.Ordinal))
                    atlayanlar.Add(ad);
            }

            // VAKUM KIRICI: yardimci GERCEKTEN kullaniliyor olmali. Bu olmadan "hic kimse
            // dogrudan cagirmiyor" iddiasi, kimse veritabani kurmuyor olsa da dogru cikardi.
            yardimciCagrisi.Should().BeGreaterThan(100,
                "test siniflari veritabani kurulumunu GERCEKTEN yardimci uzerinden yapmali");

            atlayanlar.Should().BeEmpty(
                "veritabani kurulumu TEK NOKTADAN gecmeli - dogrudan EnsureDeleted/EnsureCreated "
              + "cagiran sinif `model` kilidi yeniden denemesinden YARARLANAMAZ (CI kirmizisi 10d794d)");
        }

        private static int Say(string metin, string desen)
        {
            var n = 0;
            for (var i = metin.IndexOf(desen, StringComparison.Ordinal); i >= 0;
                     i = metin.IndexOf(desen, i + desen.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        private static string TestProjesiDizini()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "frontend", "index.html")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: frontend/index.html iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pin kaynagi okuyamadan yesil kalamaz.");
            return Path.Combine(d.FullName, "Divisima.IntegrationTests");
        }

        // Veritabani ADI vermez - bu sinif KASITLI olarak hicbir veritabani kurmaz
        // (kurulum yukunu artirmak, tam da duzeltilen sorunu geri getirirdi).
        private static string? MasterBaglantisi()
        {
            var b = string.IsNullOrWhiteSpace(ExplicitConn)
                ? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
                : ExplicitConn;
            try
            {
                return new SqlConnectionStringBuilder(b)
                {
                    InitialCatalog = "master",
                    ConnectTimeout = 5
                }.ConnectionString;
            }
            catch { return null; }
        }
    }
}
