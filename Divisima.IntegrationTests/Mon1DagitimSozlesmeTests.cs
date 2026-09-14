using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ MON-1 TUR 2 - DAGITIM / BELGE SOZLESMELERI ═══════════════════════════════════════════
    //
    // Denetcilerin (L3 · kural-uyum · rapor) ve canli olcumun buldugu dort kusurun pinleri.
    // Ikisi DAVRANIS pinidir (belgedeki komut GERCEK bash ile kosar), ikisi KAYNAK-SOZLESMEDIR
    // (Dockerfile ve Program.cs icin davranis kaniti canli sunucuda: muhur 60).
    public class Mon1DagitimSozlesmeTests
    {
        private static readonly Lazy<string> Kok = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("Depo koku bulunamadi - sessiz skip YOK.");
        });

        private static string Oku(string yol) => File.ReadAllText(Path.Combine(Kok.Value, yol));

        private static string Yorumsuz(string metin, string onek) =>
            string.Join("\n", metin.Split('\n').Select(s =>
            {
                var i = s.IndexOf(onek, StringComparison.Ordinal);
                return i < 0 ? s : s.Substring(0, i);
            }));

        // ══ CANLI BULGU - LOG VOLUME'U BOS ═══════════════════════════════════════════════════
        // OLCULDU (uretim, 14 Eylul 2026): `divisima_logs_data` 7 gundur 0 dosya, dizin root:root.
        // Dockerfile C2'de uploads dizinini chown'dan ONCE yaratiyordu ama /app/logs'u HIC
        // yaratmiyordu; named volume root:root dogdu ve `USER divisima` Serilog File sink'i
        // SESSIZCE yazamadi. Ayni tuzagin (C2) ikinci ornegi.
        [Fact]
        public void DOCKERFILE_LOG_DIZININI_CHOWNDAN_ONCE_YARATIR()
        {
            var dockerfile = Yorumsuz(Oku("Dockerfile"), "#");
            var mkdir = Regex.Match(dockerfile, @"RUN mkdir -p [^\n]*/app/logs\b");
            mkdir.Success.Should().BeTrue("log volume'unun sahipligi imajdaki dizinden devralinir - dizin ACIKCA yaratilmali");
            var chown = dockerfile.IndexOf("chown -R divisima:divisima /app", StringComparison.Ordinal);
            chown.Should().BeGreaterThan(-1, "vakum kirici: non-root sahiplik adimi bulunmali");
            mkdir.Index.Should().BeLessThan(chown, "chown'dan SONRA yaratilan dizin root:root kalir");
            Oku("docker-compose.prod.yml").Should().Contain("logs_data:/app/logs", "dizin volume hedefiyle AYNI yol olmali");
        }

        // ══ KULLANICI KARARI - SERILOG SAKLAMA 14 GUN (dosya sayisi DEGIL) ═══════════════════
        // KAYNAK-SOZLESME: Serilog saklamasi yalniz dosya YUVARLANIRKEN uygulanir; 14 gunluk
        // silme davranisi test suresinde gozlenemez. Pin yapilandirmanin KENDISINI olcer.
        [Fact]
        public void SERILOG_DOSYA_SAKLAMASI_14_GUN_ZAMAN_SINIRIDIR_DOSYA_SAYISI_DEGIL()
        {
            var program = Yorumsuz(Oku("Divisima.API/Program.cs"), "//");
            var blok = Regex.Match(program, @"\.WriteTo\.File\((.*?)\)\);", RegexOptions.Singleline);
            blok.Success.Should().BeTrue("vakum kirici: File sink cagrisi bulunmali");
            var sink = blok.Groups[1].Value;

            sink.Should().Contain("rollingInterval: RollingInterval.Day");
            sink.Should().Contain("retainedFileTimeLimit: TimeSpan.FromDays(14)", "kullanici karari: 14 GUN");
            // TUR 3 / YB-1: sayi siniri KALDIRILINCA disk tavani da kalkti (denetci olctu: 40 x 100 MB
            // parca silinmedi). Karar: IKI sinir BIRLIKTE - once hangisi dolarsa. 40 x 100 MB ~ 4 GB.
            sink.Should().Contain("retainedFileCountLimit: 40",
                "zaman siniri tek basina disk tavani DEGILDIR; sayi siniri tavan olarak kalir");
            sink.Should().NotContain("retainedFileCountLimit: null", "sinirsiz sayi disk tavanini kaldirir");

            Oku("Divisima.API/appsettings.Production.example.json").Should().Contain("14 gun",
                "operator sablonu saklamayi DOGRU anlatmali");
            Oku("ops/deployment-checklist.md").Should().NotContain("30 dosya saklanır", "bayat saklama cumlesi kalmamali");
        }

        private static string Bash() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\Program Files\Git\bin\bash.exe" : "bash";

        private static string KomutuKos(string komut, string envIcerik)
        {
            var dizin = Path.Combine(Path.GetTempPath(), "mon1-grep-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dizin);
            try
            {
                File.WriteAllText(Path.Combine(dizin, ".env"), envIcerik, new UTF8Encoding(false));
                var psi = new ProcessStartInfo(Bash()) { RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = dizin };
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(komut);
                using var p = Process.Start(psi)!;
                var cikti = p.StandardOutput.ReadToEnd();
                p.WaitForExit(30_000);
                return cikti.Trim();
            }
            finally
            {
                try { Directory.Delete(dizin, true); } catch { /* best-effort */ }
            }
        }

        // ══ KURAL-UYUM G1 / RAPOR (d) - BELGEDEKI ALICI KONTROLU BOS DEGERDE KORDU ══════════
        // DAVRANIS: belgelerdeki GERCEK komut satiri ayiklanip bos ve dolu `.env` ile kosulur.
        // ONCEKI HAL: `ops/monitoring.md` §5 `grep -c '^DIVISIMA_ALARM_EMAIL='` bos degerde de 1.
        [Theory]
        [InlineData("ops/monitoring.md")]
        [InlineData("ops/deployment-checklist.md")]
        public void BELGEDEKI_ALICI_KONTROLU_BOS_DEGERI_YOK_SAYAR(string belge)
        {
            var satir = Regex.Match(Oku(belge), @"grep -cE? '\^DIVISIMA_ALARM_EMAIL=[^']*' \.env");
            satir.Success.Should().BeTrue($"vakum kirici: {belge} alici kontrol komutunu tasimali");

            KomutuKos(satir.Value, "DIVISIMA_ALARM_EMAIL=\n").Should().Be("0", $"{belge}: BOS alici 'var' sayilmamali");
            // TUR 3 / YB-3: `=.` deseni bu uc bicimde de 1 veriyordu (denetci olctu).
            KomutuKos(satir.Value, "DIVISIMA_ALARM_EMAIL=\"\"\n").Should().Be("0", $"{belge}: bos cift tirnak alici DEGIL");
            KomutuKos(satir.Value, "DIVISIMA_ALARM_EMAIL=''\n").Should().Be("0", $"{belge}: bos tek tirnak alici DEGIL");
            KomutuKos(satir.Value, "DIVISIMA_ALARM_EMAIL=   \n").Should().Be("0", $"{belge}: yalniz bosluk alici DEGIL");
            KomutuKos(satir.Value, "DIVISIMA_ALARM_EMAIL=a@ornek.test\n").Should().Be("1", $"{belge}: dolu alici bulunmali");
        }

        // ══ TUR 3 / YB-4 - LOG VOLUME SAHIPLIGI ICIN GERCEK KOMUT ═══════════════════════════
        // ONCEKI HAL: monitoring.md "chown dagitim kaydinda" diyordu, depoda komut YOKTU.
        // Dockerfile duzeltmesi YENI volume'u kurtarir; VAR OLAN (root:root dogmus) volume'u
        // kurtarmaz - o yuzden komut belgede YAZILI olmali. Uid/gid SABIT YAZILMAZ, konteynerden okunur
        // (canlida olculdu: 999:999, ama imaj degisirse degisebilir).
        [Theory]
        [InlineData("ops/monitoring.md")]
        [InlineData("ops/deployment-checklist.md")]
        public void LOG_VOLUME_SAHIPLIK_KOMUTU_BELGEDE_GERCEK_KOMUT_OLARAK_DURUR(string belge)
        {
            var metin = Oku(belge);
            metin.Should().Contain(
                "chown \"$(docker exec divisima-api-1 id -u):$(docker exec divisima-api-1 id -g)\" /var/lib/docker/volumes/divisima_logs_data/_data",
                $"{belge}: var olan volume'un sahipligi uid/gid KONTEYNERDEN okunarak duzeltilir");
            metin.Should().Contain("touch /app/logs/", $"{belge}: duzeltmenin ayirt edici kaniti (konteyner icinden yazma) yazili olmali");
            metin.Should().NotContain("dağıtım kaydında", $"{belge}: komutu baska bir yere havale eden bayat ifade kalmamali");
        }

        // ══ TUR 3 / YB-3 - BILINEN RISK: env_oku COMPOSE GRAMERININ TAMAMI DEGIL ═══════════
        // Kullanici karari: duzeltilmez, DURUST SINIR olarak adiyla yazilir. Pin, sinirin belgeden
        // sessizce silinmesini yakalar (KAYNAK-SOZLESME; davranis ayagi B4 pininde).
        [Fact]
        public void ENV_AYRISTIRICI_DURUST_SINIRI_OLCULMEYEN_DURUMLARI_ADIYLA_SAYAR()
        {
            var metin = Oku("ops/monitoring.md");
            // MUT-35 DERSI: ilk capa tum bolumu tariyordu; "sekme" OLCULEN listede de gectigi icin
            // olculmeyen listeden silinmesi 0 kirmizi verdi. Capa artik YALNIZ olculmeyen cumledir.
            var bolum = Regex.Match(metin, @"\*\*Ölçülmeyen ve ayrışabilecek kenar durumlar \(BİLİNEN RİSK\):\*\*(.*?)\n\n", RegexOptions.Singleline);
            bolum.Success.Should().BeTrue("vakum kirici: olculmeyen kenar durumlar cumlesi bulunmali");
            foreach (var durum in new[] { "`$$`", "sekme", "`export`", "`KEY = v`" })
                bolum.Groups[1].Value.Should().Contain(durum, $"olculmeyen kenar durum '{durum}' adiyla yazili olmali");
        }

        // ══ KURAL-UYUM G2 - SECURITY.md AYNI PARAGRAFTA CELISEN IKI CUMLE ═══════════════════
        [Fact]
        public void SECURITY_MD_ALERTING_PARAGRAFI_KENDISIYLE_CELISMEZ()
        {
            var paragraf = Oku("SECURITY.md").Split('\n')
                .SingleOrDefault(s => s.StartsWith("- **Anormallik/alerting:**", StringComparison.Ordinal));
            paragraf.Should().NotBeNull("vakum kirici: alerting paragrafi bulunmali");
            paragraf.Should().Contain("kritik-olay-alarm", "MON-1 mail okuyucusu anilmali");
            paragraf.Should().NotContain("Mail dalı YOKTUR", "mail okuyucusu VARKEN 'mail dali yok' demek celiski");
            paragraf.Should().NotContain("hiçbir alarm bir insana ULAŞMAZ", "MON-1 sonrasi bu genelleme YANLIS");
        }
    }
}
