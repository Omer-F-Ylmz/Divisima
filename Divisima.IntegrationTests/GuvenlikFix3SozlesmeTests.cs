using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ GUVENLIK-FIX-3 - DAGITIM YUZEYI SOZLESMESI ═════════════════════════════════════════
    //
    // GUVENLIK DALGASI 2'nin (yalniz-olcum) kalemlerinin karsiligi. Bu sinif VERITABANI ACMAZ -
    // kasitli: 10d794d CI kirmizisinda olculdu ki "kendi veritabanini kuran" her yeni sinif
    // SQL Server'in `model` kilidinde bir katilimci daha olur ve BASKA siniflari dusurebilir.
    // Buradaki pinlerin hicbirinin veritabanina ihtiyaci yok.
    //
    // PIN SINIRI (DURUST KAYIT): nginx bu suitte AYAGA KALDIRILAMAZ (olculdu: makinede ne
    // `nginx` ne `docker` var). Pinler nginx'i KOSTURMAZ; artefakti okur ve `location`
    // cozumlemesini SIMULE EDER. Simulasyon nginx'in gercek onceligini uygular (once `=`,
    // sonra en uzun `^~`, sonra YAPILANDIRMA SIRASINDA regex'ler, sonra en uzun prefix) ama
    // nginx'in TAMAMI degildir - ic ice location / `rewrite` / `break` yoktur (bu iki
    // yapilandirmada da yoktur). "nginx gercekten boyle davraniyor" kaniti ancak sunucuda
    // `curl -sI` ile alinir; o adim ops/deployment-checklist.md'ye ZORUNLU madde olarak yazildi.
    public class GuvenlikFix3SozlesmeTests
    {
        private const string BaslikDosyasi = "divisima-security-headers.conf";

        private static readonly Lazy<string> KokDizin = new(() =>
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "docker-compose.yml")))
                d = d.Parent;
            if (d == null)
                throw new InvalidOperationException(
                    "Depo koku bulunamadi: docker-compose.yml iceren ust dizin yok. " +
                    "Sessiz skip YOK - bu pinler artefakti okuyamadan yesil kalamaz.");
            return d.FullName;
        });

        private static string Oku(string goreliYol)
        {
            var tam = Path.Combine(KokDizin.Value, goreliYol.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(tam).Should().BeTrue($"pinlenen artefakt bulunmali: {goreliYol}");
            return File.ReadAllText(tam);
        }

        // ── Susli parantez sayarak bir blogun govdesini cikarir (ic ice bloklar dahil). ──
        private static string GovdeyiCikar(string metin, int acilisIndeksi)
        {
            var derinlik = 0;
            for (var i = acilisIndeksi; i < metin.Length; i++)
            {
                if (metin[i] == '{')
                {
                    derinlik++;
                }
                else if (metin[i] == '}')
                {
                    derinlik--;
                    if (derinlik == 0) return metin.Substring(acilisIndeksi, i - acilisIndeksi + 1);
                }
            }

            throw new InvalidOperationException("Kapanmayan blok: nginx yapilandirmasi bozuk.");
        }

        private static IEnumerable<string> ServerBloklari(string conf)
        {
            foreach (Match m in Regex.Matches(conf, @"^server\s*\{", RegexOptions.Multiline))
                yield return GovdeyiCikar(conf, m.Index + m.Value.IndexOf('{'));
        }

        // server_name TOKEN BAZLI eslesir - alt dize DEGIL. Gerekce olculdu: `\bdivisima\.com\b`
        // deseni `api.divisima.com` ICINDE de eslesiyor ve iki blok karisiyor (bu pin ilk
        // kosumda tam bunu yakaladi: storefront asserti API blogu uzerinde kosuyordu).
        private static string ServerBlogu(string conf, string serverName)
        {
            foreach (var govde in ServerBloklari(conf))
            {
                var m = Regex.Match(govde, @"server_name\s+([^;]+);");
                if (!m.Success) continue;

                var adlar = m.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (adlar.Contains(serverName, StringComparer.Ordinal)) return govde;
            }

            throw new InvalidOperationException($"server_name '{serverName}' iceren blok bulunamadi.");
        }

        private sealed record Konum(string Belirtec, string Desen, string Govde);

        private static List<Konum> Konumlar(string serverGovdesi)
        {
            var liste = new List<Konum>();
            foreach (Match m in Regex.Matches(serverGovdesi, @"location\s+(=|\^~|~\*|~)?\s*(\S+)\s*\{"))
            {
                var govde = GovdeyiCikar(serverGovdesi, m.Index + m.Value.LastIndexOf('{'));
                liste.Add(new Konum(m.Groups[1].Value, m.Groups[2].Value, govde));
            }

            return liste;
        }

        // ── nginx location onceligi: `=` > `^~` (en uzun) > regex (YAPILANDIRMA SIRASINDA) > prefix ──
        private static Konum Cozumle(string yol, List<Konum> konumlar)
        {
            var tam = konumlar.FirstOrDefault(k => k.Belirtec == "=" && k.Desen == yol);
            if (tam != null) return tam;

            var onEk = konumlar
                .Where(k => k.Belirtec == "^~" && yol.StartsWith(k.Desen, StringComparison.Ordinal))
                .OrderByDescending(k => k.Desen.Length)
                .FirstOrDefault();
            if (onEk != null) return onEk;

            foreach (var k in konumlar.Where(k => k.Belirtec == "~" || k.Belirtec == "~*"))
            {
                var secenek = k.Belirtec == "~*" ? RegexOptions.IgnoreCase : RegexOptions.None;
                if (Regex.IsMatch(yol, k.Desen, secenek)) return k;
            }

            var duz = konumlar
                .Where(k => k.Belirtec.Length == 0 && yol.StartsWith(k.Desen, StringComparison.Ordinal))
                .OrderByDescending(k => k.Desen.Length)
                .FirstOrDefault();
            if (duz == null)
                throw new InvalidOperationException($"'{yol}' hicbir location'a dusmedi - yapilandirma eksik.");

            return duz;
        }

        private static bool Reddedilir(string yol, List<Konum> konumlar) =>
            Regex.IsMatch(Cozumle(yol, konumlar).Govde, @"return\s+404\s*;");

        // ══ #4 - CLICKJACKING: IKI BLOK DA KORUMALI ═════════════════════════════════════════
        [Fact]
        public void IKI_SERVER_BLOGU_DA_CLICKJACKINGE_KAPALI_ve_CSP_YALNIZ_frame_ancestors_Tasir()
        {
            var conf = Oku("ops/infra/nginx.conf");

            // VAKUM KIRICI: dosya gercekten nginx yapilandirmasi olmali - bos/yanlis bir dosya
            // asagidaki "icerir" assertlerini bedavaya dusururdu.
            conf.Should().Contain("proxy_pass", "nginx.conf gercek bir ters proxy yapilandirmasi olmali");

            // API blogu: X-Frame-Options ZATEN vardi, geri alinmamali.
            ServerBlogu(conf, "api.divisima.com").Should()
                .MatchRegex(@"add_header\s+X-Frame-Options\s+""DENY""",
                    "api blogunun clickjacking korumasi geri alinmamali");

            // Storefront blogu: koruma TEK KAYNAKTAN (include) gelir.
            ServerBlogu(conf, "divisima.com").Should()
                .Contain("include " + BaslikDosyasi,
                    "storefront blogu guvenlik basliklarini tek kaynaktan almali (GUVENLIK DALGASI 2 / #4)");

            var basliklar = Oku("ops/infra/" + BaslikDosyasi);
            basliklar.Should().MatchRegex(@"add_header\s+X-Frame-Options\s+""DENY""");
            basliklar.Should().MatchRegex(@"add_header\s+Content-Security-Policy\s+""frame-ancestors 'none'""",
                "meta CSP'de frame-ancestors SPEC GEREGI yok sayilir - koruma HTTP basligindan gelmeli");

            // CIFT-ANLAM KIRICI: baslik YALNIZ frame-ancestors tasimali. script-src/connect-src
            // buraya konursa `ops/set-api-origin.sh`in BILMEDIGI ikinci bir senkron noktasi acilir
            // ve M1 (elle senkron) tuzagi geri gelir - o betik yalniz HTML meta'sini yazar.
            var cspSatiri = basliklar
                .Split('\n')
                .Single(s => s.Contains("Content-Security-Policy", StringComparison.Ordinal) && s.TrimStart().StartsWith("add_header", StringComparison.Ordinal));
            foreach (var direktif in new[] { "script-src", "connect-src", "img-src", "form-action", "default-src" })
            {
                cspSatiri.Should().NotContain(direktif,
                    $"nginx basligindaki CSP '{direktif}' TASIMAMALI - o direktif meta'da durur ve set-api-origin.sh onu yazar");
            }
        }

        // ══ #4 - DEVRALMA TUZAGI: BASLIK TANIMLAYAN HER LOCATION INCLUDE ETMELI ═════════════
        [Fact]
        public void KENDI_add_header_TANIMLAYAN_HER_STOREFRONT_LOCATIONU_BASLIK_DOSYASINI_INCLUDE_Eder()
        {
            var storefront = ServerBlogu(Oku("ops/infra/nginx.conf"), "divisima.com");

            var kendiBasligiOlanlar = Konumlar(storefront)
                .Where(k => Regex.IsMatch(k.Govde, @"^\s*add_header", RegexOptions.Multiline))
                .ToList();

            // VAKUM KIRICI: tarama gercekten bir sey bulmus olmali. Bu iki location (admin.html
            // ve html|js|json) kalkarsa assert bedavaya dogru olurdu.
            kendiBasligiOlanlar.Should().HaveCountGreaterThanOrEqualTo(2,
                "kendi add_header'ini tanimlayan location'lar hala duruyor olmali; yoksa bu tarama vakuma duser");

            foreach (var k in kendiBasligiOlanlar)
            {
                k.Govde.Should().Contain("include " + BaslikDosyasi,
                    $"nginx'te add_header YALNIZCA o seviyede hic add_header yoksa devralinir - " +
                    $"'location {k.Belirtec} {k.Desen}' kendi basligini tanimladigi icin guvenlik " +
                    $"basliklarini DEVRALMAZ ve onlari acikca include ETMELIDIR");
            }
        }

        // ══ #4 - API BLOGUNA IKINCI CSP EKLENMEDI (OLCUME DAYALI KARAR) ════════════════════
        [Fact]
        public void API_BLOGUNA_IKINCI_CSP_BASLIGI_EKLENMEZ_UYGULAMA_ZATEN_Gonderiyor()
        {
            // Kararin DAYANDIGI PREMIS de pinli: uygulama frame-ancestors'i KENDISI basiyor.
            // Middleware'den kalkarsa bu pin kirilir ve okuyucuya "artik nginx kapatmali" der.
            var mw = Oku("Divisima.API/Middlewares/SecurityHeadersMiddleware.cs");
            mw.Should().Contain("frame-ancestors 'none'",
                "API tarafinin korumasi uygulama middleware'inden gelir; kalkarsa nginx'in kapatmasi gerekir");
            mw.Should().Contain("X-Frame-Options", "eski tarayicilar icin X-Frame-Options da korunmali");

            // CIFT-ANLAM KIRICI: storefront'ta CSP VAR ama API blogunda YOK - yani "hicbir yerde
            // CSP yok" diyen bir uygulama bu testi GECEMEZ.
            var conf = Oku("ops/infra/nginx.conf");
            ServerBlogu(conf, "api.divisima.com").Should().NotContain("Content-Security-Policy",
                "uygulama her API yanitina zaten TAM bir CSP basiyor (SecurityHeadersMiddleware, UseStaticFiles'DAN once) - " +
                "nginx'ten ikincisini eklemek her yanitta iki bagimsiz politika dogururdu, kazanc SIFIR");
            ServerBlogu(conf, "divisima.com").Should().Contain("include " + BaslikDosyasi,
                "storefront STATIK dosyadir - hicbir middleware kosmaz, tek kaynak nginx'tir");
        }

        // ══ #6 - IC DOKUMANLAR KAPALI, IHTIYAC DUYULAN DOSYALAR ACIK ═══════════════════════
        [Fact]
        public void IC_DOKUMANLAR_404_STOREFRONTUN_IHTIYACI_OLAN_DOSYALAR_SERVIS_EDILIR()
        {
            var konumlar = Konumlar(ServerBlogu(Oku("ops/infra/nginx.conf"), "divisima.com"));

            var kapali = new[]
            {
                "/API-CONTRACT.md", "/INTEGRATION.md", "/SEO-ANALYTICS.md",
                "/pwa/README.md", "/vendor/README.txt", "/test/mobil-erisilebilirlik.js",
                "/.git/config", "/.env", "/index.html~", "/backup.sql", "/deploy.sh",
            };

            var acik = new[]
            {
                "/", "/index.html", "/admin.html", "/api-bridge.js", "/api-client.js",
                "/pwa-register.js", "/service-worker.js", "/manifest.json", "/robots.txt",
                "/vendor/purify.min.js", "/icons/icon-512.png",
                // RFC 9116: gizli-dosya kuralina TAKILIRDI. Acik `^~` muafiyeti olmadan bu
                // satir KIRILIR - muafiyetin tek dogrudan kaniti budur.
                "/.well-known/security.txt",
            };

            // VAKUM KIRICI: kural gercekten bir seyi koruyor olmali. Kapatilan dokumanin ve
            // muafiyetin korudugu dosyanin depoda VAR oldugu dogrulanir; bos bir frontend/
            // agacinda iki liste de anlamsiz olurdu.
            var vitrinKoku = Path.Combine(KokDizin.Value, "frontend");
            File.Exists(Path.Combine(vitrinKoku, "API-CONTRACT.md")).Should().BeTrue(
                "kapatilan ic dokuman depoda GERCEKTEN bulunmali; yoksa deny kurali hicbir seyi korumuyor demektir");
            File.Exists(Path.Combine(vitrinKoku, ".well-known", "security.txt")).Should().BeTrue(
                "muafiyetin korudugu dosya depoda GERCEKTEN bulunmali");

            foreach (var yol in kapali)
                Reddedilir(yol, konumlar).Should().BeTrue($"'{yol}' disariya servis EDILMEMELI");

            foreach (var yol in acik)
                Reddedilir(yol, konumlar).Should().BeFalse($"'{yol}' storefront'un ihtiyaci - kapsama GIRMEMELI");
        }

        // ══ #6 - DEV IKIZI: AYNI DENY KURALLARI, /test/ BILINCLI OLARAK ACIK ═══════════════
        [Fact]
        public void DEV_KONFIGI_AYNI_DENY_KURALLARINI_Tasir_ama_OLCUM_BETIGI_YERELDE_ACIK_KALIR()
        {
            var devKonumlar = Konumlar(ServerBloklari(Oku("ops/infra/frontend-dev.conf")).Single());

            Reddedilir("/API-CONTRACT.md", devKonumlar).Should().BeTrue(
                "ic dokuman kurali yerelde de kosmali - yoksa uretimdeki 404 yerelde HIC denenmemis olur");
            Reddedilir("/.env", devKonumlar).Should().BeTrue();
            Reddedilir("/.well-known/security.txt", devKonumlar).Should().BeFalse(
                "RFC 9116 muafiyeti yerelde de gecerli olmali");

            // CIFT-ANLAM KIRICI: "her seyi kapat" YANLIS duzeltmedir. Dalga 4'un pin boslugunu
            // telafi eden olcum betigi YERELDE erisilebilir kalmalidir.
            Reddedilir("/test/mobil-erisilebilirlik.js", devKonumlar).Should().BeFalse(
                "olcum betigi yerelde ACIK kalmali (Dalga 4 telafisi)");

            var uretimKonumlar = Konumlar(ServerBlogu(Oku("ops/infra/nginx.conf"), "divisima.com"));
            Reddedilir("/test/mobil-erisilebilirlik.js", uretimKonumlar).Should().BeTrue(
                "ayni betik URETIMDE kapali olmali - ayrisma bilincli ve TEK YONLU");
        }

        // ══ #3 + #7 + #8 - CHECKLIST ZORUNLU MADDELERI ═════════════════════════════════════
        [Fact]
        public void CHECKLIST_PROXY_PORT_ARKAPLAN_ve_DNS_MADDELERINI_Tasir()
        {
            var c = Oku("ops/deployment-checklist.md");

            // VAKUM KIRICI: dosya gercekten bir kontrol listesi olmali.
            c.Should().Contain("- [ ]", "deployment-checklist.md gercek bir kontrol listesi olmali");

            c.Should().Contain("ForwardedHeaders:KnownProxies",
                "#3: topolojiye bagli SESSIZ hatanin tek panzehiri bu maddedir");
            c.Should().Contain("X-Forwarded-For: 8.8.8.8",
                "#3: yayin sonrasi DAVRANIS dogrulamasi - iki farkli XFF ayri kova almali");
            c.Should().Contain("ForwardLimit",
                "#3: cok hop'lu zincirde okunan IP bir onceki proxy'nindir");
            // Tek satir icinde aranir: satir sinirini asan bir arama CRLF/LF farkinda kirilgandir.
            c.Should().Contain("portu **public",
                "#3: API'nin 5000 portu disariya acilmamali");
            c.Should().Contain("BackgroundJobs:Enabled",
                "#8: bayrak yanlissa outbox SESSIZCE durur");
            c.Should().Contain("status = 1 (Processed)",
                "#8: dogrulama konfigurasyona degil SONUCA bakmali");
            c.Should().Contain("subdomain takeover",
                "#7: cerez .divisima.com kapsaminda - DNS hijyeni maddesi");
            c.Should().Contain(BaslikDosyasi,
                "#4: include dosyasi kurulmazsa nginx acilmaz - kurulum maddesi olmali");
            c.Should().Contain("/.well-known/security.txt",
                "#6: kapsamin fazla genis OLMADIGI da dogrulanmali");
        }
    }
}
