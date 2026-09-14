using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ MON-1 / D2-D4 - SUNUCU BETIKLERI: DAVRANIS PINLERI ═══════════════════════════════════
    //
    // Betikler GERCEK bash ile kosar; dis dunya (curl, docker, df, mail) PATH onune konan
    // KAYITCI stub'larla degistirilir. Olculen sey betigin KARARIDIR: hangi turda mail, kac
    // restart, hangi konu, hangi sayim. Canli davranis kaniti (gercek API durdurma, gercek SMTP)
    // muhur 60'ta; burada o kararlarin mantigi pinlenir.
    //
    // ORTAM: CI ubuntu (bash yerlesik). Yerelde Windows'ta Git Bash kullanilir; bulunamazsa
    // test PATLAR (sessiz skip YOK).
    public class Mon1SunucuBetikleriTests : IDisposable
    {
        private static readonly Lazy<string> Kok = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("Depo koku bulunamadi - sessiz skip YOK.");
        });

        private readonly string _tmp;

        public Mon1SunucuBetikleriTests()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "mon1-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_tmp, "bin"));
            Directory.CreateDirectory(Path.Combine(_tmp, "state"));
        }

        public void Dispose()
        {
            try { Directory.Delete(_tmp, true); } catch { /* best-effort */ }
        }

        private static string Bash()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "bash";
            var git = @"C:\Program Files\Git\bin\bash.exe";
            File.Exists(git).Should().BeTrue("Windows'ta bu pinler Git Bash ister - sessiz skip YOK");
            return git;
        }

        // Git Bash PATH'i ':' ile boler; "C:\..." bicimi kirilir.
        private static string BashYolu(string yol) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "/" + char.ToLowerInvariant(yol[0]) + yol.Substring(2).Replace('\\', '/')
                : yol;

        private string T(string ad) => Path.Combine(_tmp, ad);

        private void Yaz(string ad, string icerik) => File.WriteAllText(T(ad), icerik.Replace("\r\n", "\n"), new UTF8Encoding(false));

        private void Stub(string ad, string govde) => Yaz(Path.Combine("bin", ad), "#!/usr/bin/env bash\n" + govde + "\n");

        private string Oku(string ad) => File.Exists(T(ad)) ? File.ReadAllText(T(ad)) : "";

        private (int Kod, string Cikti) Kos(string betik, Dictionary<string, string> ortam, params string[] argumanlar)
        {
            var psi = new ProcessStartInfo(Bash())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("chmod +x \"$STUB_BIN\"/* 2>/dev/null; PATH=\"$STUB_BIN:$PATH\" exec bash \"$0\" \"$@\"");
            psi.ArgumentList.Add(BashYolu(Path.Combine(Kok.Value, "ops", "monitoring", betik)));
            foreach (var a in argumanlar) psi.ArgumentList.Add(a);
            psi.Environment["STUB"] = BashYolu(_tmp);
            psi.Environment["STUB_BIN"] = BashYolu(T("bin"));
            foreach (var (k, v) in ortam) psi.Environment[k] = v;

            using var p = Process.Start(psi)!;
            p.StandardInput.Write("stdin-govde\n");
            p.StandardInput.Close();
            var cikti = p.StandardOutput.ReadToEndAsync();
            var hata = p.StandardError.ReadToEndAsync();
            p.WaitForExit(60_000).Should().BeTrue($"{betik} 60 sn icinde bitmeli");
            return (p.ExitCode, cikti.Result + hata.Result);
        }

        private Dictionary<string, string> WatchdogOrtami()
        {
            Stub("curl", "cat \"$STUB/kod\" 2>/dev/null || printf '000'");
            Stub("docker", "echo \"$*\" >> \"$STUB/docker.calls\"");
            Yaz("mail", "echo \"KONU=$1\" >> \"$STUB/mail.calls\"; cat >> \"$STUB/mail.calls\"");
            return new()
            {
                ["STATE_DIR"] = BashYolu(T("state")),
                ["LOG_FILE"] = BashYolu(T("wd.log")),
                ["MAIL_CMD"] = BashYolu(T("mail")),
                ["DIVISIMA_DIR"] = "/opt/divisima-test",
                ["WATCHDOG_UYARI_ARALIGI"] = "2",
            };
        }

        private int Say(string metin, string alt) =>
            metin.Split('\n').Count(s => s.Contains(alt, StringComparison.Ordinal));

        [Fact]
        public void WATCHDOG_UCUNCU_ARDISIK_HATADA_TEK_MAIL_TEK_RESTART_SONRA_YALNIZ_UYARI_IYILESINCE_NORMALE_DONDU()
        {
            var ortam = WatchdogOrtami();

            Kos("watchdog.sh", ortam);
            Kos("watchdog.sh", ortam);
            Say(Oku("mail.calls"), "KONU=").Should().Be(0, "1-2. hata esigin altinda - alarm YOK");
            Oku("docker.calls").Should().BeEmpty("esik altinda restart YOK");

            Kos("watchdog.sh", ortam);
            Say(Oku("mail.calls"), "KONU=Divisima ALARM - API sağlık kontrolü başarısız").Should().Be(1, "3. ardisik hata TEK alarm");
            Say(Oku("docker.calls"), "restart api").Should().Be(1, "3. ardisik hata TEK restart denemesi");
            Oku("docker.calls").Should().Contain("-f /opt/divisima-test/docker-compose.prod.yml", "restart uretim compose'u uzerinden");
            // MUT-17 DERSI: log ICERIGI olculmuyordu; LOG_FILE'i yok sayan betik yesil kaliyordu.
            Oku("wd.log").Should().Contain("HATA kod=000 ardisik=3", "tur izi LOG_FILE'a (logrotate kapsamindaki dosya) yazilmali");
            Oku("wd.log").Should().Contain("YENIDEN_BASLATMA");

            Kos("watchdog.sh", ortam);
            Kos("watchdog.sh", ortam);
            Say(Oku("docker.calls"), "restart api").Should().Be(1, "ayni kesintide IKINCI restart YOK");
            Say(Oku("mail.calls"), "KONU=Divisima ALARM - API hâlâ erişilemiyor").Should().Be(1,
                "restart'tan sonra yalniz uyari (aralik 2 tur verildi: 5. turda)");

            Yaz("kod", "200");
            Kos("watchdog.sh", ortam);
            Say(Oku("mail.calls"), "KONU=Divisima ALARM ÇÖZÜLDÜ").Should().Be(1, "iyilesme bildirilmeli");
            Oku("state/watchdog.state").Should().Contain("ARDISIK=0");

            Kos("watchdog.sh", ortam);
            Say(Oku("mail.calls"), "KONU=").Should().Be(3, "saglikli turlar mail uretmez");
        }

        [Fact]
        public void WATCHDOG_ARALIKLI_HATA_ALARM_URETMEZ_200_SAYACI_SIFIRLAR()
        {
            var ortam = WatchdogOrtami();
            Kos("watchdog.sh", ortam);
            Kos("watchdog.sh", ortam);
            Oku("state/watchdog.state").Should().Contain("ARDISIK=2", "vakum kirici: hata sayaci gercekten artmali");
            Yaz("kod", "200");
            Kos("watchdog.sh", ortam);
            File.Delete(T("kod"));
            Kos("watchdog.sh", ortam);
            Kos("watchdog.sh", ortam);

            Oku("mail.calls").Should().BeEmpty("araya giren 200 sayaci sifirlar; 2+2 hata alarm DEGIL");
            Oku("docker.calls").Should().BeEmpty();
        }

        private Dictionary<string, string> MailOrtami(string envIcerik)
        {
            Stub("curl",
                "printf '%s\\n' \"$@\" > \"$STUB/curl.args\"\n" +
                "while [ $# -gt 0 ]; do if [ \"$1\" = \"--config\" ]; then cat \"$2\" > \"$STUB/curl.config\"; fi; shift; done\n" +
                "cat > \"$STUB/curl.stdin\"");
            // HAM yazilir (CR korunur): .env Windows'ta duzenlenmis olabilir.
            File.WriteAllText(T("env"), envIcerik, new UTF8Encoding(false));
            return new() { ["ENV_FILE"] = BashYolu(T("env")) };
        }

        [Fact]
        public void ALARM_MAILI_PAROLAYI_KOMUT_SATIRINA_KOYMAZ_ve_ENV_DOSYASINI_CALISTIRMAZ()
        {
            // MUT-13 DERSI: komut satiri ILK SIRADA. Ilk yazimda parola satirindan (tek `"`)
            // SONRAYDI; `source` o tirnakta sozdizimi hatasiyla dusup komuta HIC ulasmadi ve
            // pin `source` mutasyonuna KOR kaldi (0 kirmizi).
            var ortam = MailOrtami(
                $"KOTU=$(touch {BashYolu(T("calisti"))})\n" +
                "DIVISIMA_SMTP_HOST=smtp.ornek.test\n" +
                "DIVISIMA_SMTP_USER=\"u@ornek.test\"\n" +
                "DIVISIMA_SMTP_PASSWORD=p\"q\\r\r\n" +
                "DIVISIMA_SMTP_FROM=Divisima <no-reply@divisima.net>\n" +
                "DIVISIMA_ALARM_EMAIL=alici@ornek.test\n");

            var (kod, cikti) = Kos("alarm-mail.sh", ortam, "Divisima ALARM - deneme");
            kod.Should().Be(0, cikti);

            var argumanlar = Oku("curl.args");
            argumanlar.Should().Contain("smtp://smtp.ornek.test:587");
            argumanlar.Should().Contain("--ssl-reqd", "SMTP parolasi sifrelenmemis baglantidan gitmez");
            argumanlar.Should().NotContain("p\"q", "parola komut satirinda (ps ile gorunur) OLMAMALI");
            argumanlar.Split('\n').Should().Contain("no-reply@divisima.net", "zarf adresi 'Ad <adres>' bicimden ayiklanmali");
            Oku("curl.config").Should().Contain("user = \"u@ornek.test:p\\\"q\\\\r\"",
                "kimlik curl'e yapilandirma kanalindan, kacirilmis olarak gitmeli (CR soyulmus)");

            var ileti = Oku("curl.stdin");
            ileti.Should().Contain("To: alici@ornek.test");
            ileti.Should().Contain("Subject: =?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Divisima ALARM - deneme")) + "?=");
            ileti.Should().Contain("stdin-govde");
            File.Exists(T("calisti")).Should().BeFalse(".env `source` EDILMEMELI - icindeki komut calismamali");
        }

        [Fact]
        public void ALARM_MAILI_ALICI_YOKSA_GONDERMEZ_ve_BASARISIZ_DONER()
        {
            var ortam = MailOrtami("DIVISIMA_SMTP_HOST=smtp.ornek.test\nDIVISIMA_SMTP_PASSWORD=x\n");
            var (kod, cikti) = Kos("alarm-mail.sh", ortam, "Divisima ALARM - deneme");
            kod.Should().Be(3, "eksik alici SESSIZ basari olmamali: " + cikti);
            File.Exists(T("curl.args")).Should().BeFalse("alici yokken SMTP'ye gidilmez");
        }

        private Dictionary<string, string> OzetOrtami(string dockerCiktisi)
        {
            Stub("df", "printf 'Filesystem 1024-blocks Used Available Capacity Mounted on\\n/dev/sda1 100 42 58 42%% /\\n'");
            Stub("docker", "cat \"$STUB/docker.out\"");
            Yaz("docker.out", dockerCiktisi);
            Yaz("mail", "echo \"KONU=$1\" >> \"$STUB/mail.calls\"; cat >> \"$STUB/mail.calls\"");
            Directory.CreateDirectory(T("db"));
            Directory.CreateDirectory(T("log"));
            Directory.CreateDirectory(T("yedek"));
            File.WriteAllBytes(T("db/DivisimaDb.mdf"), new byte[3 * 1024 * 1024]);
            File.WriteAllBytes(T("db/DivisimaDb_log.ldf"), new byte[5 * 1024 * 1024]);  // log dosyasi SAYILMAZ
            File.WriteAllBytes(T("yedek/divisima-bugun.bak.age"), new byte[1]);

            var simdi = DateTime.UtcNow;
            string Z(DateTime t) => t.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " +00:00";
            Yaz("log/divisima-20260914.log",
                $"{Z(simdi.AddHours(-1))} [ERR] bir\n   at Yigin.Satiri [ERR]\n" +
                $"{Z(simdi.AddHours(-2))} [FTL] iki\n" +
                $"{Z(simdi.AddHours(-1))} [INF] bilgi\n" +
                $"{Z(simdi.AddHours(-30))} [ERR] eski\n");

            return new()
            {
                ["MAIL_CMD"] = BashYolu(T("mail")),
                ["DB_VERI_DIZIN"] = BashYolu(T("db")),
                ["LOG_DIZIN"] = BashYolu(T("log")),
                ["YEDEK_DIZIN"] = BashYolu(T("yedek")),
            };
        }

        [Fact]
        public void GUNLUK_OZET_NORMALDE_NORMAL_KONU_ESIK_ASILINCA_ALARM_ve_24_SAAT_HATA_SAYIMI()
        {
            var ortam = OzetOrtami("divisima-api-1 running Up 2 hours (healthy)\ndivisima-redis-1 running Up 2 hours (healthy)\n");

            Kos("daily-report.sh", ortam);
            var normal = Oku("mail.calls");
            normal.Should().Contain("KONU=Divisima günlük özet - normal");
            normal.Should().Contain("[OK]    DB veri dosyasi: 3 MB", "yalniz .mdf/.ndf sayilir, 5 MB'lik log dosyasi DEGIL");
            normal.Should().Contain("ERROR 1 · FATAL 1",
                "24 saat icindeki ERR ve FTL sayilir; 30 saatlik ERR, INF ve yigin satiri SAYILMAZ");

            File.Delete(T("mail.calls"));
            ortam["DISK_ESIK"] = "40";
            ortam["DB_ESIK_MB"] = "3";
            ortam["YEDEK_DIZIN"] = BashYolu(T("bos-yedek"));
            Yaz("docker.out", "divisima-api-1 running Up 2 hours (unhealthy)\ndivisima-mssql-1 exited Exited (1) 3 hours ago\n");
            Kos("daily-report.sh", ortam);

            var alarm = Oku("mail.calls");
            alarm.Should().Contain("KONU=Divisima ALARM - günlük özet: 4 sorun");
            alarm.Should().Contain("[SORUN] Disk /: %42 (esik %40)");
            alarm.Should().Contain("[SORUN] DB veri dosyasi: 3 MB");
            alarm.Should().Contain("divisima-api-1,divisima-mssql-1", "unhealthy VE calismayan konteyner ayri ayri yakalanmali");
            alarm.Should().Contain("[SORUN] Yedek:");
        }

        [Fact]
        public void GUNLUK_OZET_OLCULEMEYEN_KALEMI_NORMAL_SAYMAZ()
        {
            var ortam = OzetOrtami("divisima-api-1 running Up 2 hours (healthy)\n");
            ortam["DB_VERI_DIZIN"] = BashYolu(T("olmayan-db"));
            ortam["LOG_DIZIN"] = BashYolu(T("olmayan-log"));

            Kos("daily-report.sh", ortam);
            var mail = Oku("mail.calls");
            mail.Should().Contain("KONU=Divisima ALARM - günlük özet: 2 sorun");
            mail.Should().Contain("[SORUN] DB veri dosyasi: OLCULEMEDI");
            mail.Should().Contain("[SORUN] API log: dizin OKUNAMADI");
        }

        // KAYNAK-SOZLESME: tek-kaynak iddialari. Davranis ayagi yukaridaki pinlerde + canli muhurde.
        [Fact]
        public void SMTP_VARSAYILANLARI_ALICI_ANAHTARI_CRON_ve_LOGROTATE_TEK_KAYNAKLA_UYUMLU()
        {
            string Oku2(string yol) => File.ReadAllText(Path.Combine(Kok.Value, yol));
            var compose = Oku2("docker-compose.prod.yml");
            var mail = Oku2("ops/monitoring/alarm-mail.sh");

            compose.Should().Contain("MailSettings__User: ${DIVISIMA_SMTP_USER:-no-reply@divisima.net}");
            mail.Should().Contain("kullanici=\"no-reply@divisima.net\"", "betik varsayilani compose varsayilaniyla AYNI olmali");
            compose.Should().Contain("MailSettings__From: ${DIVISIMA_SMTP_FROM:-Divisima <no-reply@divisima.net>}");
            mail.Should().Contain("gonderen=\"Divisima <no-reply@divisima.net>\"");
            compose.Should().Contain("Monitoring__AlarmEmail: ${DIVISIMA_ALARM_EMAIL:-}", "uygulama alicisi .env anahtarindan");
            mail.Should().Contain("env_oku DIVISIMA_ALARM_EMAIL", "betik alicisi AYNI .env anahtarindan");

            var cron = Oku2("ops/monitoring/divisima-monitoring.cron");
            cron.Should().Contain("*/5 * * * * root /bin/bash /opt/divisima/ops/monitoring/watchdog.sh");
            cron.Should().Contain("0 7 * * * root /bin/bash /opt/divisima/ops/monitoring/daily-report.sh");
            cron.Should().Contain("CRON_TZ=UTC");

            var logrotate = Oku2("ops/monitoring/logrotate-divisima-monitoring");
            Oku2("ops/monitoring/watchdog.sh").Should().Contain("/var/log/divisima-watchdog.log");
            logrotate.Should().Contain("/var/log/divisima-watchdog.log", "watchdog logu rotasyona girmeli");
            logrotate.Should().Contain("/var/log/divisima-daily-report.log", "cron'un yonlendirdigi ozet logu rotasyona girmeli");

            // CR tasiyan betik sunucuda "$'\r': command not found" ile duser (S5 dedektoru).
            foreach (var betik in new[] { "alarm-mail.sh", "watchdog.sh", "daily-report.sh", "divisima-monitoring.cron", "logrotate-divisima-monitoring" })
                Oku2("ops/monitoring/" + betik).Should().NotContain("\r", $"{betik} saf LF olmali");
        }
    }
}
