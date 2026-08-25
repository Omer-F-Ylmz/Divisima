using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Divisima.Bussiness.Concrete;
using Divisima.Core.Security.RateLimiting;
using Divisima.Core.Utilities.Enums;
using FluentAssertions;
using Xunit;

namespace Divisima.IntegrationTests
{
    // ══ BUYUK DENETIM / FAZ 0 - ENVANTER TEMIZLIGI SOZLESMELERI ════════════════════════════
    //
    // Bu sinif VERITABANI ACMAZ - kasitli: 10d794d CI kirmizisinda olculdu ki "kendi
    // veritabanini kuran" her yeni sinif SQL Server'in `model` kilidinde bir katilimci daha
    // olur ve BASKA siniflari dusurebilir. Buradaki pinlerin hicbirinin veritabanina ihtiyaci
    // yok: ikisi kaynak/artefakt sozlesmesi, ucu SAF fonksiyon birimi.
    //
    // Davranis pinleri BASKA yerde (mevcut host'lari yeniden kullaniyorlar):
    //   p-k1b -> StorefrontCatalogContractTests (ETag davranisi, anonim GET)
    //   p-k2  -> PaymentCallbackRedirectTests   (initialize 429, iki-host deseni)
    //   p-k6a/b -> DalgaBOperasyonTests         (admin denetim ucu)
    public class Faz0SozlesmeTests
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
            File.Exists(tam).Should().BeTrue($"pin '{goreliYol}' dosyasini okumali - yoksa vakuma duser");
            return File.ReadAllText(tam);
        }

        // Kaynak tararken KENDI belgeledigimiz kalibi bulmamak icin yorum satirlari ayiklanir.
        // (Bu tuzagin bedeli depoda IKI KEZ odendi: Dalga B ve Dalga D / D2.)
        private static string YorumsuzKaynak(string goreliYol)
        {
            var satirlar = Oku(goreliYol).Split('\n');
            return string.Join("\n", satirlar.Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        }

        // ═══ p-k1a - OLU ETAG ONEKI YAPISAL OLARAK YASAK ═══════════════════════════════════
        //
        // K1'de olculdu: liste "/api/sizeguide" iddia ediyordu ama gercek rota "api/size-guide"
        // ve eslesme StartsWithSegments ile SEGMENT SINIRLI - yani onek ILK COMMIT'ten beri HIC
        // eslesmiyordu. Bu pin, listedeki HER onegin gercek bir uca segment-eslestigini tarar;
        // olu bir onek yeniden eklenirse KIRILIR.
        [Fact]
        public void ETAG_ONEK_LISTESINDE_OLU_ONEK_OLAMAZ_HEPSI_GERCEK_ROTAYA_SEGMENT_ESLESIR()
        {
            var kaynak = YorumsuzKaynak("Divisima.API/Middlewares/ETagMiddleware.cs");
            var dizi = Regex.Match(kaynak, @"CacheablePrefixes\s*=\s*\{([^}]*)\}");
            dizi.Success.Should().BeTrue("onek listesi kaynakta bulunmali - yoksa tarama vakuma duser");

            var onekler = Regex.Matches(dizi.Groups[1].Value, "\"([^\"]+)\"")
                               .Select(m => m.Groups[1].Value)
                               .ToList();

            // VAKUM KIRICI: liste GERCEKTEN dolu olmali, yoksa "hicbiri olu degil" bedava dogru olur.
            onekler.Should().HaveCountGreaterThan(1, "ETag kapsami bos birakilmis olamaz");

            // Gercek rota kumesi: controller'larin sinif duzeyi [Route] onekleri.
            var rotalar = new List<string>();
            var ctrlDizin = Path.Combine(KokDizin.Value, "Divisima.API", "Controllers");
            foreach (var f in Directory.GetFiles(ctrlDizin, "*Controller.cs"))
            {
                var metin = File.ReadAllText(f);
                var m = Regex.Match(metin, "\\[Route\\(\"([^\"]+)\"\\)\\]");
                if (!m.Success) continue;
                var ad = Path.GetFileNameWithoutExtension(f).Replace("Controller", "", StringComparison.Ordinal);
                rotalar.Add("/" + m.Groups[1].Value.Replace("[controller]", ad, StringComparison.Ordinal));
            }
            rotalar.Should().NotBeEmpty("controller rotalari okunmali - yoksa tarama vakuma duser");

            // SEGMENT ESLESMESI: uretimdeki StartsWithSegments ile ayni semantik - onek, rotanin
            // ya TAMAMI ya da bir SEGMENT SINIRINDA baslangicidir. "sizeguide" ile "size-guide"
            // bu kurala gore ESLESMEZ (kusurun ta kendisi).
            static bool SegmentEslesir(string rota, string onek) =>
                rota.Equals(onek, StringComparison.OrdinalIgnoreCase) ||
                rota.StartsWith(onek + "/", StringComparison.OrdinalIgnoreCase);

            foreach (var onek in onekler)
            {
                rotalar.Any(r => SegmentEslesir(r, onek)).Should().BeTrue(
                    $"'{onek}' oneki HICBIR gercek rotaya segment-eslesmiyor - OLU ONEK. " +
                    "K1'de '/api/sizeguide' tam bu sekilde ilk commit'ten beri etkisizdi.");
            }

            // CIFT-ANLAM KIRICI: kaldirilan onek GERI GELMEMELI (kaldirma yonu de sabitlenir).
            onekler.Should().NotContain("/api/sizeguide",
                "K1 karari: onek KALDIRILDI, duzeltilmedi - size-guide vitrine baglanirsa " +
                "'/api/size-guide' olarak BILINCLI geri eklenir ve Cache-Control karari onunla verilir");
        }

        // ═══ p-k3 - FILTRELI INDEKS LITERALLERI DORT YERDE BAYT-BIREBIR ════════════════════
        //
        // K3'te olculdu: 8 filtreli indeksin YALNIZ biri (UX_store_credit_referee_reward) bir
        // METIN LITERALINE bagli; UX_loyalty_transactions_order_earn ise bir SAYISAL ENUM
        // sabitine ([type] = 0 == LedgerEntryTypeEnum.Earn). Ikisinde de kuplaj derleyiciye
        // GORUNMEZ: sabit degisirse indeks sessizce eslesmez ve koruma KALKARDI.
        // Kod DEGISTIRILMEDI (kuplaj bilincli kabul edildi); bu pin kuplajin SESSIZ kalmasini
        // engeller - dort artefaktin herhangi biri kayarsa KIRILIR.
        [Fact]
        public void FILTRELI_INDEKS_LITERALLERI_SABITLE_BAYT_BIREBIR_ESIT_KUPLAJ_SESSIZ_KALAMAZ()
        {
            // 1) Sabitin GERCEK degeri - dizgeyi kaynaktan degil, DERLENMIS sabitten al.
            var sabit = ReferralManager.RefereeRewardReason;
            sabit.Should().NotBeNullOrWhiteSpace("sabit dolu olmali - yoksa karsilastirma vakuma duser");

            // 2) DbContext filtre ifadesi
            var ctx = Oku("Divisima.Dal/Concrete/Context/DivisimaDbContext.cs");
            var ctxLit = Regex.Match(ctx, @"HasFilter\(""\[reason\] = N'([^']+)'""\)");
            ctxLit.Success.Should().BeTrue("DbContext'te referee-reward filtre ifadesi bulunmali");

            // 3) Sprint 8 migration
            var mig = Oku("Divisima.Dal/Migrations/20260821202442_RefereeRewardUniquenessSprint8.cs");
            var migLit = Regex.Match(mig, @"filter: ""\[reason\] = N'([^']+)'""");
            migLit.Success.Should().BeTrue("migration'da referee-reward filtresi bulunmali");

            // 4) Uretilen sema dosyasi (tirnaklar SQL'de ciftlenmis: '' )
            var sema = Oku("database/mssql/01_schema.sql");
            var semaLit = Regex.Match(sema, @"UX_store_credit_referee_reward.*?WHERE \[reason\] = N''([^']+)''");
            semaLit.Success.Should().BeTrue("01_schema.sql'de referee-reward indeksi bulunmali");

            ctxLit.Groups[1].Value.Should().Be(sabit,
                "DbContext filtresi ReferralManager.RefereeRewardReason ile BAYT-BIREBIR esit olmali");
            migLit.Groups[1].Value.Should().Be(sabit,
                "Sprint 8 migration literali sabitle BAYT-BIREBIR esit olmali");
            semaLit.Groups[1].Value.Should().Be(sabit,
                "01_schema.sql literali sabitle BAYT-BIREBIR esit olmali");

            // ── AYNI PIN, IKINCI KUPLAJ: UX_loyalty_transactions_order_earn <-> Earn = 0 ──
            ((byte)LedgerEntryTypeEnum.Earn).Should().Be(0,
                "UX_loyalty_transactions_order_earn filtresi '[type] = 0' yaziyor; Earn'un sayisal " +
                "degeri degisirse indeks YANLIS satirlari kisitlar ve cift kazanim korumasi kalkar");
            ctx.Should().Contain("[order_id] IS NOT NULL AND [type] = 0",
                "sadakat kazanim indeksinin filtresi enum degeriyle hizali kalmali");
        }

        // ═══ p-k7a - METADATA ONCELIKLI (oznitelik TEK KAYNAK) ═════════════════════════════
        [Fact]
        public void KOVA_SECIMI_METADATA_ONCELIKLIDIR_LISTEDE_OLMAYAN_YOL_DA_AUTH_OLUR()
        {
            // Ayirt edici degerler: ne 10 ne 100 - "varsayilan geldi" ile karistirilamaz.
            var p = new RateLimitPolitikasi(authLimiti: 37, odemeLimiti: 41, genelLimit: 43);

            // /api/guest-checkout/place KapsamSec listesinde YOK (K7'de olculdu: global'e duserdi)
            // ama endpoint metadata'si "auth" tasiyor -> AUTH kazanmali.
            var (kapsam, limit) = p.KovaSec("auth", "/api/guest-checkout/place");
            kapsam.Should().Be(RateLimitPolitikasi.AuthKapsami);
            limit.Should().Be(37, "limit yapilandirmadan gelen auth degeri olmali");

            // VAKUM KIRICI: ayni yol metadata OLMADAN global'e duser - yani yukaridaki sonucu
            // ureten sey GERCEKTEN metadata, yolun kendisi degil.
            var (yedekKapsam, yedekLimit) = p.KovaSec(null, "/api/guest-checkout/place");
            yedekKapsam.Should().Be(RateLimitPolitikasi.GenelKapsam);
            yedekLimit.Should().Be(43);

            // payment tarafi da metadata'dan gelir
            p.KovaSec("payment", "/api/olmayan").Should().Be((RateLimitPolitikasi.OdemeKapsami, 41));
        }

        // ═══ p-k7b - METADATA YOKSA YEDEK (KapsamSec) CALISIR ══════════════════════════════
        [Fact]
        public void METADATA_YOKSA_YOL_LISTESI_YEDEGI_CALISIR()
        {
            var p = new RateLimitPolitikasi(authLimiti: 37, odemeLimiti: 41, genelLimit: 43);

            p.KovaSec(null, "/api/auth/login").Should().Be((RateLimitPolitikasi.AuthKapsami, 37),
                "endpoint cozulmemis isteklerde (404 vb.) yol listesi hala korumali olmali");
            p.KovaSec("", "/API/AUTH/LOGIN").Should().Be((RateLimitPolitikasi.AuthKapsami, 37),
                "B3: yol KIMLIK dizgesidir, buyuk harfli URL kovadan KACAMAZ");
            p.KovaSec(null, "/api/payment/callback").Should().Be((RateLimitPolitikasi.OdemeKapsami, 41));

            // CIFT-ANLAM KIRICI: taninmayan bir policy adi metadata'yi "uydurmaz", yedege duser.
            p.KovaSec("bilinmeyen-policy", "/api/auth/login").Should().Be((RateLimitPolitikasi.AuthKapsami, 37));
        }

        // ═══ p-k7c - ESLESMEYEN YOL + METADATA YOK -> GLOBAL ═══════════════════════════════
        [Fact]
        public void METADATA_YOK_ve_ESLESMEYEN_YOL_GLOBAL_KOVASINA_DUSER()
        {
            var p = new RateLimitPolitikasi(authLimiti: 37, odemeLimiti: 41, genelLimit: 43);

            p.KovaSec(null, "/api/olmayan-yol").Should().Be((RateLimitPolitikasi.GenelKapsam, 43),
                "ADIM 0'da olculdu: /api/olmayan-yol icin endpoint NULL doner - yedek yol sart");
            p.KovaSec(null, "").Should().Be((RateLimitPolitikasi.GenelKapsam, 43));
            p.KovaSec(null, "/api/product/get/1").Should().Be((RateLimitPolitikasi.GenelKapsam, 43),
                "katalog ucu hicbir kovaya isaretli degil - global dogru sonuc");
        }

        // ═══ p-k7 EK - MIDDLEWARE GERCEKTEN SAF FONKSIYONU CAGIRIYOR ═══════════════════════
        //
        // Yukaridaki uc pin SAF fonksiyonu olcer; bu pin, uretim middleware'inin o fonksiyonu
        // GERCEKTEN kullandigini ve policy adini endpoint metadata'sindan okudugunu sabitler.
        // Olmadan: karar mantigi dogru ama middleware eski `KapsamSec`e donse hicbir pin kirilmazdi.
        [Fact]
        public void MIDDLEWARE_POLICY_ADINI_ENDPOINT_METADATASINDAN_OKUR_ve_SAF_FONKSIYONA_VERIR()
        {
            var kaynak = YorumsuzKaynak("Divisima.API/Middlewares/RedisRateLimitMiddleware.cs");

            kaynak.Should().Contain("GetEndpoint()",
                "kova secimi endpoint metadata'sindan turemeli - oznitelik TEK KAYNAK");
            kaynak.Should().Contain("EnableRateLimitingAttribute",
                "policy adi [EnableRateLimiting] ozniteliginden okunmali");
            kaynak.Should().Contain("PolicyName",
                "ADIM 0'da derleyici kanitiyla dogrulandi: PolicyName public okunabilir");
            kaynak.Should().Contain("KovaSec(",
                "karar mantigi saf fonksiyonda toplanmali - middleware kopyasini TUTMAMALI");

            // CIFT-ANLAM KIRICI: middleware ARTIK dogrudan KapsamSec cagirmamali (yedek yol
            // KovaSec'in ICINDE). Aksi halde iki el yazmasi yeniden dogar.
            Regex.IsMatch(kaynak, @"_politika\.KapsamSec\s*\(").Should().BeFalse(
                "yedege dusme karari KovaSec'in ICINDE - middleware'de ikinci bir cagri olmamali");
        }
    }
}
