using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ DALGA-4-FIX-2 / M1 - API ORIGIN TEK KAYNAK SOZLESMESI ══════════════════════════════
    //
    // OLCULEN ZARAR: storefront'un API tabani ve CSP origin'leri KAYNAKTA SABIT gomuluydu -
    // "http://localhost:5000" BES ayri yerde (index.html CSP + index.html JS literali +
    // api-bridge.js yedegi + admin.html CSP + admin.html JS literali). Depo neyse o yayina
    // gidiyordu: LAN adresinden acilinca istekler kullanicinin KENDI makinesine gidiyor,
    // tarayici engelliyor (ERR_BLOCKED_BY_CLIENT) ve KATALOG BOS geliyordu. Ustelik API
    // tabani ile CSP origin'leri ELLE senkron tutuluyordu.
    //
    // NEDEN DAGITIM ANI, NEDEN CALISMA ANI DEGIL - TARAYICIDA OLCULDU:
    //   CSP <meta> belge AYRISTIRILIRKEN uygulanir; calisma aninda DAHA GENIS bir CSP meta'si
    //   eklemek politikayi GEVSETMEZ. Denendi: enjekte edilen genis meta'ya ragmen istek
    //   "securitypolicyviolation: connect-src -> http://192.168.x.x:5000/health" ile
    //   ENGELLENDI. Yani API tabani runtime'da ayarlanabilirdi ama UC CSP DIREKTIFI
    //   ayarlanamazdi - sart ise "hepsi TEK KAYNAKTAN turesin" idi.
    //
    // SECILEN TASARIM: origin dosyaya DAGITIM aninda yazilir (ops/set-api-origin.sh, TEK
    // girdi -> hem meta hem UC CSP direktifi), CALISMA aninda ise yalnizca DOGRULANIR
    // (index.html/admin.html icindeki tutarlilik guard'i, uyusmazlikta gorunur uyari).
    // Bugunku kusur mekanizma degildi, DOGRULANMAMIS elle senkrondu - guard tam o bosluga bakar.
    //
    // BU PINLERIN SINIRI: 2. ve 4. pin GERCEK BIR HESAP yapar (CSP ayristirmasi ve tam
    // degistirme simulasyonu); digerleri kaynak sozlesmesini tutar. Tarayici davranisi
    // (guard'in gercekten bagirdigi, isteklerin gercekten yeni origin'e gittigi) bu suitte
    // dogrulanamaz - depoda JS/DOM kosucusu YOK; olcum tarayicida yapildi ve rapora yazildi.
    public class ApiOriginTekKaynakTests
    {
        private const string TestOrigin = "https://api.ornek-dagitim.test";

        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "frontend", "index.html")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: frontend/index.html iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pinler kaynagi okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen kaynak dosya bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        private static string BeyanEdilenOrigin(string html)
        {
            var m = Regex.Match(html, "name=\"divisima-api-origin\"[^>]*content=\"([^\"]*)\"");
            m.Success.Should().BeTrue("TEK KAYNAK meta etiketi bulunmali: meta[name=\"divisima-api-origin\"]");
            return m.Groups[1].Value.Trim();
        }

        private static string CspIcerigi(string html)
        {
            var m = Regex.Match(html, "<meta http-equiv=\"Content-Security-Policy\" content=\"([^\"]*)\"");
            m.Success.Should().BeTrue("CSP meta etiketi bulunmali");
            return m.Groups[1].Value;
        }

        // CSP ';' ile ayrilmis direktiflerden olusur. Uretimdeki guard ile AYNI yontem
        // (duz split + trim) - kacis semantigi olmadigi icin sessizce bozulamaz.
        private static string? DirektifDegeri(string csp, string direktif)
        {
            foreach (var parca in csp.Split(';'))
            {
                var p = parca.Trim();
                if (p == direktif) return "";
                if (p.StartsWith(direktif + " ", StringComparison.Ordinal))
                    return p.Substring(direktif.Length + 1);
            }
            return null;
        }

        // ── 1) TEK KAYNAK: ikinci literal YOK ─────────────────────────────────────────────
        [Fact]
        public void API_TABANI_TEK_KAYNAKTAN_Turer_IKINCI_LITERAL_YOK()
        {
            var index = Oku("frontend/index.html");
            var bridge = Oku("frontend/api-bridge.js");

            // VAKUM KIRICI: once TEK KAYNAGIN gercekten var ve dolu oldugu dogrulanir.
            var origin = BeyanEdilenOrigin(index);
            origin.Should().StartWith("http", "beyan edilen origin bir mutlak origin olmali");

            index.Should().NotContain("window.DIVISIMA_API_BASE=\"http",
                "OLCULEN ZARAR: taban dogrudan literal olarak atanirsa dagitimda unutulur ve " +
                "istekler son kullanicinin KENDI makinesine gider (katalog BOS gelir)");

            bridge.Should().NotContain("localhost:5000",
                "api-bridge.js'teki sessiz yedek, yanlis yapilandirilmis bir dagitimda " +
                "hatayi GIZLERDI - bos taban gorunur sekilde bozuktur, sessiz yanlis taban degildir");

            index.Should().Contain("window.DIVISIMA_API_BASE=origin",
                "taban TEK KAYNAKTAN (meta) turemeli");
        }

        // ── 2) ELLE SENKRONUN CI'DAKI KARSILIGI - GERCEK HESAP ────────────────────────────
        [Fact]
        public void CSP_UC_DIREKTIF_BEYAN_EDILEN_ORIGINI_Tasir()
        {
            var index = Oku("frontend/index.html");
            var origin = BeyanEdilenOrigin(index);
            var csp = CspIcerigi(index);

            foreach (var d in new[] { "img-src", "connect-src", "form-action" })
            {
                var deger = DirektifDegeri(csp, d);
                deger.Should().NotBeNull($"CSP'de '{d}' direktifi bulunmali");
                deger!.Should().Contain(origin,
                    $"beyan edilen API origin'i '{d}' direktifinde de bulunmali - bu ikisi " +
                    "ELLE senkron tutuldugu surece biri guncellenip digeri unutulabilirdi; " +
                    "ops/set-api-origin.sh ikisini TEK girdiden yazar");
            }
        }

        // ── 3) ADMIN PANELI de ayni sozlesmeye tabi ───────────────────────────────────────
        [Fact]
        public void ADMIN_PANELI_de_AYNI_TEK_KAYNAK_SOZLESMESINI_Tasir()
        {
            // GF-2b/K5: panelin JS'i `admin.html`in satir ici <script> blogundan
            // `frontend/admin.js`e tasindi (admin CSP'sinden `script-src 'unsafe-inline'`
            // kaldirilabilsin diye). Bu pinin OLCTUGU SEY DEGISMEDI: operator override'i
            // ve sabit yedegin yoklugu artik JS dosyasinda. Panel BUTUN olarak okunur;
            // html ONCE gelir - meta ve CSP okuyan yardimcilar ilk eslesmeyi bulur.
            // OLCULDU: `admin.js` icinde "localhost:5000" GECISI 0, yani asagidaki NEG
            // assert birlestirmeden ETKILENMEZ ve `ops/set-api-origin.sh`in uc dosyalik
            // kapsami da EKSIK KALMAZ.
            var admin = Oku("frontend/admin.html") + "\n" + Oku("frontend/admin.js");
            var origin = BeyanEdilenOrigin(admin);
            var csp = CspIcerigi(admin);

            origin.Should().Be(BeyanEdilenOrigin(Oku("frontend/index.html")),
                "iki yuzey AYNI origin'i beyan etmeli - ayrisirlarsa dagitim yine elle senkrona doner");

            foreach (var d in new[] { "img-src", "connect-src" })
            {
                var deger = DirektifDegeri(csp, d);
                deger.Should().NotBeNull($"admin CSP'de '{d}' direktifi bulunmali");
                deger!.Should().Contain(origin, $"admin CSP '{d}' direktifi de ayni origin'i tasimali");
            }

            admin.Should().Contain("localStorage.getItem(\"divisima_api_base\")",
                "operator override'i KORUNMALI - paneli baska bir ortama yoneltmek mesru bir ihtiyac");
            admin.Should().NotContain("|| \"http://localhost:5000\"",
                "ama override'in ARDINDAKI sabit yedek KALKMALI - taban meta'dan turemeli");
        }

        // ── 4) DAVRANIS PINI: tek girdiyle degistirme TUM yerleri kapsar ──────────────────
        // ops/set-api-origin.sh'in yaptigi isin TA KENDISI burada bellekte simule edilir:
        // beyan edilen origin'in duz metin olarak degistirilmesi YETERLI OLMALI. Yeni bir
        // yerde ikinci bir literal belirirse (or. baska bir bicimde yazilmis origin) bu pin
        // KIRMIZI verir.
        [Fact]
        public void TEK_GIRDIYLE_DEGISTIRME_TUM_YERLERI_KAPSAR_ESKI_ORIGIN_KALMAZ()
        {
            var yollar = new[] { "frontend/index.html", "frontend/admin.html", "frontend/api-bridge.js" };
            var index = Oku("frontend/index.html");
            var eski = BeyanEdilenOrigin(index);

            var yeniler = new Dictionary<string, string>();
            foreach (var y in yollar)
                yeniler[y] = Oku(y).Replace(eski, TestOrigin, StringComparison.Ordinal);

            // (a) yeni origin BEKLENEN her yerde
            BeyanEdilenOrigin(yeniler["frontend/index.html"]).Should().Be(TestOrigin);
            BeyanEdilenOrigin(yeniler["frontend/admin.html"]).Should().Be(TestOrigin);
            var yeniCsp = CspIcerigi(yeniler["frontend/index.html"]);
            foreach (var d in new[] { "img-src", "connect-src", "form-action" })
                DirektifDegeri(yeniCsp, d)!.Should().Contain(TestOrigin);

            // (b) CIFT-ANLAM KIRICI: iki origin BIRBIRINE KARISMAZ - eski origin HICBIR
            // dosyada KALMAMALI. Kalirsa dagitim "yarim" olur ve bugunku zarar geri gelir.
            foreach (var y in yollar)
                yeniler[y].Should().NotContain(eski,
                    $"{y}: tek girdiyle degistirme sonrasi eski origin KALMAMALI");

            // (c) VAKUM KIRICI: degistirmenin GERCEKTEN bir sey yaptigini dogrula - aksi
            // halde origin hic gecmiyorsa (b) bedavaya yesil kalirdi.
            yeniler["frontend/index.html"].Should().NotBe(index);
        }

        // ── 5) CALISMA ANI GUARD'I - UC DIREKTIFI DE KONTROL EDER ve GURULTULUDUR ─────────
        [Fact]
        public void CALISMA_ANI_GUARD_I_UC_DIREKTIFI_de_Kontrol_Eder_ve_GURULTULUDUR()
        {
            var index = Oku("frontend/index.html");

            index.Should().Contain("['img-src','connect-src','form-action']",
                "guard UC direktifin UCUNU de kontrol etmeli - biri disarida kalirsa " +
                "tam da bugunku 'sessizce yanlis origin' durumu geri gelir");
            index.Should().Contain("setAttribute('role','alert')",
                "uyari EKRANDA gorunmeli - yalniz konsola yazmak son kullanicida SESSIZDIR");
            index.Should().Contain("ops/set-api-origin.sh",
                "uyari ne yapilmasi gerektigini SOYLEMELI");

            // CIFT-ANLAM KIRICI: guard, API storefront ile AYNI origin'de servis edilirse
            // 'self' kapsamini kabul etmeli - yoksa mesru bir dagitimda YANLIS ALARM verirdi.
            index.Should().Contain("ayniOrigin",
                "ayni-origin dagitimi 'self' ile kapsanir; guard bunu YANLIS ALARM saymamali");
        }

        // ── 6) DAGITIM MEKANIZMASI VE CHECKLIST GERCEKTEN VAR ────────────────────────────
        [Fact]
        public void DAGITIM_BETIGI_ve_CHECKLIST_MADDESI_VAR()
        {
            var betik = Oku("ops/set-api-origin.sh");
            betik.Should().Contain("--verify", "dogrulama modu olmali - yayin sonrasi kontrol edilebilsin");
            foreach (var d in new[] { "index.html", "admin.html", "api-bridge.js" })
                betik.Should().Contain(d, $"betik {d} dosyasini da kapsamali");
            betik.Should().Contain("Iyzico:CallbackUrl",
                "form-action <-> CallbackUrl SENKRON KURALI betikte hatirlatilmali");

            var checklist = Oku("ops/deployment-checklist.md");
            checklist.Should().Contain("set-api-origin.sh", "checklist maddesi olmali");
            checklist.Should().Contain("VERSION", "SW surum bump'i ayni maddede hatirlatilmali");
            checklist.Should().Contain("Iyzico:CallbackUrl", "senkron kurali checklist'te de olmali");
        }
    }
}
