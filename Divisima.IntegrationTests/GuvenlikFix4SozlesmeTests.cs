using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GF-4 - TEDARIK ZINCIRI SOZLESME PINLERI ════════════════════════════════════════
    //
    // Bu dosya GUVENLIK-FIX-4 (Eylul 2026) dalgasinin KAYNAK SOZLESMESI pinlerini tasir.
    //
    // NEDEN KAYNAK PINI: dalganin konusu paket/imaj/CI yapilandirmasidir. "Ayni imaj DORT
    // yerde birebir ayni" turu bir kural CALISMA ZAMANINDA gozlenemez - dordu ayri
    // ortamlarda (GitHub service container, docker compose, Testcontainers) kosar ve
    // hicbiri digerini gormez. Tek durust mekanizma KAYNAK metnini karsilastirmaktir.
    //
    // CAPA KIRLENMESI - YAPISAL COZUM: sayimlar YORUMSUZ metin uzerinde yapilir. Bu
    // dosyanin ilk taslaginda NuGet.config icin satir-oneki soyucusu kullanildi ve
    // "<clear />" asserti dosyanin KENDI YORUMUNDAN bedava saglaniyordu (yorum cok
    // satirli, yalniz ILK satiri "<!--" ile basliyor). XML icin blok soyucu, YAML/
    // Dockerfile ve C# icin satir soyucusu kullanilir. Ayni tuzagin bedeli bu depoda
    // daha once odendi (CLAUDE.md B6 / A-CAPA ailesi).
    public class GuvenlikFix4SozlesmeTests
    {
        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: docker-compose.yml iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pinler kaynagi okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen kaynak dosya bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        private static int Sayim(string metin, string parca) => metin.Split(parca).Length - 1;

        // Satir yorumlarini duser (YAML/Dockerfile "#", C# "//").
        private static string YorumsuzSatir(string metin, string onek)
            => string.Join("\n", metin.Replace("\r\n", "\n").Split('\n')
                .Where(s => !s.TrimStart().StartsWith(onek, StringComparison.Ordinal)));

        // XML yorum BLOKLARINI duser - cok satirli yorumun govdesi de gider.
        private static string YorumsuzXml(string metin)
            => Regex.Replace(metin, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        // Dosya uzantisina gore dogru soyucu.
        private static string Govde(string goreliYol)
        {
            var ham = Oku(goreliYol);
            if (goreliYol.EndsWith(".cs", StringComparison.Ordinal)) return YorumsuzSatir(ham, "//");
            if (goreliYol.EndsWith(".config", StringComparison.Ordinal) ||
                goreliYol.EndsWith(".props", StringComparison.Ordinal)) return YorumsuzXml(ham);
            return YorumsuzSatir(ham, "#");
        }

        // ── Y6: SQL SERVER IMAJI - TEK REFERANS, DORT YERDE BIREBIR ────────────────────
        //
        // "2022-latest" YURUYEN bir etikettir: digest olmadan ayni satir zaman icinde
        // BASKA bir imaji cozer. Dordunun ayni tag+digest'i tasimasi, testin kostugu
        // veritabani ile CI'nin ve yerel gelistirmenin AYNI ikiliyi kullandigini garanti
        // eder. Digest degisecekse DORDU BIRDEN degisir - bu pin onu zorlar.
        private const string ImajTam =
            "mcr.microsoft.com/mssql/server:2022-latest@sha256:0730f3689a6dcc33beaf8f466376ac056d7483a2272dcbd3bcc36d3a6df05437";

        private const string ImajDigestsiz = "mcr.microsoft.com/mssql/server:2022-latest";

        private static readonly string[] ImajSiteleri =
        {
            ".github/workflows/ci.yml",
            ".github/workflows/security.yml",
            "docker-compose.yml",
            "Divisima.IntegrationTests/CustomWebApplicationFactory.cs",
        };

        public static TheoryData<string> ImajSitesi
        {
            get
            {
                var d = new TheoryData<string>();
                foreach (var y in ImajSiteleri) d.Add(y);
                return d;
            }
        }

        [Theory]
        [MemberData(nameof(ImajSitesi))]
        public void Y6_SqlServer_Imaji_DORT_SITEDE_AYNI_TAG_VE_DIGEST(string yol)
        {
            var govde = Govde(yol);

            // (a) Tam referans (tag + digest) BULUNMALI.
            Sayim(govde, ImajTam).Should().BeGreaterThan(0,
                $"{yol} icinde imaj tag+digest ile pinli olmali");

            // (b) DIGEST'SIZ hicbir kopya KALMAMALI. Digest'siz dizge tam referansin
            //     ONEKI oldugu icin duz "Contains" bir SUPERSTRING tuzagidir; dogru
            //     olcut, digest'siz gecis sayisinin tam gecis sayisina ESIT olmasidir -
            //     yani her etiket gecisini bir digest izliyor.
            Sayim(govde, ImajDigestsiz).Should().Be(Sayim(govde, ImajTam),
                $"{yol} icinde digest'siz (yuruyen etiketli) imaj referansi KALMAMALI");
        }

        [Fact]
        public void Y6_Imaj_Referansi_DORT_SITEDE_DE_BIREBIR_AYNI_DIZGE()
        {
            // Theory her siteyi TEK TEK dogrular; bu test dordunun AYNI ANDA ve AYNI
            // dizgeyle pinli oldugunu tek assert'te gosterir (drift kaniti).
            var bulunan = ImajSiteleri.Select(y => Sayim(Govde(y), ImajTam)).ToArray();

            bulunan.Should().HaveCount(4, "Y6 dort siteyi kapsar");
            bulunan.Should().OnlyContain(n => n > 0,
                "dort sitenin HEPSI ayni tag+digest'i tasimali - biri kayarsa bu pin kirilir");
        }

        // ── Y6: DOCKERFILE TABAN IMAJLARI DIGEST'E PINLI ──────────────────────────────
        [Theory]
        [InlineData("mcr.microsoft.com/dotnet/sdk:8.0",
                    "sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8")]
        [InlineData("mcr.microsoft.com/dotnet/aspnet:8.0",
                    "sha256:787c228ea85457bec43c8b084e6ac360b26ea43b5c2fcbe861f721f2e8670dd3")]
        public void Y6_Dockerfile_Taban_Imajlari_DIGESTE_PINLI(string imaj, string digest)
        {
            var govde = Govde("Dockerfile");
            var tam = imaj + "@" + digest;

            Sayim(govde, tam).Should().BeGreaterThan(0,
                $"Dockerfile'da {imaj} digest ile pinli olmali");

            Sayim(govde, imaj).Should().Be(Sayim(govde, tam),
                $"Dockerfile'da {imaj} icin digest'siz FROM satiri KALMAMALI");
        }

        // ── Y5: PAKET KAYNAGI TEK VE KILITLI GRAF ─────────────────────────────────────
        [Fact]
        public void Y5_NuGet_Config_TEK_KAYNAK_ve_Devralinanlari_TEMIZLIYOR()
        {
            var govde = Govde("NuGet.config");

            // <clear /> olmadan MAKINE duzeyi kaynaklar devralinir; zeminde birebir bu
            // vardi (ikinci kaynak yerel bir klasordu).
            govde.Should().Contain("<clear />", "devralinan paket kaynaklari temizlenmeli");
            govde.Should().Contain("https://api.nuget.org/v3/index.json", "tek kaynak nuget.org olmali");
            Sayim(govde, "<add key=").Should().Be(1, "TEK paket kaynagi tanimlanmali");
        }

        [Fact]
        public void Y5_Lock_Dosyasi_ALTI_PROJEDE_DE_VAR_ve_CI_KILITLI_MODDA_RESTORE_EDIYOR()
        {
            string[] projeler =
            {
                "Divisima.Core", "Divisima.Entity", "Divisima.Dal",
                "Divisima.Bussiness", "Divisima.API", "Divisima.IntegrationTests",
            };

            foreach (var p in projeler)
                File.Exists(Path.Combine(KokDizin.Value, p, "packages.lock.json"))
                    .Should().BeTrue($"{p} icin packages.lock.json depoda olmali");

            Govde("Directory.Build.props")
                .Should().Contain("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>",
                    "lock dosyasi uretimi tum projeler icin acik olmali");

            // Kilitli graf dogrulamasi OLMADAN lock dosyasi yalniz bir kayittir; kapiyi
            // kuran sey locked-mode bayragidir ve IKI workflow'da da bulunmalidir.
            foreach (var wf in new[] { ".github/workflows/ci.yml", ".github/workflows/security.yml" })
                Govde(wf).Should().Contain("dotnet restore Divisima-Backend.sln --locked-mode",
                    $"{wf} restore adimi kilitli grafi dogrulamali");
        }

        [Fact]
        public void K7_NuGet_Denetimi_GECISLI_PAKETLERI_DE_KAPSIYOR()
        {
            // Varsayilan "direct" YALNIZ dogrudan basvurulari tarar. Deger ACIKCA
            // yazilmalidir: varsayilan SDK surumune baglidir ve yerel (SDK 9) ile CI
            // (SDK 8) arasinda sessizce ayrisabilir.
            Govde("Directory.Build.props")
                .Should().Contain("<NuGetAuditMode>all</NuGetAuditMode>",
                    "NuGet denetimi gecisli paketleri de kapsamali");
        }

        // ── Y8: SECURITY.md'DEKI ESLEME SAYISI KAYNAKTAN OLCULUR ──────────────────────
        //
        // SECURITY.md'nin AutoMapper kabul-edilen-risk gerekcesi "istemci girdisinden
        // entity'ye esleme yalniz N noktada" diyor. Bu sayi belgeye ELLE yazildigi surece
        // BAYATLAR - nitekim bayatlamisti (belge 7 diyordu, gercek 10; Address ve
        // Category'nin GUNCELLEME yollari sayilmamisti).
        //
        // ESLESME BICIMI KUSURU (CLAUDE.md B6 / A grubu): esleme IKI BICIMDE yaziliyor -
        // jenerik `Map<Entity>(dto)` (ekleme) ve jenerik OLMAYAN `Map(dto, entity)`
        // (guncelleme). Tek capa kullanan her sayim guncelleme yollarini KACIRIR; belgedeki
        // 7 tam olarak boyle olusmustu. Pin BU YUZDEN iki bicimi de sayar.
        private const int EslemeNoktasiSayisi = 10;

        private static string[] BussinessKaynaklari()
            => Directory.GetFiles(Path.Combine(KokDizin.Value, "Divisima.Bussiness"), "*.cs",
                                  SearchOption.AllDirectories);

        [Fact]
        public void Y8_Istemci_Girdisinden_Entitye_ESLEME_NOKTASI_SAYISI_KAYNAKTAN()
        {
            var ekleme = 0;
            var guncelleme = 0;

            foreach (var dosya in BussinessKaynaklari())
            {
                // Yorumlar SOYULUR: `.Map(dto, product)` dizgesi bir validator'in YORUM
                // satirinda da geciyor; sayilsaydi sonuc 11 cikardi.
                var govde = YorumsuzSatir(File.ReadAllText(dosya), "//");

                // Jenerik bicim: hedef tipi Dto/Response OLMAYANLAR entity hedeflidir.
                // (Olculdu: 25 gecisin 20'si *ResponseDto / List<*ResponseDto> - cikis yonu.)
                ekleme += Regex.Matches(govde, @"_mapper\.Map<([^>]+)>")
                    .Cast<Match>()
                    .Count(m => !m.Groups[1].Value.Contains("Dto", StringComparison.Ordinal)
                             && !m.Groups[1].Value.StartsWith("List<", StringComparison.Ordinal));

                // Jenerik OLMAYAN bicim: var olan entity'nin USTUNE yazan guncelleme yolu.
                guncelleme += Regex.Matches(govde, @"_mapper\.Map\([^<]").Count;
            }

            (ekleme + guncelleme).Should().Be(EslemeNoktasiSayisi,
                "esleme yuzeyi degistiyse SECURITY.md'deki kabul-edilen-risk gerekcesi de " +
                "guncellenmelidir (bolum 11, AutoMapper CVE-2026-32933)");

            // Belgedeki sayi ile kaynaktaki sayi AYNI olmali.
            Oku("SECURITY.md").Should().Contain($"yalnız {EslemeNoktasiSayisi} noktada",
                "SECURITY.md'deki sayi kaynaktan olculen sayiyla AYNI olmali");
        }

        [Fact]
        public void Y8_ProjectTo_KULLANILMIYOR_iddiasi_HALA_DOGRU()
        {
            // SECURITY.md'nin ILK kaniti bu; ProjectTo eklenirse maruziyet analizi
            // COKER (IQueryable uzerinde derin graf kurulabilir).
            var projeler = new[] { "Divisima.Bussiness", "Divisima.Dal", "Divisima.API" };
            var toplam = projeler
                .SelectMany(p => Directory.GetFiles(Path.Combine(KokDizin.Value, p), "*.cs",
                                                    SearchOption.AllDirectories))
                .Sum(f => Sayim(YorumsuzSatir(File.ReadAllText(f), "//"), "ProjectTo"));

            toplam.Should().Be(0, "SECURITY.md 'ProjectTo kullanilmiyor - sifir eslesme' diyor");
        }
    }
}
