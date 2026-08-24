using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Divisima.Core.Utilities.Enums;
using Divisima.Core.Utilities.Orders;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ DALGA C - DAGITIM SOZLESMESI ═══════════════════════════════════════════════════════
    //
    // Bu dosya, DAVRANISLA dogrulanamayan kalemleri tutar. Docker imaji, nginx yapilandirmasi
    // ve HTML meta etiketleri bu suitte AYAGA KALDIRILAMAZ; "kaldirabiliyormus gibi" yapan bir
    // pin yalanci guvence olurdu. Onun yerine ARTEFAKTIN KENDISI okunur ve olculen boslugun
    // kapali kaldigi dogrulanir.
    //
    // OLCULEN ONCE-DURUM (hepsi Dalga C'de birebir tespit edildi):
    //   C1 Dockerfile yalniz Divisima.API'yi publish ediyordu, docker-compose'da frontend
    //      servisi YOKTU, nginx.conf'ta TEK server block vardi (api.divisima.com) ve
    //      Divisima.API/wwwroot yalnizca uploads/products iceriyordu. Yani storefront'u KIMIN
    //      sunacagi depoda HICBIR YERDE tanimli degildi.
    //   C2 compose'da mssql_data ve redis_data volume'leri vardi, YUKLEMELER icin YOKTU ->
    //      konteyner degisince admin'in yukledigi tum urun gorselleri kaybolurdu.
    //   C5 robots.txt "Sitemap: https://divisima.com/sitemap.xml" diyordu, sitemap'i URETEN uc
    //      (/api/seo/sitemap) VARDI, ama o adresi SUNAN hicbir sey YOKTU. Ayrica og:image ve
    //      og:url YOKTU - paylasimlar gorselsiz cikiyordu.
    //   C6b Kargo ekrani KOR FORMDU - operatorden siparis ID'si elle isteniyordu.
    public class DalgaCDagitimSozlesmesiTests
    {
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

        // ══ C1 - STOREFRONT'U SUNAN TANIM VAR ═══════════════════════════════════════════════
        [Fact]
        public void NGINX_HEM_API_HEM_STOREFRONT_BLOGUNU_Tasir_ve_SITEMAP_PROXYLENIR()
        {
            var conf = Oku("ops/infra/nginx.conf");

            // VAKUM KIRICI: dosya gercekten nginx yapilandirmasi olmali (bos/yanlis bir dosya
            // asagidaki "icerir" assertlerinin hepsini bedavaya dusururdu).
            conf.Should().Contain("proxy_pass", "nginx.conf gercek bir ters proxy yapilandirmasi olmali");

            conf.Should().Contain("server_name api.divisima.com", "API blogu KORUNMALI");
            conf.Should().Contain("server_name divisima.com", "storefront blogu EKLENMIS olmali - eksik olan buydu");

            // SITEMAP ZINCIRI: robots.txt'in gosterdigi adresi SUNAN tanim olmali.
            conf.Should().Contain("location = /sitemap.xml");
            conf.Should().Contain("/api/seo/sitemap", "sitemap'i ureten uc BUDUR - statik bir dosya degil");

            // SPA fallback: hash router, bilinmeyen YOL yok.
            conf.Should().Contain("try_files $uri $uri/ /index.html");

            // Admin paneli arama motoruna kapali (robots.txt bir SOZLESMEDIR, zorlayici degil).
            conf.Should().Contain("X-Robots-Tag");
        }

        [Fact]
        public void COMPOSE_STOREFRONT_SERVISINI_Tasir_ve_YAPILANDIRMASI_DEPODA()
        {
            var compose = Oku("docker-compose.yml");

            compose.Should().Contain("frontend:", "storefront'u sunan servis compose'da OLMALI");
            compose.Should().Contain("./frontend:/usr/share/nginx/html:ro",
                "vitrin dosyalari SALT-OKUR baglanmali - origin yazimi bir DAGITIM adimidir (set-api-origin.sh), calisma ani isi degil");

            // Servisin yapilandirmasi da depoda olmali; aksi halde "calisiyor" iddiasi
            // makinede duran bir dosyaya bagli kalirdi.
            var devConf = Oku("ops/infra/frontend-dev.conf");
            devConf.Should().Contain("/api/seo/sitemap", "compose'daki vitrin de sitemap'i proxy'lemeli");
            devConf.Should().Contain("try_files $uri $uri/ /index.html");
        }

        // ══ C2 - YUKLENEN GORSELLER KALICI ══════════════════════════════════════════════════
        [Fact]
        public void YUKLEME_DIZINI_KALICI_VOLUME_de_ve_SAHIPLIK_ZINCIRI_KURULU()
        {
            var compose = Oku("docker-compose.yml");
            compose.Should().Contain("uploads_data:/app/wwwroot/uploads",
                "yuklenen gorseller konteynerin YAZILABILIR KATMANINDA kalmamali");
            // Adlandirilmis volume TANIMLI olmali - mount satiri tek basina yetmez.
            compose.Should().MatchRegex(@"volumes:(.|\n)*\n\s{2}uploads_data:",
                "adlandirilmis volume compose'un volumes bolumunde TANIMLI olmali");

            // SAHIPLIK ZINCIRI - BU SATIR OLMADAN VOLUME SESSIZCE KIRILIR:
            // .dockerignore yuklemeleri build context'inden disliyor -> publish bos dizini
            // KOPYALAMAZ -> imajda /app/wwwroot/uploads YOK -> Docker volume'u root:root
            // olusturur -> `USER divisima` YAZAMAZ. Dizin acikca olusturulmali ve chown'dan
            // ONCE gelmeli.
            var dockerfile = Oku("Dockerfile");
            dockerfile.Should().Contain("mkdir -p /app/wwwroot/uploads",
                "yukleme dizini imajda ACIKCA olusturulmali (volume sahipligi oradan devralinir)");
            var mkdirIdx = dockerfile.IndexOf("mkdir -p /app/wwwroot/uploads", StringComparison.Ordinal);
            var chownIdx = dockerfile.IndexOf("chown -R divisima:divisima /app", StringComparison.Ordinal);
            chownIdx.Should().BeGreaterThan(-1, "non-root sahiplik adimi KORUNMALI");
            mkdirIdx.Should().BeLessThan(chownIdx, "dizin chown'DAN ONCE olusturulmali - sonra olusturulursa root:root kalir");

            var dockerignore = Oku(".dockerignore");
            dockerignore.Should().Contain("Divisima.API/wwwroot/uploads",
                "dev makinesindeki yuklenmis gorseller uretim imajina GIRMEMELI (publish onlari ciktiya kopyaliyor - olculdu)");
        }

        // ══ C5 - PAYLASIM ONIZLEMESI ve SITEMAP ZINCIRI ═════════════════════════════════════
        [Fact]
        public void PAYLASIM_ETIKETLERI_TAM_ve_KART_TURU_GORSELLE_TUTARLI()
        {
            var html = Oku("frontend/index.html");
            var bas = html.Substring(0, Math.Min(html.Length, 8000));   // <head> bolgesi

            bas.Should().Contain("property=\"og:image\"", "paylasimlarda gorsel YOKTU");
            bas.Should().Contain("property=\"og:url\"", "kanonik paylasim adresi YOKTU");

            // Mutlak URL sarti: paylasim botlari goreli yolu cozemez.
            bas.Should().Contain("content=\"https://divisima.com/icons/icon-512.png\"");

            // Gorselin GERCEKTEN var oldugu dogrulanir - olmayan bir dosyaya isaret eden
            // og:image, etiketi hic koymamaktan daha kotudur (bot 404 alir).
            File.Exists(Path.Combine(KokDizin.Value, "frontend", "icons", "icon-512.png"))
                .Should().BeTrue("og:image'in gosterdigi dosya depoda BULUNMALI");

            // CIFT-ANLAM KIRICI: etiketi eklemek yetmez, KART TURU gorselle tutarli olmali.
            // "summary_large_image" 1200x630 bekler; elimizdeki varlik 512x512 KARE.
            bas.Should().Contain("name=\"twitter:card\" content=\"summary\"",
                "kare gorselle genis kart vaat edilmemeli - eski hali 'summary_large_image' idi ve HICBIR gorsel vermiyordu");

            // Organization schema'sindaki logo da var olan bir dosyayi gostermeli
            // (once https://divisima.com/logo.png idi - depoda BOYLE BIR DOSYA YOK).
            html.Should().NotContain("divisima.com/logo.png", "olmayan bir logo dosyasina isaret edilmemeli");
        }

        [Fact]
        public void ROBOTS_SITEMAP_ADRESI_NGINX_IN_SUNDUGU_ADRESLE_ORTUSUR()
        {
            var robots = Oku("frontend/robots.txt");
            var conf = Oku("ops/infra/nginx.conf");

            // robots.txt'teki Sitemap satirindan YOLU cikar ve nginx'in o yolu sundugunu dogrula.
            var satir = robots.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase));
            satir.Should().NotBeNull("robots.txt bir Sitemap satiri tasimali");
            var url = satir!.Split(':', 2)[1].Trim();
            var yol = new Uri(url).AbsolutePath;                       // "/sitemap.xml"

            conf.Should().Contain($"location = {yol}",
                $"robots.txt {url} adresini gosteriyor; nginx bu YOLU SUNMALI - once hicbir sey sunmuyordu ve arama motoru OLU bir adrese gonderiliyordu");

            robots.Should().Contain("Disallow: /admin.html", "yonetim paneli indekslenmemeli");
        }

        // ══ C6b - KARGOLANMAYI BEKLEYENLER ══════════════════════════════════════════════════
        [Fact]
        public void KARGO_EKRANI_BEKLEYEN_SIPARIS_LISTESI_Tasir_ve_DURUM_MAKINESIYLE_TUTARLI()
        {
            var admin = Oku("frontend/admin.html");

            admin.Should().Contain("kargoBekleyenler", "kargo ekrani artik KOR FORM olmamali");
            admin.Should().Contain("kargoFormunaAl", "listeden forma gecis olmali - operator ID kopyalamamali");

            // ASIL SOZLESME: panelin filtreledigi durum, DURUM MAKINESININ Shipped'e izin
            // verdigi TEK durum olmali. Deger elle "2" diye dogrulanmiyor - MAKINEDEN HESAPLANIYOR,
            // yani makine degisirse bu pin kirilir.
            var kargolanabilir = Enum.GetValues<OrderStatusEnum>()
                .Where(s => s != OrderStatusEnum.Shipped &&
                            OrderStatusMachine.IsValidTransition(s, OrderStatusEnum.Shipped))
                .ToList();

            kargolanabilir.Should().ContainSingle(
                "durum makinesine gore Shipped'e gecebilen TEK bir durum olmali; birden fazlaysa panel filtresi de genisletilmeli");
            var beklenen = (byte)kargolanabilir[0];

            admin.Should().Contain($"allOrders({{status:{beklenen}",
                $"panel {beklenen} ({kargolanabilir[0]}) durumundaki siparisleri listelemeli - baska bir durum gostermek "
                + "operatore ucun REDDEDECEGI siparisleri sunmak olurdu");
        }

        // ══ C3 - GUVENLI VARSAYILAN ═════════════════════════════════════════════════════════
        [Fact]
        public void ADMIN_TOHUMLAMA_VARSAYILAN_OLARAK_KAPALI_ve_SIFRE_ALANI_BOS()
        {
            using var doc = JsonDocument.Parse(Oku("Divisima.API/appsettings.json"));
            var seed = doc.RootElement.GetProperty("AdminSeed");

            seed.GetProperty("Enabled").GetBoolean().Should().BeFalse(
                "commit'li varsayilan KAPALI olmali - yanlislikla admin acilmasin");
            seed.GetProperty("Password").GetString().Should().BeEmpty(
                "sifre appsettings'e YAZILMAZ; uretimde env/Key Vault'tan gelir");
            seed.GetProperty("Email").GetString().Should().BeEmpty();
        }
    }
}
